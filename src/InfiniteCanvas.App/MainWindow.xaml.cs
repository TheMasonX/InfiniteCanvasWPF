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
using System.Windows.Threading;

namespace InfiniteCanvas.App;

public partial class MainWindow : Window
{
    private readonly LiveSpatialIndexService<SampleAnnotation> _spatialIndex;
    private readonly CanvasViewportViewModel<SampleAnnotation> _viewModel;
    private readonly CameraTransform _camera = new(0.01, 50);
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _resizeTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<SampleImageTile> _tiles = [];
    private SpatialBounds _sceneBounds;
    private ZeroCopyBitmapFactory? _frontBitmapFactory;
    private ZeroCopyBitmapFactory? _backBitmapFactory;
    private Point? _lastPointerPosition;
    private Point? _hoverPointerPosition;
    private string? _selectedAnnotationId;

    public MainWindow()
    {
        InitializeComponent();

        _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(
            new StrTreeSpatialIndexBuilder<SampleAnnotation>());
        _viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);
        DataContext = _viewModel;
        _renderAction = new CoalescingAsyncAction(DispatchRenderFrameAsync);

        _resizeTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, OnResizeElapsed, Dispatcher)
        {
            IsEnabled = false
        };

        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerValueText.Text = "PIXEL --";

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Generating metadata for 32 inspection tiles";
            _tiles = await Task.Run(
                () => SampleImageGenerator.GenerateSet(),
                _lifetime.Token);
            _sceneBounds = GetSceneBounds(_tiles);

            var annotations = _tiles.SelectMany(tile => tile.Annotations).ToArray();
            _spatialIndex.AddRange(annotations);
            StatusText.Text = $"Publishing {annotations.Length:N0} classified annotations";
            await _spatialIndex.PublishSnapshotAsync(_lifetime.Token);

            FitSceneToViewport();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await RequestRenderAsync();
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
        var generatedTileCount = _tiles.Count(tile => tile.IsImageGenerated);
        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  Zoom {camera.ScaleX:F2}x  |  Images {generatedTileCount}/32";

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

    private void FitSceneToViewport()
    {
        var scale = (ViewportHost.ActualHeight / _sceneBounds.Height) * 0.92;

        if (_camera.Zoom(scale, new ScreenPoint(0, 0)))
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

            var color = Color.FromArgb(
                annotation.Color.Alpha,
                annotation.Color.Red,
                annotation.Color.Green,
                annotation.Color.Blue);
            var borderBrush = new SolidColorBrush(color);
            var label = new TextBlock
            {
                Text = annotation.Id,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cascadia Mono"),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var annotationElement = new Border
            {
                Width = width,
                Height = height,
                BorderThickness = new Thickness(2),
                BorderBrush = borderBrush,
                Background = Brushes.Transparent,
                Child = new Viewbox { Child = label, Margin = new Thickness(3) },
                Tag = annotation,
                ToolTip = CreateAnnotationToolTip(annotation)
            };
            annotationElement.MouseLeftButtonDown += OnAnnotationMouseLeftButtonDown;
            Canvas.SetLeft(annotationElement, topLeft.X);
            Canvas.SetTop(annotationElement, topLeft.Y);
            annotationLayer.Children.Add(annotationElement);

            if (annotation.Id == _selectedAnnotationId)
            {
                var animation = new ColorAnimation(
                    color,
                    Colors.White,
                    TimeSpan.FromMilliseconds(450))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
        }

        return frame;
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
        _camera.ClampToBounds(_sceneBounds, width, height);
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
        _lastPointerPosition = e.GetPosition(ViewportHost);
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _lastPointerPosition = null;
        ViewportHost.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private async void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(ViewportHost);
        _hoverPointerPosition = current;
        UpdatePixelometer(current);

        if (_lastPointerPosition is not Point previous || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _lastPointerPosition = current;
        _camera.Pan(current.X - previous.X, current.Y - previous.Y);
        ClampCameraToScene();
        await RequestRenderAsync();
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
        var scaleDelta = e.Delta > 0 ? 1.15 : 1 / 1.15;
        if (_camera.Zoom(scaleDelta, new ScreenPoint(origin.X, origin.Y)))
        {
            ClampCameraToScene();
            await RequestRenderAsync();
        }
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

    private async void OnClosed(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        FramePresenter.Child = null;
        _frontBitmapFactory?.Dispose();
        _backBitmapFactory?.Dispose();
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

        if (TryReadPixelValue(worldX, worldY, out var pixelValue, out var tileId))
        {
            PixelometerValueText.Text = $"PIXEL {pixelValue}  ({tileId})";
            return;
        }

        PixelometerValueText.Text = "PIXEL --";
    }

    private bool TryReadPixelValue(double worldX, double worldY, out byte value, out string tileId)
    {
        foreach (var tile in _tiles)
        {
            if (tile.TryGetPixelValue(worldX, worldY, out value))
            {
                tileId = tile.Id;
                return true;
            }
        }

        value = default;
        tileId = string.Empty;
        return false;
    }
}
