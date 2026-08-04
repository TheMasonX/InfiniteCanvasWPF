using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    public CameraTransform Camera { get; } = new();

    [ObservableProperty]
    public partial SpatialBounds SceneBounds { get; set; }

    [ObservableProperty]
    public partial SpatialBounds Viewport { get; set; }

    [ObservableProperty]
    public partial int VisibleItemCount { get; set; }

    [ObservableProperty]
    public partial int TotalItemCount { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ICanvasItem> VisibleItems { get; set; } = [];

    public bool HasScene => SceneBounds.Width > 0 && SceneBounds.Height > 0;

    public void ResetCamera()
    {
        Camera.Reset();
        Viewport = default;
    }

    // The canvas component owns all per-frame viewport state. MainWindow
    // publishes the frame result through this method so the canvas view model
    // stays self-contained and independent of MainViewModel or any spatial
    // index service (ICW-309). The visible item list is optional so hosts can
    // drive the view model from any ICanvasSceneSource without app types
    // (ICW-312 consumer-host gate; ICW-314 consumes VisibleItems).
    public void ApplyFrame(
        SpatialBounds frameViewport,
        int frameVisibleItemCount,
        int frameTotalItemCount,
        IReadOnlyList<ICanvasItem>? frameVisibleItems = null)
    {
        Viewport = frameViewport;
        VisibleItemCount = frameVisibleItemCount;
        TotalItemCount = frameTotalItemCount;
        VisibleItems = frameVisibleItems ?? [];
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
