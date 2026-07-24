using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class CanvasViewportViewModelTests
{
    [Test]
    public async Task RefreshCommand_UpdatesVisibleAndPublishedState()
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

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.That(viewModel.VisibleItemCount, Is.EqualTo(1));
        Assert.That(viewModel.TotalItemCount, Is.EqualTo(2));
        Assert.That(viewModel.LastSnapshotPublishedAtUtc, Is.Not.Null);
    }

    [Test]
    public async Task RefreshCommand_ReportsRunningWhileQueryExecutesOffThread()
    {
        var spatialIndex = new BlockingSpatialIndex();
        var viewModel = new CanvasViewportViewModel<SpatialRecord<string>>(spatialIndex)
        {
            Viewport = new SpatialBounds(0, 0, 10, 10)
        };

        var refreshTask = viewModel.RefreshCommand.ExecuteAsync(null);
        await spatialIndex.QueryStarted.Task;

        Assert.That(viewModel.RefreshCommand.IsRunning, Is.True);

        spatialIndex.ReleaseQuery();
        await refreshTask;

        Assert.That(viewModel.RefreshCommand.IsRunning, Is.False);
    }

    private sealed class BlockingSpatialIndex : ISpatialIndexService<SpatialRecord<string>>
    {
        private readonly TaskCompletionSource _queryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource QueryStarted => _queryStarted;

        public int Count => 0;

        public IReadOnlyList<SpatialRecord<string>> Query(SpatialBounds viewport)
        {
            _queryStarted.TrySetResult();
            _releaseQuery.Task.GetAwaiter().GetResult();
            return [];
        }

        public void ReleaseQuery()
        {
            _releaseQuery.TrySetResult();
        }
    }
}
