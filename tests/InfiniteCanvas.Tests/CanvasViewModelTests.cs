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

        viewModel.ApplyFrame(new SpatialBounds(100, 50, 400, 200), 7, 42, CreateItems(7));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Viewport, Is.EqualTo(new SpatialBounds(100, 50, 400, 200)));
            Assert.That(viewModel.VisibleItemCount, Is.EqualTo(7));
            Assert.That(viewModel.TotalItemCount, Is.EqualTo(42));
            Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(7));
        }
    }

    [Test]
    public void ApplyFrame_ThrowsWhenVisibleCountExceedsTotal()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        Assert.That(
            () => viewModel.ApplyFrame(new SpatialBounds(0, 0, 100, 100), 5, 4, CreateItems(5)),
            Throws.TypeOf<ArgumentOutOfRangeException>(),
            "VisibleItemCount must never exceed TotalItemCount (ICW-316A).");
    }

    [Test]
    public void ApplyFrame_ThrowsWhenItemsCountMismatchesVisibleCount()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        Assert.That(
            () => viewModel.ApplyFrame(new SpatialBounds(0, 0, 100, 100), 7, 42, CreateItems(3)),
            Throws.TypeOf<ArgumentException>(),
            "The items list must be exactly the visible set (ICW-316A).");
    }

    [Test]
    public void ApplyFrame_RequiresNonNullItemsList()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 1000, 500));

        Assert.That(
            () => viewModel.ApplyFrame(new SpatialBounds(0, 0, 100, 100), 0, 0, null!),
            Throws.TypeOf<ArgumentNullException>(),
            "ApplyFrame must require a visible-items list (ICW-316A).");
    }

    [Test]
    public void SetSceneBounds_IsTheOnlySceneBoundsMutationPath()
    {
        var viewModel = new CanvasViewModel();

        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 10, 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.HasScene, Is.True, "HasScene must reflect the scene bounds set through SetSceneBounds.");
            Assert.That(viewModel.SceneBounds, Is.EqualTo(new SpatialBounds(0, 0, 10, 10)));
        }
    }

    private static IReadOnlyList<ICanvasItem> CreateItems(int count)
    {
        var items = new ICanvasItem[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = new FakeCanvasItem($"item-{i}", new SpatialBounds(i, 0, 1, 1));
        }

        return items;
    }

    private sealed class FakeCanvasItem : ICanvasItem
    {
        public FakeCanvasItem(string id, SpatialBounds bounds)
        {
            Id = id;
            Bounds = bounds;
        }

        public string Id { get; }

        public SpatialBounds Bounds { get; }
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
