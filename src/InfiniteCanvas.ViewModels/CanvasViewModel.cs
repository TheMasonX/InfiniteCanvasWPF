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

    public bool HasScene => SceneBounds.Width > 0 && SceneBounds.Height > 0;

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
