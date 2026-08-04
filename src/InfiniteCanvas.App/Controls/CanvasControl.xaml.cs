using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.App.Controls;

public partial class CanvasControl : UserControl
{
    private const double _mouseWheelZoomDelta = 1.2;

    private const double _panExponent = 1.8;
    private const double _panDeadZone = 1;
    private const double _panScale = 0.1;
    private const double _panGain = 0.075;

    private Point? _lastPointerPosition;
    private Point? _anchorPanOrigin;
    private Point _anchorPanPointer;
    private ViewportScrollbarAxis? _scrollbarDragAxis;
    private double _scrollbarDragPointerOffset;
    private readonly DispatcherTimer _anchorPanTimer;

    public CanvasControl()
    {
        InitializeComponent();
        ViewModel = new CanvasViewModel();
        DataContext = ViewModel;
        _anchorPanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), System.Windows.Threading.DispatcherPriority.Input, OnAnchorPanTick, Dispatcher)
        {
            IsEnabled = false
        };
    }

    public CanvasViewModel ViewModel { get; }

    public Border SurfaceHost => ViewportHost;
    public Viewbox FrameHost => FramePresenter;
    public TextBlock LoadingText => LoadingOverlay;
    public TextBlock WorldReadout => PixelometerWorldText;
    public TextBlock TileReadout => PixelometerTileText;
    public TextBlock ValueReadout => PixelometerValueText;
    public ProgressBar BusyBar => RenderBusyBar;

    public event EventHandler? ViewportChanged;
    public event MouseEventHandler? PointerMoved;
    public event MouseWheelEventHandler? PointerWheel;

    public void PublishFrame(UIElement frame)
    {
        FramePresenter.Child = frame;
    }

    public void ResetCamera()
    {
        ViewModel.ResetCamera();
        ApplyViewportState();
    }

    public void SetSceneBounds(SpatialBounds bounds)
    {
        ViewModel.SetSceneBounds(bounds);
        ApplyViewportState();
    }

    public void RefreshScrollbars()
    {
        UpdateViewportScrollbars();
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

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(ViewportHost);
        PointerMoved?.Invoke(this, e);
        if (_anchorPanOrigin is not null)
        {
            _anchorPanPointer = current;
        }

        if (_lastPointerPosition is not Point previous || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _lastPointerPosition = current;
        ViewModel.Pan(current.X - previous.X, current.Y - previous.Y, ViewportHost.ActualWidth, ViewportHost.ActualHeight);
        UpdateViewportScrollbars();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
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

    private void OnAnchorPanTick(object? sender, EventArgs e)
    {
        if (_anchorPanOrigin is not Point anchor)
        {
            return;
        }

        var deltaX = ApplyDeadZone((_anchorPanPointer.X - anchor.X) * _panScale, _panDeadZone);
        var deltaY = ApplyDeadZone((_anchorPanPointer.Y - anchor.Y) * _panScale, _panDeadZone);
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        ViewModel.Pan(-(deltaX * _panGain), -(deltaY * _panGain), ViewportHost.ActualWidth, ViewportHost.ActualHeight);
        UpdateViewportScrollbars();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    // Sign-preserving exponential curve (ICW-311). The dead-zone guard keeps
    // the magnitude positive, so the fractional exponent never sees a negative
    // base and cannot produce NaN.
    private static double ApplyDeadZone(double value, double deadZone)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= deadZone)
        {
            return 0;
        }

        return Math.Sign(value) * Math.Pow(magnitude - deadZone, _panExponent);
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

    private void OnScrollbarTrackMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border track || e.OriginalSource is Border { Name: "HorizontalScrollbarThumb" or "VerticalScrollbarThumb" })
        {
            return;
        }

        var axis = track == HorizontalScrollbarTrack ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        var thumb = axis == ViewportScrollbarAxis.Horizontal ? HorizontalScrollbarThumb : VerticalScrollbarThumb;
        var pointer = e.GetPosition(ViewportScrollbarOverlay);
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackPosition = axis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(track) : Canvas.GetTop(track);
        PanToScrollbarPosition(axis, (pointerPosition - trackPosition - (thumbLength / 2)) / Math.Max(1, trackLength - thumbLength));
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border thumb)
        {
            return;
        }

        _scrollbarDragAxis = thumb == HorizontalScrollbarThumb ? ViewportScrollbarAxis.Horizontal : ViewportScrollbarAxis.Vertical;
        var track = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? HorizontalScrollbarTrack : VerticalScrollbarTrack;
        var pointer = e.GetPosition(track);
        var thumbPosition = _scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? Canvas.GetLeft(thumb) : Canvas.GetTop(thumb);
        _scrollbarDragPointerOffset = (_scrollbarDragAxis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y) - thumbPosition;
        thumb.CaptureMouse();
        e.Handled = true;
    }

    private void OnScrollbarThumbMouseMove(object sender, MouseEventArgs e)
    {
        if (_scrollbarDragAxis is not ViewportScrollbarAxis axis || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var track = axis == ViewportScrollbarAxis.Horizontal ? HorizontalScrollbarTrack : VerticalScrollbarTrack;
        var thumb = axis == ViewportScrollbarAxis.Horizontal ? HorizontalScrollbarThumb : VerticalScrollbarThumb;
        var pointer = e.GetPosition(track);
        var pointerPosition = axis == ViewportScrollbarAxis.Horizontal ? pointer.X : pointer.Y;
        var trackLength = axis == ViewportScrollbarAxis.Horizontal ? track.ActualWidth : track.ActualHeight;
        var thumbLength = axis == ViewportScrollbarAxis.Horizontal ? thumb.ActualWidth : thumb.ActualHeight;
        PanToScrollbarPosition(axis, (pointerPosition - _scrollbarDragPointerOffset) / Math.Max(1, trackLength - thumbLength));
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

    private void PanToScrollbarPosition(ViewportScrollbarAxis axis, double targetPosition)
    {
        var delta = ViewportScrollbarPolicy.ComputePanDelta(
            ViewModel.Camera.Capture(),
            ViewModel.SceneBounds,
            Math.Max(1, ViewportHost.ActualWidth),
            Math.Max(1, ViewportHost.ActualHeight),
            axis,
            targetPosition);
        if (delta == 0)
        {
            return;
        }

        ViewModel.Pan(axis == ViewportScrollbarAxis.Horizontal ? delta : 0,
            axis == ViewportScrollbarAxis.Vertical ? delta : 0,
            ViewportHost.ActualWidth,
            ViewportHost.ActualHeight);
        UpdateViewportScrollbars();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateViewportScrollbars()
    {
        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);
        UpdateScrollbar(ViewportScrollbarAxis.Horizontal, ViewportScrollbarPolicy.ComputeMetrics(ViewModel.Camera.Capture(), ViewModel.SceneBounds, width, height, ViewportScrollbarAxis.Horizontal), HorizontalScrollbarTrack, HorizontalScrollbarThumb, 10, height - 20, Math.Max(0, width - 24), 24);
        UpdateScrollbar(ViewportScrollbarAxis.Vertical, ViewportScrollbarPolicy.ComputeMetrics(ViewModel.Camera.Capture(), ViewModel.SceneBounds, width, height, ViewportScrollbarAxis.Vertical), VerticalScrollbarTrack, VerticalScrollbarThumb, width - 20, 10, Math.Max(0, height - 24), 24);
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

    private void OnViewportMouseLeave(object sender, MouseEventArgs e)
    {
        PixelometerWorldText.Text = "WORLD X --  Y --";
        PixelometerTileText.Text = "TILE --";
        PixelometerValueText.Text = "PIXEL --";
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var origin = e.GetPosition(ViewportHost);
        var requestedScaleDelta = e.Delta > 0 ? _mouseWheelZoomDelta : 1 / _mouseWheelZoomDelta;

        var width = Math.Max(1, ViewportHost.ActualWidth);
        var height = Math.Max(1, ViewportHost.ActualHeight);

        if (ViewModel.HasScene)
        {
            var (minimumScaleX, minimumScaleY) = ViewModel.ComputeMinimumZoom(width, height);
            var zoomDeltas = ViewportZoomPolicy.ComputeWheelDeltas(
                ViewModel.Camera.ScaleX,
                ViewModel.Camera.ScaleY,
                minimumScaleX,
                minimumScaleY,
                requestedScaleDelta);
            if (zoomDeltas.HasChange
                && ViewModel.Camera.Zoom(zoomDeltas.ScaleX, zoomDeltas.ScaleY, new ScreenPoint(origin.X, origin.Y)))
            {
                ViewModel.ApplyZoomFloor(width, height);
                ViewModel.ApplyViewportSize(width, height);
                UpdateViewportScrollbars();
                ViewportChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Let the host observe the wheel position (for the pixelometer).
        PointerWheel?.Invoke(this, e);
        e.Handled = true;
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyViewportState();
    }

    private void ApplyViewportState()
    {
        ViewModel.ApplyViewportSize(ViewportHost.ActualWidth, ViewportHost.ActualHeight);
        UpdateViewportScrollbars();
    }
}
