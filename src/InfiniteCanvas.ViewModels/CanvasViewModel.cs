using CommunityToolkit.Mvvm.ComponentModel;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.ViewModels;

public partial class CanvasViewModel : ObservableObject
{
    public CameraTransform Camera { get; } = new();

    [ObservableProperty]
    private SpatialBounds sceneBounds;

    [ObservableProperty]
    private SpatialBounds viewport;

    [ObservableProperty]
    private int visibleItemCount;

    [ObservableProperty]
    private int totalItemCount;

    public bool HasScene => SceneBounds.Width > 0 && SceneBounds.Height > 0;

    public void ResetCamera()
    {
        Camera.Reset();
        Viewport = default;
    }

    // The canvas component owns all per-frame viewport state. MainWindow
    // publishes the frame result through this method so the canvas view model
    // stays self-contained and independent of MainViewModel or any spatial
    // index service (ICW-309).
    public void ApplyFrame(SpatialBounds frameViewport, int frameVisibleItemCount, int frameTotalItemCount)
    {
        Viewport = frameViewport;
        VisibleItemCount = frameVisibleItemCount;
        TotalItemCount = frameTotalItemCount;
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
}
