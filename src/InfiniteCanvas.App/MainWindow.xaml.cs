using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace InfiniteCanvas.App;

public partial class MainWindow : Window
{
    private static readonly Color AnnotationOutlineColor = Color.FromRgb(230, 58, 58);
    private static readonly Color AnnotationFillColor = Color.FromArgb(220, 230, 58, 58);
    private static readonly Color AnnotationFillOverlayColor = Color.FromArgb(64, 230, 58, 58);

    private LiveSpatialIndexService<SampleAnnotation> _spatialIndex = null!;
    private CanvasViewportViewModel<SampleAnnotation> _viewModel = null!;
    private CameraTransform _camera = new(0.01, 50);
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _resizeTimer;
    private readonly DispatcherTimer _anchorPanTimer;
    private readonly ISelectionOutlineAnimator _selectionOutlineAnimator;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _generationGate = new(1, 1);
    private IReadOnlyList<SampleImageTile> _tiles = [];
    private IReadOnlyList<SampleAnnotation> _annotations = [];
    private SpatialBounds _sceneBounds;
    private ZeroCopyBitmapFactory? _frontBitmapFactory;
    private ZeroCopyBitmapFactory? _backBitmapFactory;
    private Point? _lastPointerPosition;
    private Point? _anchorPanOrigin;
    private Point _anchorPanPointer;
    private Point? _hoverPointerPosition;
    private string? _selectedAnnotationId;
    private AnnotationDisplayOptions _annotationDisplayOptions = AnnotationDisplayOptions.Default;
    private int _tileColumns = 2;
    private int _tileRows = 32;
    private int _objectsPerTile = 16;
    private int _generationSeed = 1729;

