using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class ViewportScrollbarPolicyTests
{
    private static readonly SpatialBounds Scene = new(0, 0, 1_000, 500);

    [Test]
    public void ComputeMetrics_UsesVisibleWorldFractionAndCameraPosition()
    {
        var camera = new CameraTransform();
        camera.Zoom(2, new ScreenPoint(0, 0));
        camera.Pan(-500, -200);

        var horizontal = ViewportScrollbarPolicy.ComputeMetrics(
            camera.Capture(), Scene, 400, 200, ViewportScrollbarAxis.Horizontal);
        var vertical = ViewportScrollbarPolicy.ComputeMetrics(
            camera.Capture(), Scene, 400, 200, ViewportScrollbarAxis.Vertical);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(horizontal.IsScrollable, Is.True);
            Assert.That(horizontal.ViewportFraction, Is.EqualTo(0.2));
            Assert.That(horizontal.PositionFraction, Is.EqualTo(0.3125));
            Assert.That(vertical.ViewportFraction, Is.EqualTo(0.2));
            Assert.That(vertical.PositionFraction, Is.EqualTo(0.25));
        }
    }

    [Test]
    public void ComputeMetrics_ReturnsNonScrollableWhenViewportCoversScene()
    {
        var metrics = ViewportScrollbarPolicy.ComputeMetrics(
            new CameraTransform().Capture(), Scene, 1_000, 500, ViewportScrollbarAxis.Horizontal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.IsScrollable, Is.False);
            Assert.That(metrics.ViewportFraction, Is.EqualTo(1));
            Assert.That(metrics.PositionFraction, Is.EqualTo(0));
        }
    }

    [Test]
    public void ComputePanDelta_MapsThumbTargetToCameraOffset()
    {
        var camera = new CameraTransform();
        camera.Zoom(2, new ScreenPoint(0, 0));

        var panDelta = ViewportScrollbarPolicy.ComputePanDelta(
            camera.Capture(), Scene, 400, 200, ViewportScrollbarAxis.Horizontal, 1);

        Assert.That(panDelta, Is.EqualTo(-1_600));
    }
}