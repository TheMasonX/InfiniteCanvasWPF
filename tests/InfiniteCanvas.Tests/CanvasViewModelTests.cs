using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;
using NUnit.Framework;

namespace InfiniteCanvas.Tests;

[TestFixture]
public sealed class CanvasViewModelTests
{
    [Test]
    public void ApplyViewportSize_TracksSceneViewport()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        viewModel.ApplyViewportSize(500, 250);

        Assert.That(viewModel.HasScene, Is.True);
        Assert.That(viewModel.Viewport, Is.EqualTo(new SpatialBounds(0, 0, 500, 250)));
    }

    [Test]
    public void Pan_UpdatesCameraAndClampsToScene()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));
        viewModel.ApplyViewportSize(500, 250);

        viewModel.Pan(-1000, 0, 500, 250);

        Assert.That(viewModel.Camera.Capture().OffsetX, Is.EqualTo(-500).Within(0.0001));
        Assert.That(viewModel.Viewport.X, Is.EqualTo(500).Within(0.0001));
    }

    [Test]
    public void ApplyFrame_UpdatesViewportAndVisibleAndTotalCounts()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        viewModel.ApplyFrame(new SpatialBounds(100, 50, 400, 200), 7, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Viewport, Is.EqualTo(new SpatialBounds(100, 50, 400, 200)));
            Assert.That(viewModel.VisibleItemCount, Is.EqualTo(7));
            Assert.That(viewModel.TotalItemCount, Is.EqualTo(42));
        }
    }

    [Test]
    public void ComputeMinimumZoom_ReturnsViewportOverSceneRatios()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        var (scaleX, scaleY) = viewModel.ComputeMinimumZoom(500, 250);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scaleX, Is.EqualTo(0.5));
            Assert.That(scaleY, Is.EqualTo(0.5));
        }
    }

    [Test]
    public void ApplyZoomFloor_ScalesUpCameraBelowMinimum()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));
        viewModel.Camera.Zoom(0.1, new ScreenPoint(0, 0));

        viewModel.ApplyZoomFloor(500, 250);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Camera.ScaleX, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(viewModel.Camera.ScaleY, Is.EqualTo(0.5).Within(0.0001));
        }
    }

    [Test]
    public void ApplyZoomFloor_LeavesCameraAboveMinimum()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));
        viewModel.Camera.Zoom(2, new ScreenPoint(0, 0));

        viewModel.ApplyZoomFloor(500, 250);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Camera.ScaleX, Is.EqualTo(2));
            Assert.That(viewModel.Camera.ScaleY, Is.EqualTo(2));
        }
    }
}
