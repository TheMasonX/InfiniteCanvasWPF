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
    private LiveSpatialIndexService<SampleAnnotation> _spatialIndex = null!;
    private CanvasViewportViewModel<SampleAnnotation> _viewModel = null!;
    private CameraTransform _camera = new();
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _resizeTimer;
    private readonly DispatcherTimer _anchorPanTimer;
    private readonly ISelectionOutlineAnimator _selectionOutlineAnimator;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _generationGate = new(1, 1);
    private readonly string _settingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InfiniteCanvas",
        "settings.json");
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
    private int _busyOperationCount;
    private TileCacheBudget _tileCacheBudget = new(TileCacheBudget.DefaultMaxBytes);
    private bool _showBackgroundImages = true;
    private byte _backgroundNoise = 8;
    private int _backgroundCircleCount = 3;
    private bool _showImageTiles = true;
    private ViewportScrollbarAxis? _scrollbarDragAxis;
    private double _scrollbarDragPointerOffset;
    private Canvas? _viewportScrollbarOverlay;
    private Border? _horizontalScrollbarTrack;
    private Border? _horizontalScrollbarThumb;
    private Border? _verticalScrollbarTrack;
    private Border? _verticalScrollbarThumb;
    private IReadOnlyList<FeatureDisplayItem> _selectedAnnotationFeatures = [];

    public IReadOnlyList<FeatureDisplayItem> SelectedAnnotationFeatures => _selectedAnnotationFeatures;

    public MainWindow()
    {
        InitializeComponent();

        _viewportScrollbarOverlay = (Canvas?)FindName("ViewportScrollbarOverlay");
        _horizontalScrollbarTrack = (Border?)FindName("HorizontalScrollbarTrack");
        _horizontalScrollbarThumb = (Border?)FindName("HorizontalScrollbarThumb");
        _verticalScrollbarTrack = (Border?)FindName("VerticalScrollbarTrack");
        _verticalScrollbarThumb = (Border?)FindName("VerticalScrollbarThumb");

        InitializeSpatialState();
        _renderAction = new CoalescingAsyncAction(DispatchRenderFrameAsync, OnRenderActionFaulted);
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
        ApplySettingsToUi(CanvasUserSettingsStore.Load(_settingsPath));

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void InitializeSpatialState()
    {
        _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(new StrTreeSpatialIndexBuilder<SampleAnnotation>());
        _viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);
        DataContext = _viewModel;
    }

    private void OnAboutButtonClicked(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
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

    private void ApplySettingsToUi(CanvasUserSettings settings)
    {
        _tileColumns = settings.TileColumns;
        _tileRows = settings.TileRows;
        _objectsPerTile = settings.ObjectsPerTile;
        ApplyGenerationControlsToUi();
        DisplayModeComboBox.SelectedIndex = settings.AnnotationDisplayMode;
        OutlineThicknessSlider.Value = settings.OutlineThickness;
        LabelSizeSlider.Value = settings.LabelSize;
        LabelDisplayComboBox.SelectedIndex = settings.LabelDisplay;
        ShowLabelsCheckBox.IsChecked = settings.ShowLabels;
        ShowImageTilesCheckBox.IsChecked = true;
        ShowBackgroundImagesCheckBox.IsChecked = settings.ShowBackgroundImages;
        _showBackgroundImages = settings.ShowBackgroundImages;
        _backgroundNoise = settings.BackgroundNoise;
        _backgroundCircleCount = settings.BackgroundCircleCount;
        BackgroundNoiseSlider.Value = settings.BackgroundNoise;
        BackgroundCircleCountSlider.Value = settings.BackgroundCircleCount;
    }

    private async Task RegenerateSceneAsync(bool fitToWidth)
    {
        await _generationGate.WaitAsync(_lifetime.Token);
        BeginBusyOperation();
        try
        {
            LoadingOverlay.Text = "GENERATING TILE MATERIAL";
            LoadingOverlay.Visibility = Visibility.Visible;
            RegenerateButton.IsEnabled = false;

            InitializeSpatialState();
            _selectedAnnotationId = null;
            _camera = new CameraTransform();
            _tileCacheBudget = new TileCacheBudget(_tileCacheBudget.MaxBytes);
            UnsubscribeTileGenerationEvents(_tiles);

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
                    defectPoolSize: 64,
                    noise: _backgroundNoise,
                    circleCount: _backgroundCircleCount),
                _lifetime.Token);
            SubscribeTileGenerationEvents(_tiles);
            _sceneBounds = GetSceneBounds(_tiles);

            _annotations = _tiles.SelectMany(tile => tile.Annotations).ToArray();
            _spatialIndex.AddRange(_annotations);
            UpdateSelectedAnnotationFeatures();
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
            EndBusyOperation();
            _generationGate.Release();
        }
    }

    private void SubscribeTileGenerationEvents(IReadOnlyList<SampleImageTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].PixelsGenerated += OnTilePixelsGenerated;
            tiles[i].PixelsGenerationFailed += OnTilePixelsGenerationFailed;
        }
    }

    private void UnsubscribeTileGenerationEvents(IReadOnlyList<SampleImageTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].PixelsGenerated -= OnTilePixelsGenerated;
            tiles[i].PixelsGenerationFailed -= OnTilePixelsGenerationFailed;
        }
    }

    private void OnTilePixelsGenerated(object? sender, EventArgs e)
    {
        if (_lifetime.IsCancellationRequested)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (!IsLoaded || _lifetime.IsCancellationRequested)
            {
                return;
            }

            await RequestRenderAsync();
        });
    }

    private void OnTilePixelsGenerationFailed(object? sender, EventArgs e)
    {
        if (sender is SampleImageTile tile)
        {
            _tileCacheBudget.Release(tile);
        }
    }

    private async Task RequestRenderAsync()
    {
        BeginBusyOperation();
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
        finally
        {
            EndBusyOperation();
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

    private static void OnRenderActionFaulted(Exception exception)
    {
        Serilog.Log.Error(exception, "Render action failed");
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
            _tileCacheBudget.SetPinnedTiles(visibleTiles);
            var bitmap = factory.GenerateFrozenBitmap(
                visibleTiles,
                visibleItems,
                camera,
                _tileCacheBudget.TryReserve);
            return (Bitmap: bitmap, VisibleItems: visibleItems, VisibleTiles: visibleTiles);
        }, cancellationToken);

        var frameVisual = BuildFrameVisual(frame.Bitmap, frame.VisibleItems, camera, width, height);
        PublishFrame(factory, frameVisual);
        _viewModel.ApplyFrame(viewport, frame.VisibleItems.Count);

        stopwatch.Stop();
        var generatedTileCount = _tiles.Count(tile => tile.IsBackgroundFetched);
        var visibleBackgroundTileCount = frame.VisibleTiles.Count(tile => tile.IsImageGenerated);
        UpdateCacheStatus(visibleBackgroundTileCount);
        var queuedTileCount = _tiles.Count(tile => tile.IsGenerationQueued);
        var completedTiles = _tiles.Where(tile => tile.GenerationDuration.HasValue).ToArray();
        var averageGenerationMilliseconds = completedTiles.Length == 0
            ? 0
            : completedTiles.Average(tile => tile.GenerationDuration!.Value.TotalMilliseconds);
        var averageConversionMilliseconds = completedTiles.Length == 0
            ? 0
            : completedTiles.Average(tile => tile.BitmapConversionDuration!.Value.TotalMilliseconds);
        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  Zoom {camera.ScaleX:F3}x  |  Backgrounds {visibleBackgroundTileCount}/{frame.VisibleTiles.Length} visible, {generatedTileCount} total  |  Queue {queuedTileCount}  |  Gen {averageGenerationMilliseconds:F1} ms  |  Gray8 {averageConversionMilliseconds:F1} ms";
        UpdateZoomDisplay(camera, width, height);
        UpdateViewportScrollbars(camera, width, height);

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
        ApplyFitToWidthZoom();
        ClampCameraToScene();
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
        if (_showBackgroundImages && _showImageTiles)
        {
            frame.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            });
        }

        frame.Children.Add(BuildTileGridLayer(camera, frameWidth, frameHeight));

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

            var outlineBrush = new SolidColorBrush(ToMediaColor(annotation.Color));
            var fillBrush = CreateFillBrush(_annotationDisplayOptions.Mode, annotation.Color);
            var outline = new Rectangle
            {
                Stroke = outlineBrush,
                Fill = fillBrush,
                StrokeThickness = _annotationDisplayOptions.OutlineThickness,
                SnapsToDevicePixels = true,
                StrokeDashCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            var annotationVisual = new Grid
            {
                Children = { outline }
            };
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

            if (_annotationDisplayOptions.ShowLabels)
            {
                var labelPanel = BuildAnnotationLabel(
                    annotation,
                    topLeft,
                    outlineBrush,
                    _annotationDisplayOptions.LabelSize,
                    _annotationDisplayOptions.LabelDisplay);
                annotationLayer.Children.Add(labelPanel);
            }

            if (annotation.Id == _selectedAnnotationId)
            {
                _selectionOutlineAnimator.Apply(outline);
            }
        }

        return frame;
    }

    private Canvas BuildTileGridLayer(CameraSnapshot camera, int frameWidth, int frameHeight)
    {
        var gridLayer = new Canvas
        {
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        var gridBrush = new SolidColorBrush(Color.FromArgb(180, 40, 210, 190));
        gridBrush.Freeze();

        foreach (var worldX in _tiles.SelectMany(tile => new[] { tile.Bounds.X, tile.Bounds.Right }).Distinct())
        {
            var screenX = camera.WorldToScreen(worldX, _sceneBounds.Y).X;
            gridLayer.Children.Add(new Line
            {
                X1 = screenX,
                X2 = screenX,
                Y1 = 0,
                Y2 = frameHeight,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        foreach (var worldY in _tiles.SelectMany(tile => new[] { tile.Bounds.Y, tile.Bounds.Bottom }).Distinct())
        {
            var screenY = camera.WorldToScreen(_sceneBounds.X, worldY).Y;
            gridLayer.Children.Add(new Line
            {
                X1 = 0,
                X2 = frameWidth,
                Y1 = screenY,
                Y2 = screenY,
                Stroke = gridBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        return gridLayer;
    }

    private static Border BuildAnnotationLabel(
        SampleAnnotation annotation,
        ScreenPoint topLeft,
        SolidColorBrush outlineBrush,
        double labelSize,
        AnnotationLabelDisplay labelDisplay)
    {
        var labelPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 16, 22, 28)),
            BorderBrush = outlineBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = labelDisplay == AnnotationLabelDisplay.Id
                    ? annotation.ObjectId
                    : annotation.Classification,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono"),
                FontWeight = FontWeights.SemiBold,
                FontSize = labelSize,
                TextAlignment = TextAlignment.Left
            }
        };

        Canvas.SetLeft(labelPanel, topLeft.X);
        Canvas.SetTop(labelPanel, topLeft.Y - 22);
        return labelPanel;
    }

    private static Brush CreateFillBrush(AnnotationDisplayMode mode, Bgra32Color classColor)
    {
        var fillColor = Color.FromArgb(220, classColor.Red, classColor.Green, classColor.Blue);
        var overlayColor = Color.FromArgb(64, classColor.Red, classColor.Green, classColor.Blue);
        return mode switch
        {
            AnnotationDisplayMode.Outline => Brushes.Transparent,
            AnnotationDisplayMode.Fill => new SolidColorBrush(fillColor),
            AnnotationDisplayMode.OutlineAndFill => new SolidColorBrush(overlayColor),
            _ => Brushes.Transparent
        };
    }

    private static Color ToMediaColor(Bgra32Color color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
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

        var minimumUniform = Math.Max(minimumScaleX, minimumScaleY);
        if (Math.Abs(currentScaleX - currentScaleY) <= 0.0001)
        {
            var uniformDelta = minimumUniform / currentScaleX;
            _camera.Zoom(uniformDelta, uniformDelta, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
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
            UpdateSelectedAnnotationFeatures(annotation);
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

    private async void OnScrollbarTrackMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border track || e.OriginalSource is Border { Name: "HorizontalScrollbarThumb" or "VerticalScrollbarThumb" })
        {
            return;
        }

        var axis = track == _horizontalScrollbarTrack ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        if (_viewportScrollbarOverlay is null)
        {
            return;
        }

        var thumb = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarThumb : _verticalScrollbarThumb;
        if (thumb is null)
        {
            return;
        }

        var pointer = e.GetPosition(_viewportScrollbarOverlay);
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackPosition = axis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(track) : Canvas.GetTop(track);
        var target = (pointerPosition - trackPosition - (thumbLength / 2)) / Math.Max(1, trackLength - thumbLength);
        await PanToScrollbarPositionAsync(axis, target);
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border thumb)
        {
            return;
        }

        _scrollbarDragAxis = thumb == _horizontalScrollbarThumb ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        var track = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarTrack : _verticalScrollbarTrack;
        if (track is null)
        {
            return;
        }

        var pointer = e.GetPosition(track);
        var thumbPosition = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(thumb) : Canvas.GetTop(thumb);
        _scrollbarDragPointerOffset = (_scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y) - thumbPosition;
        thumb.CaptureMouse();
        e.Handled = true;
    }

    private async void OnScrollbarThumbMouseMove(object sender, MouseEventArgs e)
    {
        if (_scrollbarDragAxis is not ViewportScrollbarAxis axis || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var track = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarTrack : _verticalScrollbarTrack;
        var thumb = axis == ViewportScrollbarAxis.Horizontal ? _horizontalScrollbarThumb : _verticalScrollbarThumb;
        if (track is null || thumb is null)
        {
            return;
        }

        var pointer = e.GetPosition(track);
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        await PanToScrollbarPositionAsync(axis, (pointerPosition - _scrollbarDragPointerOffset) / Math.Max(1, trackLength - thumbLength));
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border thumb)
        {
            thumb.ReleaseMouseCapture();
        }

        _scrollbarDragAxis = null;
        e.Handled = true;
    }

    private async Task PanToScrollbarPositionAsync(ViewportScrollbarAxis axis, double targetPosition)
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var delta = ViewportScrollbarPolicy.ComputePanDelta(_camera.Capture(), _sceneBounds, width, height, axis, targetPosition);
        if (delta == 0)
        {
            return;
        }

        _camera.Pan(axis == ViewportScrollbarAxis.Horizontal ? delta : 0, axis == ViewportScrollbarAxis.Vertical ? delta : 0);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void UpdateViewportScrollbars(CameraSnapshot camera, double viewportWidth, double viewportHeight)
    {
        if (_viewportScrollbarOverlay is null || _horizontalScrollbarTrack is null || _horizontalScrollbarThumb is null || _verticalScrollbarTrack is null || _verticalScrollbarThumb is null)
        {
            return;
        }

        const double margin = 10;
        const double thickness = 10;
        const double minimumThumbLength = 24;
        var horizontalLength = Math.Max(0, viewportWidth - (margin * 2) - thickness - 4);
        var verticalLength = Math.Max(0, viewportHeight - (margin * 2) - thickness - 4);
        UpdateScrollbar(ViewportScrollbarAxis.Horizontal, ViewportScrollbarPolicy.ComputeMetrics(camera, _sceneBounds, viewportWidth, viewportHeight, ViewportScrollbarAxis.Horizontal), _horizontalScrollbarTrack, _horizontalScrollbarThumb, margin, viewportHeight - margin - thickness, horizontalLength, minimumThumbLength);
        UpdateScrollbar(ViewportScrollbarAxis.Vertical, ViewportScrollbarPolicy.ComputeMetrics(camera, _sceneBounds, viewportWidth, viewportHeight, ViewportScrollbarAxis.Vertical), _verticalScrollbarTrack, _verticalScrollbarThumb, viewportWidth - margin - thickness, margin, verticalLength, minimumThumbLength);
    }

    private static void UpdateScrollbar(ViewportScrollbarAxis axis, ViewportScrollbarMetrics metrics, Border track, Border thumb, double left, double top, double trackLength, double minimumThumbLength)
    {
        if (!metrics.IsScrollable || trackLength <= minimumThumbLength)
        {
            track.Visibility = Visibility.Collapsed;
            thumb.Visibility = Visibility.Collapsed;
            return;
        }

        track.Visibility = Visibility.Visible;
        thumb.Visibility = Visibility.Visible;
        var thumbLength = Math.Clamp(trackLength * metrics.ViewportFraction, minimumThumbLength, trackLength);
        var thumbPosition = (trackLength - thumbLength) * metrics.PositionFraction;
        Canvas.SetLeft(track, left);
        Canvas.SetTop(track, top);
        Canvas.SetLeft(thumb, axis == ViewportScrollbarAxis.Horizontal ? left + thumbPosition : left);
        Canvas.SetTop(thumb, axis == ViewportScrollbarAxis.Vertical ? top + thumbPosition : top);
        if (axis == ViewportScrollbarAxis.Horizontal)
        {
            track.Width = trackLength;
            thumb.Width = thumbLength;
        }
        else
        {
            track.Height = trackLength;
            thumb.Height = thumbLength;
        }
    }

    private async void OnShowBackgroundImagesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showBackgroundImages = ShowBackgroundImagesCheckBox.IsChecked ?? true;
        await RequestRenderAsync();
    }

    private void OnViewportMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverPointerPosition = null;
        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private async void OnShowImageTilesChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showImageTiles = ShowImageTilesCheckBox.IsChecked ?? true;
        await RequestRenderAsync();
    }

    private async void OnBackgroundNoiseChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _backgroundNoise = (byte)Math.Round(BackgroundNoiseSlider.Value);
        await RegenerateSceneAsync(fitToWidth: false);
    }

    private async void OnBackgroundCircleCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _backgroundCircleCount = (int)Math.Round(BackgroundCircleCountSlider.Value);
        await RegenerateSceneAsync(fitToWidth: false);
    }

    private async void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var origin = e.GetPosition(ViewportHost);
        _hoverPointerPosition = origin;
        UpdatePixelometer(origin);
        var requestedScaleDelta = e.Delta > 0 ? 1.15 : 1 / 1.15;

        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);

        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        var zoomDeltas = ViewportZoomPolicy.ComputeWheelDeltas(
            _camera.ScaleX,
            _camera.ScaleY,
            minimumScaleX,
            minimumScaleY,
            requestedScaleDelta);
        if (!zoomDeltas.HasChange)
        {
            return;
        }

        if (_camera.Zoom(zoomDeltas.ScaleX, zoomDeltas.ScaleY, new ScreenPoint(origin.X, origin.Y)))
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
    }

    private void UpdateZoomDisplay(CameraSnapshot camera, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
        var percent = ViewportZoomPolicy.ComputeDisplayPercent(
            camera.ScaleX,
            camera.ScaleY,
            minimumScaleX,
            minimumScaleY);
        ZoomPresetComboBox.Text = $"{percent:F0}%";
    }

    private async void OnZoomPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var mode = ZoomPresetComboBox.SelectedIndex;
        if (mode < 0)
        {
            return;
        }

        ZoomPresetComboBox.SelectedIndex = -1;
        if (mode == 7)
        {
            CustomZoomPanel.Visibility = Visibility.Visible;
            CustomZoomTextBox.Focus();
            CustomZoomTextBox.SelectAll();
            return;
        }

        CustomZoomPanel.Visibility = Visibility.Collapsed;
        await ApplyZoomPresetAsync(mode);
    }

    private async void OnCustomZoomClicked(object sender, RoutedEventArgs e)
    {
        await ApplyCustomZoomAsync();
    }

    private async void OnCustomZoomKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyCustomZoomAsync();
    }

    private async Task ApplyCustomZoomAsync()
    {
        if (!double.TryParse(CustomZoomTextBox.Text, out var percent)
            || !double.IsFinite(percent)
            || percent <= 0)
        {
            StatusText.Text = "Custom zoom must be a positive percentage";
            CustomZoomTextBox.Focus();
            CustomZoomTextBox.SelectAll();
            return;
        }

        ApplyPercentZoom(percent);
        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private async Task ApplyZoomPresetAsync(int mode)
    {
        if (_tiles.Count == 0)
        {
            return;
        }

        switch (mode)
        {
            case 0:
                ApplyFitToWidthZoom();
                break;
            case 1:
                ApplyFitToHeightZoom();
                break;
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
                var percent = mode switch
                {
                    2 => 50,
                    3 => 75,
                    4 => 100,
                    5 => 150,
                    _ => 200
                };
                ApplyPercentZoom(percent);
                break;
            default:
                return;
        }

        ClampCameraToScene();
        await RequestRenderAsync();
    }

    private void ApplyPercentZoom(double percent)
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        var baseUniformScale = Math.Max(minimumScaleX, minimumScaleY);
        var targetScale = baseUniformScale * (percent / 100.0);
        var delta = targetScale / _camera.ScaleX;

        _camera.Zoom(delta, delta, new ScreenPoint(width / 2, height / 2));
    }

    private void ApplyFitToWidthZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleX, minimumScaleY, width, height);
    }

    private void ApplyFitToHeightZoom()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
        ApplyScaleWithUniformFirst(minimumScaleY, minimumScaleY, width, height);
    }

    private void ApplyScaleWithUniformFirst(double preferredUniformScale, double fallbackScaleY, double viewportWidth, double viewportHeight)
    {
        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
        var minimumUniform = Math.Max(minimumScaleX, minimumScaleY);

        if (preferredUniformScale >= minimumUniform)
        {
            var uniformDelta = preferredUniformScale / _camera.ScaleX;
            _camera.Zoom(uniformDelta, uniformDelta, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
            return;
        }

        var targetScaleX = Math.Max(minimumScaleX, preferredUniformScale);
        var targetScaleY = Math.Max(minimumScaleY, fallbackScaleY);
        var deltaX = targetScaleX / _camera.ScaleX;
        var deltaY = targetScaleY / _camera.ScaleY;
        _camera.Zoom(deltaX, deltaY, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
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

    private async void OnLabelDisplaySelectionChanged(object sender, SelectionChangedEventArgs e)
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
            LabelDisplayComboBox.SelectedIndex == 1 ? AnnotationLabelDisplay.Id : AnnotationLabelDisplay.Class,
            ShowLabelsCheckBox.IsChecked ?? true);
    }

    private async void OnRegenerateClicked(object sender, RoutedEventArgs e)
    {
        if (!TryReadGenerationOptions(out var validationError))
        {
            StatusText.Text = validationError;
            return;
        }

        _selectedAnnotationId = null;
        UpdateSelectedAnnotationFeatures();
        await RegenerateSceneAsync(fitToWidth: true);
    }

    private async void OnDebugDumpCacheClicked(object sender, RoutedEventArgs e)
    {
        var fetchedTiles = _tiles.Where(tile => tile.IsBackgroundFetched).Select(tile => tile.Id).ToArray();
        var dump = $"Cache fetched {fetchedTiles.Length}/{_tiles.Count}: {string.Join(", ", fetchedTiles.Take(10))}{(fetchedTiles.Length > 10 ? "..." : string.Empty)}";
        Serilog.Log.Debug(dump);

        foreach (var tile in _tiles)
        {
            tile.ResetImageCache();
        }

        _tileCacheBudget = new TileCacheBudget(_tileCacheBudget.MaxBytes);
        UpdateCacheStatus();
        StatusText.Text = "Image cache reset. Tiles will regenerate lazily as they come into range.";
        await RequestRenderAsync();
    }

    private void UpdateCacheStatus(int? visibleBackgroundTileCount = null)
    {
        var visibleCount = visibleBackgroundTileCount ?? 0;
        var cacheSummary = _tileCacheBudget.DescribeStatus();
        CacheStatusText.Text = cacheSummary;
        Serilog.Log.Debug("Cache summary: {Summary} (visible backgrounds {Visible})", cacheSummary, visibleCount);
    }

    private void UpdateSelectedAnnotationFeatures(SampleAnnotation? annotation = null)
    {
        var selectedAnnotation = annotation ?? _annotations.FirstOrDefault(item => item.Id == _selectedAnnotationId);
        if (selectedAnnotation is null)
        {
            _selectedAnnotationFeatures = [];
            FeatureDataGrid.ItemsSource = SelectedAnnotationFeatures;
            return;
        }

        _selectedAnnotationFeatures = selectedAnnotation.GetFeatureDisplayItems();
        FeatureDataGrid.ItemsSource = SelectedAnnotationFeatures;
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

        if (!int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile)
            || objectsPerTile < 0
            || objectsPerTile > SampleImageGenerator.MaxObjectsPerTile)
        {
            validationError = $"Objects per tile must be between 0 and {SampleImageGenerator.MaxObjectsPerTile:N0}.";
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
        SaveSettings();
        _resizeTimer.Stop();
        _anchorPanTimer.Stop();
        UnsubscribeTileGenerationEvents(_tiles);
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        FramePresenter.Child = null;
        _frontBitmapFactory?.Dispose();
        _backBitmapFactory?.Dispose();
        _generationGate.Dispose();
        _lifetime.Dispose();
    }

    private void SaveSettings()
    {
        var settings = new CanvasUserSettings
        {
            TileColumns = int.TryParse(TilesXTextBox.Text, out var columns) && columns > 0 ? columns : _tileColumns,
            TileRows = int.TryParse(TilesYTextBox.Text, out var rows) && rows > 0 ? rows : _tileRows,
            ObjectsPerTile = int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile) && objectsPerTile >= 0
                ? objectsPerTile
                : _objectsPerTile,
            AnnotationDisplayMode = DisplayModeComboBox.SelectedIndex,
            OutlineThickness = OutlineThicknessSlider.Value,
            LabelSize = LabelSizeSlider.Value,
            LabelDisplay = LabelDisplayComboBox.SelectedIndex,
            ShowLabels = ShowLabelsCheckBox.IsChecked ?? true,
            BackgroundNoise = _backgroundNoise,
            BackgroundCircleCount = _backgroundCircleCount
        };

        if (!settings.IsValid)
        {
            settings = settings with
            {
                TileColumns = _tileColumns,
                TileRows = _tileRows,
                ObjectsPerTile = _objectsPerTile
            };
        }

        try
        {
            CanvasUserSettingsStore.Save(_settingsPath, settings);
        }
        catch (System.IO.IOException exception)
        {
            Serilog.Log.Warning(exception, "Unable to save canvas settings to {Path}", _settingsPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            Serilog.Log.Warning(exception, "Unable to save canvas settings to {Path}", _settingsPath);
        }
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
        if (_tiles.Count > 0
            && TileGridIndexLookup.TryGetTileIndex(
                worldX,
                worldY,
                _sceneBounds,
                _tiles[0].Bounds.Width,
                _tiles[0].Bounds.Height,
                _tileColumns,
                _tiles.Count,
                out var tileIndex)
            && _tiles[tileIndex].TryGetPixelValue(worldX, worldY, out background))
        {
            defect = 0;
            var sampleArea = new SpatialBounds(worldX, worldY, 0.01, 0.01);
            var hitAnnotations = _spatialIndex.Query(sampleArea);
            for (var index = 0; index < hitAnnotations.Count; index++)
            {
                if (hitAnnotations[index].TryGetDefectValue(worldX, worldY, out var value))
                {
                    defect = Math.Max(defect, value);
                }
            }

            tileId = _tiles[tileIndex].Id;
            return true;
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

    private void BeginBusyOperation()
    {
        if (Interlocked.Increment(ref _busyOperationCount) == 1)
        {
            Dispatcher.Invoke(() => RenderBusyBar.Visibility = Visibility.Visible);
        }
    }

    private void EndBusyOperation()
    {
        if (Interlocked.Decrement(ref _busyOperationCount) <= 0)
        {
            Interlocked.Exchange(ref _busyOperationCount, 0);
            Dispatcher.Invoke(() => RenderBusyBar.Visibility = Visibility.Collapsed);
        }
    }

    private sealed record AnnotationDisplayOptions(
        AnnotationDisplayMode Mode,
        double OutlineThickness,
        double LabelSize,
        AnnotationLabelDisplay LabelDisplay,
        bool ShowLabels)
    {
        public static AnnotationDisplayOptions Default { get; } = new(
            AnnotationDisplayMode.Outline,
            2,
                8.5,
                AnnotationLabelDisplay.Class,
            true);
    }

            private enum AnnotationLabelDisplay
            {
            Class,
            Id
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
