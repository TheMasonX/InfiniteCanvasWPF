namespace InfiniteCanvas.Core;

public enum ViewportScrollbarAxis
{
    Horizontal,
    Vertical
}

public readonly record struct ViewportScrollbarMetrics(
    bool IsScrollable,
    double ViewportFraction,
    double PositionFraction)
{
    public static ViewportScrollbarMetrics NotScrollable { get; } = new(false, 1, 0);
}

public static class ViewportScrollbarPolicy
{
    public static ViewportScrollbarMetrics ComputeMetrics(
        CameraSnapshot camera,
        SpatialBounds sceneBounds,
        double viewportWidth,
        double viewportHeight,
        ViewportScrollbarAxis axis)
    {
        ValidateViewport(viewportWidth, viewportHeight);

        var viewport = camera.GetViewportBounds(viewportWidth, viewportHeight);
        var sceneStart = axis == ViewportScrollbarAxis.Horizontal ? sceneBounds.X : sceneBounds.Y;
        var sceneLength = axis == ViewportScrollbarAxis.Horizontal ? sceneBounds.Width : sceneBounds.Height;
        var viewportStart = axis == ViewportScrollbarAxis.Horizontal ? viewport.X : viewport.Y;
        var viewportLength = axis == ViewportScrollbarAxis.Horizontal ? viewport.Width : viewport.Height;

        if (sceneLength <= 0 || viewportLength >= sceneLength)
        {
            return ViewportScrollbarMetrics.NotScrollable;
        }

        var availableStartRange = sceneLength - viewportLength;
        var position = Math.Clamp((viewportStart - sceneStart) / availableStartRange, 0, 1);
        return new ViewportScrollbarMetrics(true, viewportLength / sceneLength, position);
    }

    public static double ComputePanDelta(
        CameraSnapshot camera,
        SpatialBounds sceneBounds,
        double viewportWidth,
        double viewportHeight,
        ViewportScrollbarAxis axis,
        double targetPositionFraction)
    {
        ValidateViewport(viewportWidth, viewportHeight);
        if (!double.IsFinite(targetPositionFraction))
        {
            throw new ArgumentOutOfRangeException(nameof(targetPositionFraction));
        }

        var viewport = camera.GetViewportBounds(viewportWidth, viewportHeight);
        var sceneStart = axis == ViewportScrollbarAxis.Horizontal ? sceneBounds.X : sceneBounds.Y;
        var sceneLength = axis == ViewportScrollbarAxis.Horizontal ? sceneBounds.Width : sceneBounds.Height;
        var viewportLength = axis == ViewportScrollbarAxis.Horizontal ? viewport.Width : viewport.Height;
        var scale = axis == ViewportScrollbarAxis.Horizontal ? camera.ScaleX : camera.ScaleY;
        var currentOffset = axis == ViewportScrollbarAxis.Horizontal ? camera.OffsetX : camera.OffsetY;

        if (sceneLength <= 0 || viewportLength >= sceneLength)
        {
            return 0;
        }

        var targetWorldStart = sceneStart + ((sceneLength - viewportLength) * Math.Clamp(targetPositionFraction, 0, 1));
        var targetOffset = -targetWorldStart * scale;
        return targetOffset - currentOffset;
    }

    private static void ValidateViewport(double viewportWidth, double viewportHeight)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        }

        if (!double.IsFinite(viewportHeight) || viewportHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        }
    }
}