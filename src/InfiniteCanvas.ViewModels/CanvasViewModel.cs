using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    public CameraTransform Camera { get; } = new();

    private SpatialBounds _sceneBounds;
    private SpatialBounds _viewport;
    private int _visibleItemCount;
    private int _totalItemCount;
    private IReadOnlyList<ICanvasItem> _visibleItems = [];

    // Frame state setters are private (ICW-316A). ApplyFrame is the only
    // mutation path, so the invariants hold by construction and no public
    // setter can produce VisibleItemCount > TotalItemCount or bypass HasScene.
    public SpatialBounds SceneBounds
    {
        get => _sceneBounds;
        private set
        {
            _sceneBounds = value;
            OnPropertyChanged();
        }
    }

    public SpatialBounds Viewport
    {
        get => _viewport;
        private set
        {
            _viewport = value;
            OnPropertyChanged();
        }
    }

    public int VisibleItemCount
    {
        get => _visibleItemCount;
        private set
        {
            _visibleItemCount = value;
            OnPropertyChanged();
        }
    }

    public int TotalItemCount
    {
        get => _totalItemCount;
        private set
        {
            _totalItemCount = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<ICanvasItem> VisibleItems
    {
        get => _visibleItems;
        private set
        {
            _visibleItems = value;
            OnPropertyChanged();
        }
    }

    public bool HasScene => SceneBounds.Width > 0 && SceneBounds.Height > 0;

    public void ResetCamera()
    {
        Camera.Reset();
        Viewport = default;
    }

    // The canvas component owns all per-frame viewport state. MainWindow
    // publishes the frame result through this method so the canvas view model
    // stays self-contained and independent of MainViewModel or any spatial
    // index service (ICW-309). The visible-items list is required (ICW-316A);
    // a host passes the query result from its ICanvasSceneSource (ICW-312
    // consumer-host gate; ICW-314 consumes VisibleItems).
    public void ApplyFrame(
        SpatialBounds frameViewport,
        int frameVisibleItemCount,
        int frameTotalItemCount,
        IReadOnlyList<ICanvasItem> frameVisibleItems)
    {
        ArgumentNullException.ThrowIfNull(frameVisibleItems);
        if (frameVisibleItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameVisibleItemCount));
        }

        if (frameTotalItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameTotalItemCount));
        }

        if (frameVisibleItemCount > frameTotalItemCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameVisibleItemCount),
                "VisibleItemCount cannot exceed TotalItemCount.");
        }

        if (frameVisibleItems.Count != frameVisibleItemCount)
        {
            throw new ArgumentException(
                "The visible-items list count must equal frameVisibleItemCount.",
                nameof(frameVisibleItems));
        }

        // Frame state publishes as one notification batch (ICW-316A): set all
        // backing fields first, then raise one notification per property so a
        // consumer never observes a half-applied frame.
        _viewport = frameViewport;
        _visibleItemCount = frameVisibleItemCount;
        _totalItemCount = frameTotalItemCount;
        _visibleItems = frameVisibleItems;
        OnPropertyChanged(nameof(Viewport));
        OnPropertyChanged(nameof(VisibleItemCount));
        OnPropertyChanged(nameof(TotalItemCount));
        OnPropertyChanged(nameof(VisibleItems));
    }

    public void SetSceneBounds(SpatialBounds bounds)
    {
        SceneBounds = bounds;
        OnPropertyChanged(nameof(HasScene));
    }

    public void ApplyViewportSize(double width, double height)
    {
        if (!HasScene || width < 1 || height < 1)
        {
            return;
        }

        Camera.ClampToBounds(SceneBounds, width, height);
        Viewport = Camera.GetViewportBounds(width, height);
    }

    public void Pan(double deltaX, double deltaY, double width, double height)
    {
        Camera.Pan(deltaX, deltaY);
        ApplyViewportSize(width, height);
    }

    public bool Zoom(double scaleDelta, ScreenPoint origin, double width, double height)
    {
        if (!Camera.Zoom(scaleDelta, origin))
        {
            return false;
        }

        ApplyViewportSize(width, height);
        return true;
    }

    // The canvas component owns the zoom-floor math so both the wheel handler
    // (CanvasControl) and the preset orchestration (MainWindow) share one
    // implementation (ICW-311).
    public (double ScaleX, double ScaleY) ComputeMinimumZoom(double viewportWidth, double viewportHeight)
    {
        return (viewportWidth / SceneBounds.Width, viewportHeight / SceneBounds.Height);
    }

    public void ApplyZoomFloor(double viewportWidth, double viewportHeight)
    {
        if (!HasScene)
        {
            return;
        }

        var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);
        var currentScaleX = Camera.ScaleX;
        var currentScaleY = Camera.ScaleY;
        if (currentScaleX >= minimumScaleX && currentScaleY >= minimumScaleY)
        {
            return;
        }

        var minimumUniform = Math.Max(minimumScaleX, minimumScaleY);
        if (Math.Abs(currentScaleX - currentScaleY) <= 0.0001)
        {
            var uniformDelta = minimumUniform / currentScaleX;
            Camera.Zoom(uniformDelta, uniformDelta, new ScreenPoint(viewportWidth / 2, viewportHeight / 2));
            return;
        }

        var origin = new ScreenPoint(viewportWidth / 2, viewportHeight / 2);
        var scaleXDelta = currentScaleX < minimumScaleX ? minimumScaleX / currentScaleX : 1;
        var scaleYDelta = currentScaleY < minimumScaleY ? minimumScaleY / currentScaleY : 1;
        Camera.Zoom(scaleXDelta, scaleYDelta, origin);
    }
}
