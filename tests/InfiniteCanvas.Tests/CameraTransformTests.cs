using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class CameraTransformTests
{
    [Test]
    public void PanAndZoom_ProjectWorldCoordinatesAndViewportBounds()
    {
        var camera = new CameraTransform();
        camera.Pan(20, 10);

        var zoomed = camera.Zoom(2, new ScreenPoint(20, 10));
        var screenPoint = camera.WorldToScreen(5, 10);
        var viewport = camera.GetViewportBounds(100, 80);

        Assert.Multiple(() =>
        {
            Assert.That(zoomed, Is.True);
            Assert.That(screenPoint, Is.EqualTo(new ScreenPoint(30, 30)));
            Assert.That(viewport, Is.EqualTo(new SpatialBounds(-10, -5, 50, 40)));
        });
    }

    [Test]
    public void Zoom_RejectsScalesOutsideConfiguredBounds()
    {
        var camera = new CameraTransform();

        var zoomed = camera.Zoom(100, new ScreenPoint(0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(zoomed, Is.False);
            Assert.That(camera.WorldToScreen(10, 10), Is.EqualTo(new ScreenPoint(10, 10)));
        });
    }

    [Test]
    public void Zoom_SupportsClampedNonUniformScaling()
    {
        var camera = new CameraTransform();

        var zoomed = camera.Zoom(2, 4, new ScreenPoint(0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(zoomed, Is.True);
            Assert.That(camera.ScaleX, Is.EqualTo(2));
            Assert.That(camera.ScaleY, Is.EqualTo(4));
            Assert.That(camera.WorldToScreen(2, 2), Is.EqualTo(new ScreenPoint(4, 8)));
        });
    }
}
