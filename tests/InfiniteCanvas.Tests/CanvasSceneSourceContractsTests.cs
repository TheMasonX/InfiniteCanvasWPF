using InfiniteCanvas.Core;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Tests;

/// <summary>
/// Consumer-host gate for ICW-312. This fixture drives CanvasViewModel from
/// fake sources and references no application type (no SampleAnnotation, no
/// spatial index, no rendering types). It proves the source contracts are
/// sufficient to drive the canvas view model.
/// </summary>
[TestFixture]
public sealed class CanvasSceneSourceContractsTests
{
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

    private sealed class FakeSceneSource : ICanvasSceneSource
    {
        private readonly IReadOnlyList<ICanvasItem> _items;

        public FakeSceneSource(SpatialBounds sceneBounds, IReadOnlyList<ICanvasItem> items)
        {
            SceneBounds = sceneBounds;
            TotalItemCount = items.Count;
            _items = items;
        }

        public SpatialBounds SceneBounds { get; }

        public int TotalItemCount { get; }

        public IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport)
        {
            return _items.Where(item => item.Bounds.Intersects(viewport)).ToArray();
        }

        public bool TryReadResidentPixel(double worldX, double worldY, int mipLevel, out CanvasPixelSample sample)
        {
            sample = default;
            return false;
        }

        public event EventHandler? SceneChanged;

        public void RaiseSceneChanged()
        {
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Test]
    public void HostLoop_DrivesViewModelFromFakeSceneSource_WithoutAppTypes()
    {
        var sceneBounds = new SpatialBounds(0, 0, 1000, 500);
        ICanvasItem[] items =
        [
            new FakeCanvasItem("a", new SpatialBounds(0, 0, 100, 100)),
            new FakeCanvasItem("b", new SpatialBounds(800, 400, 100, 100))
        ];
        var source = new FakeSceneSource(sceneBounds, items);
        var viewport = new SpatialBounds(0, 0, 500, 250);

        // Host loop: query the source for the viewport, then push the result
        // into the passive view model.
        var visible = source.QueryVisible(viewport);
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(source.SceneBounds);
        viewModel.ApplyFrame(viewport, visible.Count, source.TotalItemCount, visible);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Viewport, Is.EqualTo(viewport));
            Assert.That(viewModel.VisibleItemCount, Is.EqualTo(1));
            Assert.That(viewModel.TotalItemCount, Is.EqualTo(2));
            Assert.That(viewModel.VisibleItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.VisibleItems[0].Id, Is.EqualTo("a"));
        }
    }

    [Test]
    public void QueryVisible_UsesHalfOpenIntersection_OnSharedEdges()
    {
        ICanvasItem[] items =
        [
            new FakeCanvasItem("left", new SpatialBounds(0, 0, 100, 100)),
            new FakeCanvasItem("right", new SpatialBounds(100, 0, 100, 100))
        ];
        var source = new FakeSceneSource(new SpatialBounds(0, 0, 200, 100), items);

        // A viewport ending exactly on the shared edge intersects both tiles.
        var visible = source.QueryVisible(new SpatialBounds(0, 0, 100, 100));

        Assert.That(visible.Select(item => item.Id), Is.EquivalentTo(new[] { "left", "right" }));
    }

    [Test]
    public void ApplyFrame_WithNoVisibleItems_FallsBackToEmptyList()
    {
        var viewModel = new CanvasViewModel();
        viewModel.SetSceneBounds(new SpatialBounds(0, 0, 100, 100));

        viewModel.ApplyFrame(new SpatialBounds(0, 0, 50, 50), 0, 0);

        Assert.That(viewModel.VisibleItems, Is.Empty);
    }

    [Test]
    public void SceneChanged_IsObservableThroughTheSourceContract()
    {
        var source = new FakeSceneSource(new SpatialBounds(0, 0, 100, 100), []);
        var changeCount = 0;
        source.SceneChanged += (_, _) => changeCount++;

        source.RaiseSceneChanged();

        Assert.That(changeCount, Is.EqualTo(1));
    }
}