    public MainWindow()
    {
        InitializeComponent();

        InitializeSpatialState();
        _renderAction = new CoalescingAsyncAction(DispatchRenderFrameAsync);
        _selectionOutlineAnimator = SelectionOutlineAnimatorFactory.Create(SelectionOutlineAnimationMode.MarchingDash);

        _resizeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnResizeElapsed, Dispatcher)
        {
            IsEnabled = false
        };
        _anchorPanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, OnAnchorPanTick, Dispatcher)
        {
            IsEnabled = false
        };

        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerValueText.Text = "PIXEL --";

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void InitializeSpatialState()
    {
        _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(new StrTreeSpatialIndexBuilder<SampleAnnotation>());
        _viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);
        DataContext = _viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            ApplyGenerationControlsToUi();
            ApplyDisplayOptionsFromUi();
            await RegenerateSceneAsync(fitToWidth: true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            LoadingOverlay.Text = "INITIALIZATION FAILED";
            StatusText.Text = exception.Message;
        }
    }

    private void ApplyGenerationControlsToUi()
    {
        TilesXTextBox.Text = _tileColumns.ToString();
        TilesYTextBox.Text = _tileRows.ToString();
        ObjectsPerTileTextBox.Text = _objectsPerTile.ToString();
    }

    private async Task RegenerateSceneAsync(bool fitToWidth)
    {
        await _generationGate.WaitAsync(_lifetime.Token);
        try
        {
            LoadingOverlay.Text = "GENERATING TILE MATERIAL";
            LoadingOverlay.Visibility = Visibility.Visible;
            RegenerateButton.IsEnabled = false;

            InitializeSpatialState();
            _selectedAnnotationId = null;
            _camera = new CameraTransform(0.01, 50);

            var tileCount = checked(_tileColumns * _tileRows);
            StatusText.Text = $"Generating metadata for {tileCount:N0} inspection tiles";
            SceneSummaryText.Text = $"{tileCount:N0} TILE INSPECTION SCENE ({_tileColumns} x {_tileRows})";

            _tiles = await Task.Run(
                () => SampleImageGenerator.GenerateSet(
                    imageCount: tileCount,
                    objectsPerTile: _objectsPerTile,
                    columns: _tileColumns,
                    rows: _tileRows,
                    seed: _generationSeed++,
                    defectPoolSize: 64),
                _lifetime.Token);
            _sceneBounds = GetSceneBounds(_tiles);

            _annotations = _tiles.SelectMany(tile => tile.Annotations).ToArray();
            _spatialIndex.AddRange(_annotations);
            StatusText.Text = $"Publishing {_annotations.Count:N0} classified annotations";
            await _spatialIndex.PublishSnapshotAsync(_lifetime.Token);

            if (fitToWidth)
            {
                FitSceneToWidth();
            }
            else
            {
                ClampCameraToScene();
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
            await RequestRenderAsync();
        }
        finally
        {
            RegenerateButton.IsEnabled = true;
            _generationGate.Release();
        }
    }

    private async Task RequestRenderAsync()
    {
        try
        {
            await _renderAction.RequestAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private Task DispatchRenderFrameAsync(CancellationToken cancellationToken)
    {
        if (Dispatcher.CheckAccess())
        {
            return RenderFrameAsync(cancellationToken);
        }

        return Dispatcher.InvokeAsync(() => RenderFrameAsync(cancellationToken)).Task.Unwrap();
    }

    private async Task RenderFrameAsync(CancellationToken cancellationToken)
    {
        if (!IsLoaded || ViewportHost.ActualWidth < 1 || ViewportHost.ActualHeight < 1)
        {
            return;
        }

        var width = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualWidth), 1, 4096);
        var height = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualHeight), 1, 4096);
        EnforceZoomFloor(width, height);
        _camera.ClampToBounds(_sceneBounds, width, height);
        var camera = _camera.Capture();
        var viewport = camera.GetViewportBounds(width, height);
        var stopwatch = Stopwatch.StartNew();

        var factory = AcquireBackBuffer(width, height);
        var frame = await Task.Run(() =>
        {
            var visibleItems = _spatialIndex.Query(viewport);
            var visibleTiles = _tiles.Where(tile => tile.Bounds.Intersects(viewport)).ToArray();
            var bitmap = factory.GenerateFrozenBitmap(visibleTiles, visibleItems, camera);
            return (Bitmap: bitmap, VisibleItems: visibleItems, VisibleTiles: visibleTiles);
        }, cancellationToken);

        var frameVisual = BuildFrameVisual(frame.Bitmap, frame.VisibleItems, camera, width, height);
        PublishFrame(factory, frameVisual);
        _viewModel.ApplyFrame(viewport, frame.VisibleItems.Count);

        stopwatch.Stop();
        var generatedTileCount = _tiles.Count(tile => tile.IsBackgroundFetched);
        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  Zoom X {camera.ScaleX:F3} Y {camera.ScaleY:F3}  |  Images {generatedTileCount}/{_tiles.Count}";

        if (_hoverPointerPosition is Point hoverPointer)
        {
            UpdatePixelometer(hoverPointer);
        }
    }

    private ZeroCopyBitmapFactory AcquireBackBuffer(int width, int height)
    {
        if (_backBitmapFactory is not null
            && _backBitmapFactory.Width == width
            && _backBitmapFactory.Height == height)
        {
            return _backBitmapFactory;
        }

        _backBitmapFactory?.Dispose();
        _backBitmapFactory = new ZeroCopyBitmapFactory(width, height);
        return _backBitmapFactory;
    }

    private void PublishFrame(ZeroCopyBitmapFactory renderedBuffer, Grid frameVisual)
    {
        FramePresenter.Child = frameVisual;

        var previousFront = _frontBitmapFactory;
        _frontBitmapFactory = renderedBuffer;
        _backBitmapFactory = null;

        if (previousFront is not null
            && previousFront.Width == renderedBuffer.Width
            && previousFront.Height == renderedBuffer.Height)
        {
            _backBitmapFactory = previousFront;
        }
        else
        {
            previousFront?.Dispose();
        }
    }

    private void FitSceneToWidth()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var targetScale = width / _sceneBounds.Width;
        var scaleDelta = targetScale / _camera.ScaleX;

        if (scaleDelta > 0
            && _camera.Zoom(scaleDelta, scaleDelta, new ScreenPoint(0, 0)))
        {
            ClampCameraToScene();
        }
    }

    private Grid BuildFrameVisual(
        ImageSource bitmap,
        IReadOnlyList<SampleAnnotation> annotations,
        CameraSnapshot camera,
        int frameWidth,
        int frameHeight)
    {
        var frame = new Grid
        {
            Width = frameWidth,
            Height = frameHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        frame.Children.Add(new Image
        {
            Source = bitmap,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true
        });

        var annotationLayer = new Canvas { ClipToBounds = true };
        frame.Children.Add(annotationLayer);

        foreach (var annotation in annotations)
        {
            var topLeft = camera.WorldToScreen(annotation.Bounds.X, annotation.Bounds.Y);
            var width = annotation.Bounds.Width * camera.ScaleX;
            var height = annotation.Bounds.Height * camera.ScaleY;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var outlineBrush = new SolidColorBrush(AnnotationOutlineColor);
            var fillBrush = CreateFillBrush(_annotationDisplayOptions.Mode);
            var outline = new Rectangle
            {
                Stroke = outlineBrush,
                Fill = fillBrush,
                StrokeThickness = _annotationDisplayOptions.OutlineThickness,
                SnapsToDevicePixels = true,
                StrokeDashCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            var label = new TextBlock
            {
                Text = annotation.Id,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono"),
                FontWeight = FontWeights.SemiBold,
                FontSize = _annotationDisplayOptions.LabelSize,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var annotationVisual = new Grid
            {
                Children = { outline }
            };

            if (_annotationDisplayOptions.ShowLabels)
            {
                annotationVisual.Children.Add(label);
            }
            var annotationElement = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent,
                Child = annotationVisual,
                Tag = annotation,
                ToolTip = CreateAnnotationToolTip(annotation)
            };
            annotationElement.MouseLeftButtonDown += OnAnnotationMouseLeftButtonDown;
            Canvas.SetLeft(annotationElement, topLeft.X);
            Canvas.SetTop(annotationElement, topLeft.Y);
            annotationLayer.Children.Add(annotationElement);

            if (annotation.Id == _selectedAnnotationId)
            {
                _selectionOutlineAnimator.Apply(outline);
            }
        }

        return frame;
    }

    private static Brush CreateFillBrush(AnnotationDisplayMode mode)
    {
        return mode switch
        {
            AnnotationDisplayMode.Outline => Brushes.Transparent,
            AnnotationDisplayMode.Fill => new SolidColorBrush(AnnotationFillColor),
            AnnotationDisplayMode.OutlineAndFill => new SolidColorBrush(AnnotationFillOverlayColor),
            _ => Brushes.Transparent
        };
    }

    private static SpatialBounds GetSceneBounds(IReadOnlyList<SampleImageTile> tiles)
    {
        var left = tiles.Min(tile => tile.Bounds.X);
        var top = tiles.Min(tile => tile.Bounds.Y);
        var right = tiles.Max(tile => tile.Bounds.Right);
        var bottom = tiles.Max(tile => tile.Bounds.Bottom);
        return new SpatialBounds(left, top, right - left, bottom - top);
    }

    private void ClampCameraToScene()
    {
        var width = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualWidth), 1, 4096);
        var height = Math.Clamp((int)Math.Ceiling(ViewportHost.ActualHeight), 1, 4096);
        EnforceZoomFloor(width, height);
        _camera.ClampToBounds(_sceneBounds, width, height);
    }

    private void EnforceZoomFloor(double viewportWidth, double viewportHeight)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
        var currentScaleX = _camera.ScaleX;
        var currentScaleY = _camera.ScaleY;
        if (currentScaleX >= minimumScaleX && currentScaleY >= minimumScaleY)
        {
            return;
        }

        var origin = new ScreenPoint(viewportWidth / 2, viewportHeight / 2);
        var scaleXDelta = currentScaleX < minimumScaleX ? minimumScaleX / currentScaleX : 1;
        var scaleYDelta = currentScaleY < minimumScaleY ? minimumScaleY / currentScaleY : 1;
        _camera.Zoom(scaleXDelta, scaleYDelta, origin);
    }

    private (double ScaleX, double ScaleY) ComputeMinimumZoom(double viewportWidth, double viewportHeight)
    {
        return (
            viewportWidth / _sceneBounds.Width,
            viewportHeight / _sceneBounds.Height);
    }

    private static ToolTip CreateAnnotationToolTip(SampleAnnotation annotation)
    {
        var confidence = annotation.Features["Confidence"];
        var severity = annotation.Features["Severity"];
        return new ToolTip
        {
            Content = $"{annotation.Id}\n{annotation.Classification}\nConfidence {confidence:P1}  |  Severity {severity:P1}"
        };
    }

    private async void OnAnnotationMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: SampleAnnotation annotation })
        {
            _selectedAnnotationId = annotation.Id;
            e.Handled = true;
            await RequestRenderAsync();
        }
    }

    private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        _lastPointerPosition = e.GetPosition(ViewportHost);
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        _lastPointerPosition = null;
        ViewportHost.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private async void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(ViewportHost);
        _hoverPointerPosition = current;
        UpdatePixelometer(current);

        if (_anchorPanOrigin is not null)
        {
            _anchorPanPointer = current;
        }

        if (_lastPointerPosition is not Point previous || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _lastPointerPosition = current;
        _camera.Pan(current.X - previous.X, current.Y - previous.Y);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void OnViewportMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _anchorPanOrigin = e.GetPosition(ViewportHost);
        _anchorPanPointer = _anchorPanOrigin.Value;
        _lastPointerPosition = null;
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.ScrollAll;
        ShowPanAnchor(_anchorPanOrigin.Value);
        _anchorPanTimer.Start();
        e.Handled = true;
    }

    private void OnViewportMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is null)
        {
            return;
        }

        StopAnchorPan();
        e.Handled = true;
    }

    private async void OnAnchorPanTick(object? sender, EventArgs e)
    {
        if (_anchorPanOrigin is not Point anchor)
        {
            return;
        }

        const double deadZone = 6;
        const double gain = 0.12;

        var deltaX = _anchorPanPointer.X - anchor.X;
        var deltaY = _anchorPanPointer.Y - anchor.Y;
        var adjustedX = ApplyDeadZone(deltaX, deadZone);
        var adjustedY = ApplyDeadZone(deltaY, deadZone);
        if (adjustedX == 0 && adjustedY == 0)
        {
            return;
        }

        _camera.Pan(-(adjustedX * gain), -(adjustedY * gain));
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private static double ApplyDeadZone(double value, double deadZone)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= deadZone)
        {
            return 0;
        }

        return Math.Sign(value) * (magnitude - deadZone);
    }

    private void ShowPanAnchor(Point anchor)
    {
        Canvas.SetLeft(PanAnchorVisual, anchor.X - (PanAnchorVisual.Width / 2));
        Canvas.SetTop(PanAnchorVisual, anchor.Y - (PanAnchorVisual.Height / 2));
        PanAnchorVisual.Visibility = Visibility.Visible;
    }

    private void StopAnchorPan()
    {
        _anchorPanTimer.Stop();
        _anchorPanOrigin = null;
        PanAnchorVisual.Visibility = Visibility.Collapsed;
        ViewportHost.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private void OnViewportMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverPointerPosition = null;
        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private async void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var origin = e.GetPosition(ViewportHost);
        _hoverPointerPosition = origin;
        UpdatePixelometer(origin);
        var requestedScaleDelta = e.Delta > 0 ? 1.15 : 1 / 1.15;
        var requestedScaleXDelta = requestedScaleDelta;
        var requestedScaleYDelta = requestedScaleDelta;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            requestedScaleYDelta = 1;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            requestedScaleXDelta = 1;
        }

        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);

        if (!TryComputeZoomDeltas(width, height, requestedScaleXDelta, requestedScaleYDelta, out var zoomXDelta, out var zoomYDelta))
        {
            return;
        }

        if (_camera.Zoom(zoomXDelta, zoomYDelta, new ScreenPoint(origin.X, origin.Y)))
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
    }

    private bool TryComputeZoomDeltas(
        double viewportWidth,
        double viewportHeight,
        double requestedScaleXDelta,
        double requestedScaleYDelta,
        out double scaleXDelta,
        out double scaleYDelta)
    {
        var currentScaleX = _camera.ScaleX;
        var currentScaleY = _camera.ScaleY;
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);

        var targetScaleX = currentScaleX * requestedScaleXDelta;
        var targetScaleY = currentScaleY * requestedScaleYDelta;

        if (requestedScaleXDelta < 1 && targetScaleX < minimumScaleX)
        {
            targetScaleX = minimumScaleX;
        }

        if (requestedScaleYDelta < 1 && targetScaleY < minimumScaleY)
        {
            targetScaleY = minimumScaleY;
        }

        scaleXDelta = targetScaleX / currentScaleX;
        scaleYDelta = targetScaleY / currentScaleY;

        return Math.Abs(scaleXDelta - 1) > double.Epsilon || Math.Abs(scaleYDelta - 1) > double.Epsilon;
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || LoadingOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private async void OnResizeElapsed(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        try
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnDisplayModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnOutlineThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnLabelSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private async void OnShowLabelsChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyDisplayOptionsFromUi();
        await RequestRenderAsync();
    }

    private void ApplyDisplayOptionsFromUi()
    {
        var selectedMode = DisplayModeComboBox.SelectedIndex switch
        {
            1 => AnnotationDisplayMode.Fill,
            2 => AnnotationDisplayMode.OutlineAndFill,
            _ => AnnotationDisplayMode.Outline
        };

        _annotationDisplayOptions = new AnnotationDisplayOptions(
            selectedMode,
            Math.Round(OutlineThicknessSlider.Value, 2),
            Math.Round(LabelSizeSlider.Value, 2),
            ShowLabelsCheckBox.IsChecked ?? true);
    }

    private async void OnRegenerateClicked(object sender, RoutedEventArgs e)
    {
        if (!TryReadGenerationOptions(out var validationError))
        {
            StatusText.Text = validationError;
            return;
        }

        await RegenerateSceneAsync(fitToWidth: true);
    }

    private bool TryReadGenerationOptions(out string validationError)
    {
        validationError = string.Empty;

        if (!int.TryParse(TilesXTextBox.Text, out var columns) || columns <= 0)
        {
            validationError = "Tiles X must be a positive integer.";
            return false;
        }

        if (!int.TryParse(TilesYTextBox.Text, out var rows) || rows <= 0)
        {
            validationError = "Tiles Y must be a positive integer.";
            return false;
        }

        if (!int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile) || objectsPerTile < 0)
        {
            validationError = "Objects per tile must be zero or greater.";
            return false;
        }

        if ((long)columns * rows > 2000)
        {
            validationError = "Tile count must be 2000 or less for this demo.";
            return false;
        }

        _tileColumns = columns;
        _tileRows = rows;
        _objectsPerTile = objectsPerTile;
        return true;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        _anchorPanTimer.Stop();
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        FramePresenter.Child = null;
        _frontBitmapFactory?.Dispose();
        _backBitmapFactory?.Dispose();
        _generationGate.Dispose();
        _lifetime.Dispose();
    }

    private void UpdatePixelometer(Point screenPoint)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        var camera = _camera.Capture();
        var worldX = (screenPoint.X - camera.OffsetX) / camera.ScaleX;
        var worldY = (screenPoint.Y - camera.OffsetY) / camera.ScaleY;

        PixelometerWorldText.Text = $"WORLD X {worldX:F1}  Y {worldY:F1}";

        if (TryReadPixelValue(worldX, worldY, out var backgroundValue, out var defectValue, out var tileId))
        {
            var finalValue = BlendDefect(backgroundValue, defectValue);
            PixelometerValueText.Text = $"PIXEL {finalValue}  ({tileId}) bg {backgroundValue} + defect {defectValue}";
            return;
        }

        PixelometerValueText.Text = "PIXEL --";
    }

    private bool TryReadPixelValue(double worldX, double worldY, out byte background, out byte defect, out string tileId)
    {
        foreach (var tile in _tiles)
        {
            if (tile.TryGetPixelValue(worldX, worldY, out background))
            {
                defect = 0;
                for (var index = 0; index < _annotations.Count; index++)
                {
                    if (_annotations[index].TryGetDefectValue(worldX, worldY, out var value))
                    {
                        defect = Math.Max(defect, value);
                    }
                }

                tileId = tile.Id;
                return true;
            }
        }

        background = default;
        defect = default;
        tileId = string.Empty;
        return false;
    }

    private static byte BlendDefect(byte baseValue, byte defectValue)
    {
        return (byte)Math.Clamp(baseValue - (defectValue / 2), byte.MinValue, byte.MaxValue);
    }

    private sealed record AnnotationDisplayOptions(
        AnnotationDisplayMode Mode,
        double OutlineThickness,
        double LabelSize,
        bool ShowLabels)
    {
        public static AnnotationDisplayOptions Default { get; } = new(
            AnnotationDisplayMode.Outline,
            2,
            12,
            true);
    }

    private enum AnnotationDisplayMode
    {
        Outline,
        Fill,
        OutlineAndFill
    }

    private interface ISelectionOutlineAnimator
    {
        void Apply(Shape outline);
    }

    private sealed class MarchingDashSelectionOutlineAnimator : ISelectionOutlineAnimator
    {
        public void Apply(Shape outline)
        {
            outline.StrokeDashArray = [4, 3];
            var animation = new DoubleAnimation(0, -14, TimeSpan.FromMilliseconds(420))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            outline.BeginAnimation(Shape.StrokeDashOffsetProperty, animation);
        }
    }

    private sealed class PulseOpacitySelectionOutlineAnimator : ISelectionOutlineAnimator
    {
        public void Apply(Shape outline)
        {
            var animation = new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(360))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            outline.BeginAnimation(OpacityProperty, animation);
        }
    }

    private enum SelectionOutlineAnimationMode
    {
        MarchingDash,
        PulseOpacity
    }

    private static class SelectionOutlineAnimatorFactory
    {
        public static ISelectionOutlineAnimator Create(SelectionOutlineAnimationMode mode)
        {
            return mode switch
            {
                SelectionOutlineAnimationMode.PulseOpacity => new PulseOpacitySelectionOutlineAnimator(),
                _ => new MarchingDashSelectionOutlineAnimator()
            };
        }
    }
}
