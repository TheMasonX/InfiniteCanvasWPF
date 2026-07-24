using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace InfiniteCanvas.App;

public partial class MainWindow : Window
{
    private const int InitialItemCount = 100_000;
    private const int LiveBatchSize = 250;

    private readonly LiveSpatialIndexService<SpatialRecord<int>> _spatialIndex;
    private readonly CanvasViewportViewModel<SpatialRecord<int>> _viewModel;
    private readonly CameraTransform _camera = new();
    private readonly CoalescingAsyncAction _renderAction;
    private readonly DispatcherTimer _liveTimer;
    private readonly DispatcherTimer _resizeTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private ZeroCopyBitmapFactory? _bitmapFactory;
    private Point? _lastPointerPosition;
    private int _nextItemId = InitialItemCount;
    private int _liveTick;

    public MainWindow()
    {
        InitializeComponent();

        _spatialIndex = new LiveSpatialIndexService<SpatialRecord<int>>(
            new StrTreeSpatialIndexBuilder<SpatialRecord<int>>());
        _viewModel = new CanvasViewportViewModel<SpatialRecord<int>>(_spatialIndex);
        DataContext = _viewModel;
        _renderAction = new CoalescingAsyncAction(DispatchRenderFrameAsync);

        _liveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, OnLiveTick, Dispatcher);
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
            StatusText.Text = $"Generating {InitialItemCount:N0} deterministic points";
            var initialItems = await Task.Run(
                () => GenerateItems(0, InitialItemCount),
                _lifetime.Token);

            _spatialIndex.AddRange(initialItems);
            StatusText.Text = "Publishing initial STR snapshot";
            await _spatialIndex.PublishSnapshotAsync(_lifetime.Token);

            _camera.Zoom(0.4, new ScreenPoint(0, 0));
            _camera.Pan(40, 40);
            LoadingOverlay.Visibility = Visibility.Collapsed;
            await RequestRenderAsync();
            _liveTimer.Start();
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
        var viewport = _camera.GetViewportBounds(width, height);
        var stopwatch = Stopwatch.StartNew();

        var factory = EnsureBitmapFactory(width, height);
        var bitmap = await Task.Run(() =>
        {
            var visibleItems = _spatialIndex.Query(viewport);
            var screenPoints = visibleItems.Select(item =>
                _camera.WorldToScreen(
                    item.Bounds.X + (item.Bounds.Width / 2),
                    item.Bounds.Y + (item.Bounds.Height / 2)));

            return factory.GenerateFrozenBitmap(
                screenPoints,
                new Bgra32Color(186, 208, 53, 255));
        }, cancellationToken);

        CanvasImage.Source = bitmap;
        _viewModel.Viewport = viewport;
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        stopwatch.Stop();
        StatusText.Text = $"Frame {width}x{height}  |  {stopwatch.Elapsed.TotalMilliseconds:F1} ms  |  Zoom {_camera.ScaleX:F2}x";
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

    private async void OnLiveTick(object? sender, EventArgs e)
    {
        _liveTimer.Stop();
        try
        {
            _spatialIndex.AddRange(GenerateItems(_nextItemId, LiveBatchSize));
            _nextItemId += LiveBatchSize;
            _liveTick++;

            if (_liveTick % 4 == 0)
            {
                await _spatialIndex.PublishSnapshotAsync(_lifetime.Token);
            }

            await RequestRenderAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!_lifetime.IsCancellationRequested)
            {
                _liveTimer.Start();
            }
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
        _liveTimer.Stop();
        _resizeTimer.Stop();
        _lifetime.Cancel();

        await _renderAction.DisposeAsync();
        CanvasImage.Source = null;
        _bitmapFactory?.Dispose();
        _lifetime.Dispose();
    }

    private static SpatialRecord<int>[] GenerateItems(int startId, int count)
    {
        var items = new SpatialRecord<int>[count];
        for (var offset = 0; offset < count; offset++)
        {
            var id = startId + offset;
            var x = ((id * 7919L) % 2_900) + (Math.Sin(id * 0.071) * 80);
            var y = ((id * 3571L) % 1_700) + (Math.Cos(id * 0.053) * 55);
            items[offset] = new SpatialRecord<int>(
                id.ToString(),
                new SpatialBounds(x, y, 1, 1),
                id);
        }

        return items;
    }
}
