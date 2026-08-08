using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Controls;

public partial class CanvasControl : UserControl
{
    private const double _mouseWheelZoomDelta = 1.2;

    private const double _panExponent = 1.8;
    private const double _panDeadZone = 1;
    private const double _panScale = 0.1;
    private const double _panGain = 0.075;

    private Point? _lastPointerPosition;
    private bool _pointerMovedDuringDrag;
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
        Unloaded += OnControlUnloaded;
    }

    public CanvasViewModel ViewModel { get; }

    /// <summary>
    /// Injected scene content source (ICW-312, ADR-0007). The host supplies
    /// this dependency property so the control never touches a generic
    /// spatial index or an application data type. It is the single item-query
    /// authority (ICW-316A F-001); the parameterless constructor stays for
    /// XAML and designer support.
    /// </summary>
    public static readonly DependencyProperty SceneSourceProperty = DependencyProperty.Register(
        nameof(SceneSource),
        typeof(ICanvasSceneSource),
        typeof(CanvasControl),
        new PropertyMetadata(null));

    public ICanvasSceneSource? SceneSource
    {
        get => (ICanvasSceneSource?)GetValue(SceneSourceProperty);
        set => SetValue(SceneSourceProperty, value);
    }

    // Method-based public surface (ICW-319). No raw element or overlay canvas
    // escapes the control; the host composes visuals through explicit methods.
    // This is the library public face for the ICW-316 extraction.

    /// <summary>
    /// Sets the loading overlay text and visibility. The overlay is centered
    /// by layout, so no host-side position is required (ICW-319).
    /// </summary>
    public void SetLoadingState(string? text, bool visible)
    {
        LoadingOverlay.Text = text ?? "BUILDING INITIAL SNAPSHOT";
        LoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _loadingVisible = visible;
    }

    /// <summary>True while the loading overlay is visible.</summary>
    public bool IsLoadingVisible => _loadingVisible;

    /// <summary>Shows or hides the indeterminate busy indicator.</summary>
    public void SetBusyIndicatorVisible(bool visible)
    {
        RenderBusyBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Sets the pixelometer readout lines in one call.</summary>
    public void SetPixelometerReadout(string world, string tile, string value)
    {
        PixelometerWorldText.Text = world;
        PixelometerTileText.Text = tile;
        PixelometerValueText.Text = value;
    }

    /// <summary>Clears the host-composed overlays and resets the readouts.</summary>
    public void ClearFrame()
    {
        ClearRegisteredItemVisuals();
        _tooltipCache.Clear();
        _tileGridLayer?.Children.Clear();
        _annotationLayer?.Children.Clear();
        SetPixelometerReadout("WORLD X --  Y --", "TILE --", "PIXEL --");
    }

    public void RegisterItemVisual(FrameworkElement visual, string? tooltipContent)
    {
        ArgumentNullException.ThrowIfNull(visual);
        DeferredCanvasToolTip? tooltip = null;
        if (tooltipContent is null)
        {
            visual.ToolTip = null;
            _tooltipCache.Remove(visual);
        }
        else if (!_tooltipCache.TryGetValue(visual, out var cachedTooltip)
            || !String.Equals(cachedTooltip.Content, tooltipContent, StringComparison.Ordinal))
        {
            tooltip = new DeferredCanvasToolTip(tooltipContent);
            _tooltipCache[visual] = tooltip;
        }
        else
        {
            tooltip = cachedTooltip;
        }

        visual.ToolTip = tooltip;
        _registeredItemVisuals.Add(visual);
    }

    public void UnregisterItemVisual(FrameworkElement visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        visual.ToolTip = null;
        _registeredItemVisuals.Remove(visual);
        _tooltipCache.Remove(visual);
    }

    private void ClearRegisteredItemVisuals()
    {
        foreach (var visual in _registeredItemVisuals)
        {
            visual.ToolTip = null;
        }

        _registeredItemVisuals.Clear();
    }

    /// <summary>Applies a viewport size to the camera view model.</summary>
    public void SetViewportSize(double width, double height)
    {
        ViewModel.ApplyViewportSize(width, height);
        UpdateViewportScrollbars();
    }

    /// <summary>Converts a mouse event position to viewport coordinates.</summary>
    public Point GetViewportPointer(MouseEventArgs e) => e.GetPosition(ViewportHost);

    /// <summary>Current viewport size in device-independent units.</summary>
    public Size GetViewportSize() => new(ViewportHost.ActualWidth, ViewportHost.ActualHeight);

    /// <summary>
    /// Internal overlay host for host-side composition. Internal so the raw
    /// canvases never become library API (ICW-319); ICW-316 owns whether
    /// overlay composition moves into the library.
    /// </summary>
    internal CanvasOverlayHost GetOverlayHost() => new(_tileGridLayer, _annotationLayer);

    // Persistent frame shell (ICW-317 pattern, owned by the control since
    // ICW-315). The shell attaches to the Viewbox once; each frame only swaps
    // Image.Source, so the visible frame has no teardown gap to flash black.
    private Grid? _frameShell;
    private Image? _frameImage;
    private Canvas? _tileGridLayer;
    private Canvas? _annotationLayer;
    private readonly HashSet<FrameworkElement> _registeredItemVisuals = [];
    private readonly Dictionary<FrameworkElement, DeferredCanvasToolTip> _tooltipCache = [];
    private bool _rasterVisible = true;
    private bool _loadingVisible;

    /// <summary>
    /// Highest frame revision displayed so far (ICW-328). Starts at the
    /// minimum so a host's first frame, even with the default revision of
    /// zero, is accepted.
    /// </summary>
    private int _lastPublishedRevision = int.MinValue;
    private CanvasFrameIdentity? _lastPublishedIdentity;

    /// <summary>
    /// Show or hide the raster Image element without rebuilding the shell.
    /// The host drives this from its layer-visibility settings.
    /// </summary>
    public bool RasterVisible
    {
        get => _rasterVisible;
        set
        {
            _rasterVisible = value;
            if (_frameImage is not null)
            {
                _frameImage.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    public event EventHandler? ViewportChanged;
    public event MouseEventHandler? PointerMoved;
    public event MouseWheelEventHandler? PointerWheel;
    public event EventHandler<CanvasFrame>? FrameLayersPublishing;
    public event EventHandler<CanvasFrame>? FramePublished;
    public event EventHandler<CanvasSelectionChangedEventArgs>? SelectionChanged;

    public ICanvasItem? SelectedItem { get; private set; }

    /// <summary>
    /// Publishes a rendered frame across the canvas boundary (ICW-315,
    /// ADR-0007). The canvas displays the frozen raster and applies the frame
    /// state to its view model. It never touches the raster's backing memory
    /// section, so the zero-copy handoff stays intact. The host composes its
    /// layers through <see cref="FrameLayersPublishing"/>.
    /// </summary>
    public void PublishFrame(CanvasFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // Stale-frame guard (ICW-328): the host may race render requests and
        // publish an out-of-order frame. Revision is the host render-request
        // version; a frame older than the last one displayed is stale and must
        // not overwrite newer frame state. Equal revisions are accepted as an
        // idempotent republish of the same frame.
        if (_lastPublishedIdentity is { } previousIdentity
            && string.Equals(
                frame.Identity.SourceSessionId,
                previousIdentity.SourceSessionId,
                StringComparison.Ordinal)
            && (frame.Revision < _lastPublishedRevision
                || !frame.Identity.CanReplace(previousIdentity)))
        {
            return;
        }

        _lastPublishedRevision = frame.Revision;
        _lastPublishedIdentity = frame.Identity;
        ClearRegisteredItemVisuals();
        EnsureFrameShell();
        _frameShell!.Width = frame.Width;
        _frameShell.Height = frame.Height;
        // The host composes app-specific layers inside the same accepted
        // frame boundary, before the raster and view-model state change.
        FrameLayersPublishing?.Invoke(this, frame);
        if (_frameImage is not null)
        {
            _frameImage.Visibility = _rasterVisible ? Visibility.Visible : Visibility.Collapsed;
            _frameImage.Source = frame.Raster;
        }

        ViewModel.ApplyFrame(frame.Viewport, frame.VisibleItemCount, frame.TotalItemCount, frame.Items);
        FramePublished?.Invoke(this, frame);
    }

    /// <summary>
    /// Attaches the persistent frame shell to the Viewbox exactly once. The
    /// shell holds the raster Image plus host-composed overlay canvases. It
    /// is never replaced per frame (ICW-317 no-flash invariant).
    /// </summary>
    private void EnsureFrameShell()
    {
        if (_frameShell is not null)
        {
            return;
        }

        var shell = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var image = new Image
        {
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true
        };
        var tileGridLayer = new Canvas
        {
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        var annotationLayer = new Canvas
        {
            ClipToBounds = true
        };
        shell.Children.Add(image);
        shell.Children.Add(tileGridLayer);
        shell.Children.Add(annotationLayer);
        FramePresenter.Child = shell;

        _frameShell = shell;
        _frameImage = image;
        _tileGridLayer = tileGridLayer;
        _annotationLayer = annotationLayer;
    }

    /// <summary>Detaches the frame shell, for example on host shutdown.</summary>
    public void DetachFrameShell()
    {
        ClearRegisteredItemVisuals();
        _tooltipCache.Clear();
        FramePresenter.Child = null;
        _frameShell = null;
        _frameImage = null;
        _tileGridLayer = null;
        _annotationLayer = null;
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

    /// <summary>
    /// Releases every interaction resource the control owns when it leaves the
    /// visual tree (ICW-316A): the anchor-pan timer, mouse capture, override
    /// cursor, pointer state, and any scrollbar drag. A host can detach and
    /// re-attach the control without a stuck timer or captured mouse.
    /// </summary>
    private void OnControlUnloaded(object? sender, RoutedEventArgs e)
    {
        StopAnchorPan();
        _lastPointerPosition = null;
        _scrollbarDragAxis = null;
    }

    private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        _lastPointerPosition = e.GetPosition(ViewportHost);
        _pointerMovedDuringDrag = false;
        ViewportHost.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_anchorPanOrigin is not null)
        {
            return;
        }

        var pointerPosition = e.GetPosition(ViewportHost);
        if (!_pointerMovedDuringDrag && _lastPointerPosition is not null)
        {
            SelectAtViewportPoint(pointerPosition);
        }

        _lastPointerPosition = null;
        _pointerMovedDuringDrag = false;
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
        if (Math.Abs(current.X - previous.X) > 2 || Math.Abs(current.Y - previous.Y) > 2)
        {
            _pointerMovedDuringDrag = true;
        }
        ViewModel.Pan(current.X - previous.X, current.Y - previous.Y, ViewportHost.ActualWidth, ViewportHost.ActualHeight);
        UpdateViewportScrollbars();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAtViewportPoint(Point viewportPoint)
    {
        if (SceneSource is null
            || !double.IsFinite(viewportPoint.X)
            || !double.IsFinite(viewportPoint.Y)
            || !double.IsFinite(ViewModel.Camera.ScaleX)
            || !double.IsFinite(ViewModel.Camera.ScaleY)
            || ViewModel.Camera.ScaleX == 0
            || ViewModel.Camera.ScaleY == 0)
        {
            return;
        }

        var camera = ViewModel.Camera.Capture();
        var worldX = (viewportPoint.X - camera.OffsetX) / camera.ScaleX;
        var worldY = (viewportPoint.Y - camera.OffsetY) / camera.ScaleY;
        var selected = SceneSource.QueryPoint(worldX, worldY)
            .FirstOrDefault(item => CanvasItemHitTesting.Contains(item, worldX, worldY));
        if (ReferenceEquals(SelectedItem, selected))
        {
            return;
        }

        SelectedItem = selected;
        SelectionChanged?.Invoke(this, new CanvasSelectionChangedEventArgs(selected));
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
        SetViewportSize(ViewportHost.ActualWidth, ViewportHost.ActualHeight);
    }
}

/// <summary>
/// Internal overlay surface of <see cref="CanvasControl"/> for host-side
/// composition (ICW-319). The raw canvases never appear on the public control
/// surface.
/// </summary>
internal sealed class CanvasOverlayHost
{
    public CanvasOverlayHost(Canvas? tileGridLayer, Canvas? annotationLayer)
    {
        TileGridLayer = tileGridLayer;
        AnnotationLayer = annotationLayer;
    }

    public Canvas? TileGridLayer { get; }

    public Canvas? AnnotationLayer { get; }
}
