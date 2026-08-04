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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoomed, Is.True);
            Assert.That(screenPoint, Is.EqualTo(new ScreenPoint(30, 30)));
            Assert.That(viewport, Is.EqualTo(new SpatialBounds(-10, -5, 50, 40)));
        }
    }

    [Test]
    public void Zoom_AllowsWideDefaultScaleRange()
    {
        var camera = new CameraTransform();

        var zoomed = camera.Zoom(100, new ScreenPoint(0, 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoomed, Is.True);
            Assert.That(camera.WorldToScreen(10, 10), Is.EqualTo(new ScreenPoint(1000, 1000)));
        }
    }

    [Test]
    public void Zoom_SupportsClampedNonUniformScaling()
    {
        var camera = new CameraTransform();

        var zoomed = camera.Zoom(2, 4, new ScreenPoint(0, 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoomed, Is.True);
            Assert.That(camera.ScaleX, Is.EqualTo(2));
            Assert.That(camera.ScaleY, Is.EqualTo(4));
            Assert.That(camera.WorldToScreen(2, 2), Is.EqualTo(new ScreenPoint(4, 8)));
        }
    }

    [Test]
    public void Capture_RemainsStableAfterCameraChanges()
    {
        var camera = new CameraTransform();
        camera.Pan(20, 10);
        var snapshot = camera.Capture();

        camera.Pan(100, 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.WorldToScreen(5, 10), Is.EqualTo(new ScreenPoint(25, 20)));
            Assert.That(snapshot.GetViewportBounds(100, 80), Is.EqualTo(new SpatialBounds(-20, -10, 100, 80)));
        }
    }

    [Test]
    public void ClampToBounds_StopsAtEdgesAndCentersContentSmallerThanViewport()
    {
        var camera = new CameraTransform(0.001);
        var bounds = new SpatialBounds(0, 0, 100, 50);

        camera.Pan(500, 500);
        camera.ClampToBounds(bounds, 40, 30);
        var edgeViewport = camera.GetViewportBounds(40, 30);

        camera.Zoom(0.1, new ScreenPoint(0, 0));
        camera.ClampToBounds(bounds, 40, 30);
        var centeredViewport = camera.GetViewportBounds(40, 30);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edgeViewport.X, Is.EqualTo(0));
            Assert.That(edgeViewport.Y, Is.EqualTo(0));
            Assert.That(centeredViewport.X, Is.EqualTo(-150));
            Assert.That(centeredViewport.Y, Is.EqualTo(-125));
        }
    }
}
