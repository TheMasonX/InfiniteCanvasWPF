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
    private ZeroCopyBitmapFactory? _bitmapFactory;
    private Point? _lastPointerPosition;
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

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Generating 8 monochrome 8192x2048 inspection images";
            _tiles = await Task.Run(
                () => SampleImageGenerator.GenerateSet(),
                _lifetime.Token);

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
        var camera = _camera.Capture();
        var viewport = camera.GetViewportBounds(width, height);
        var stopwatch = Stopwatch.StartNew();

        var factory = EnsureBitmapFactory(width, height);
        var frame = await Task.Run(() =>
        {
            var visibleItems = _spatialIndex.Query(viewport);
            var bitmap = factory.GenerateFrozenBitmap(_tiles, visibleItems, camera);
            return (Bitmap: bitmap, VisibleItems: visibleItems);
        }, cancellationToken);

        CanvasImage.Source = frame.Bitmap;
        RenderAnnotationOverlay(frame.VisibleItems, camera);
        _viewModel.ApplyFrame(viewport, frame.VisibleItems.Count);

        stopwatch.Stop();
        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  Zoom {camera.ScaleX:F2}x";
    }

    private ZeroCopyBitmapFactory EnsureBitmapFactory(int width, int height)
    {
        if (_bitmapFactory is not null
            && _bitmapFactory.Width == width
            && _bitmapFactory.Height == height)
        {
            return _bitmapFactory;
        }

        CanvasImage.Source = null;
        _bitmapFactory?.Dispose();
        _bitmapFactory = new ZeroCopyBitmapFactory(width, height);
        return _bitmapFactory;
    }

    private void FitSceneToViewport()
    {
        var sceneRight = _tiles.Max(tile => tile.Bounds.Right);
        var sceneBottom = _tiles.Max(tile => tile.Bounds.Bottom);
        var scale = Math.Min(
            ViewportHost.ActualWidth / sceneRight,
            ViewportHost.ActualHeight / sceneBottom) * 0.92;

        if (_camera.Zoom(scale, new ScreenPoint(0, 0)))
        {
            _camera.Pan(
                (ViewportHost.ActualWidth - (sceneRight * scale)) / 2,
                (ViewportHost.ActualHeight - (sceneBottom * scale)) / 2);
        }
    }

    private void RenderAnnotationOverlay(IReadOnlyList<SampleAnnotation> annotations, CameraSnapshot camera)
    {
        AnnotationLayer.Children.Clear();

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
            AnnotationLayer.Children.Add(annotationElement);

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
        if (_lastPointerPosition is not Point previous || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(ViewportHost);
        _lastPointerPosition = current;
        _camera.Pan(current.X - previous.X, current.Y - previous.Y);
        await RequestRenderAsync();
    }

    private async void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var origin = e.GetPosition(ViewportHost);
        var scaleDelta = e.Delta > 0 ? 1.15 : 1 / 1.15;
        if (_camera.Zoom(scaleDelta, new ScreenPoint(origin.X, origin.Y)))
        {
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
        CanvasImage.Source = null;
        _bitmapFactory?.Dispose();
        _lifetime.Dispose();
    }
}
