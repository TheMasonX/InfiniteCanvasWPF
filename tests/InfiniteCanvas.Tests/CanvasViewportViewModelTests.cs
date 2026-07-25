using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class CanvasViewportViewModelTests
{
    [Test]
    public async Task ApplyFrame_UpdatesVisibleTotalAndPublishedState()
    {
        var spatialIndex = new LiveSpatialIndexService<SpatialRecord<string>>(new LinearSpatialIndexBuilder<SpatialRecord<string>>());
        spatialIndex.AddRange(
        [
            new SpatialRecord<string>("visible", new SpatialBounds(0, 0, 5, 5), "visible"),
            new SpatialRecord<string>("hidden", new SpatialBounds(50, 50, 5, 5), "hidden")
        ]);

        await spatialIndex.PublishSnapshotAsync();

        var viewModel = new CanvasViewportViewModel<SpatialRecord<string>>(spatialIndex)
        {
            Viewport = new SpatialBounds(-1, -1, 10, 10)
        };

        viewModel.ApplyFrame(new SpatialBounds(-1, -1, 10, 10), 1);

        Assert.That(viewModel.VisibleItemCount, Is.EqualTo(1));
        Assert.That(viewModel.TotalItemCount, Is.EqualTo(2));
        Assert.That(viewModel.LastSnapshotPublishedAtUtc, Is.Not.Null);
    }

    [Test]
    public void ApplyFrame_WithNonLiveIndex_LeavesPublishedTimestampNull()
    {
        var spatialIndex = new ImmutableSpatialIndexService<SpatialRecord<string>>(
        [
            new SpatialRecord<string>("visible", new SpatialBounds(0, 0, 5, 5), "visible"),
            new SpatialRecord<string>("hidden", new SpatialBounds(50, 50, 5, 5), "hidden")
        ]);

        var viewModel = new CanvasViewportViewModel<SpatialRecord<string>>(spatialIndex)
        {
            Viewport = new SpatialBounds(0, 0, 10, 10)
        };

        viewModel.ApplyFrame(new SpatialBounds(0, 0, 10, 10), 1);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.VisibleItemCount, Is.EqualTo(1));
            Assert.That(viewModel.TotalItemCount, Is.EqualTo(2));
            Assert.That(viewModel.LastSnapshotPublishedAtUtc, Is.Null);
        });
    }
}
