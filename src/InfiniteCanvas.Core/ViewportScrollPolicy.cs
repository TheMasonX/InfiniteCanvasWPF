namespace InfiniteCanvas.Core;

public static class ViewportScrollPolicy
{
    public static (double ContentWidth, double ContentHeight) ComputeContentSize(
        double viewportWidth,
        double viewportHeight,
        double sceneWidth,
        double sceneHeight,
        CameraSnapshot camera)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        }

        if (sceneWidth <= 0 || sceneHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneHeight));
        }

        var scaledSceneWidth = sceneWidth * camera.ScaleX;
        var scaledSceneHeight = sceneHeight * camera.ScaleY;
        var contentWidth = Math.Max(viewportWidth, scaledSceneWidth);
        var contentHeight = Math.Max(viewportHeight, scaledSceneHeight);

        return (contentWidth, contentHeight);
    }

    public static (double HorizontalOffset, double VerticalOffset) ComputeScrollOffsets(
        double viewportWidth,
        double viewportHeight,
        double contentWidth,
        double contentHeight,
        CameraSnapshot camera)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        }

        if (contentWidth < viewportWidth || contentHeight < viewportHeight)
        {
            return (0, 0);
        }

        var horizontalRange = contentWidth - viewportWidth;
        var verticalRange = contentHeight - viewportHeight;
        var normalizedHorizontal = Math.Clamp((-camera.OffsetX / camera.ScaleX) / (contentWidth / camera.ScaleX), 0, 1);
        var normalizedVertical = Math.Clamp((-camera.OffsetY / camera.ScaleY) / (contentHeight / camera.ScaleY), 0, 1);
        var horizontalOffset = horizontalRange * normalizedHorizontal;
        var verticalOffset = verticalRange * normalizedVertical;

        return (horizontalOffset, verticalOffset);
    }
}
