using InfiniteCanvas.Core;
using InfiniteCanvas.Spatial;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class LiveSpatialIndexServiceTests
{
    [Test]
    public void Query_ReturnsPendingItems_BeforeSnapshotPublication()
    {
        var service = CreateService();
        var visible = new SpatialRecord<string>("visible", new SpatialBounds(0, 0, 10, 10), "visible");
        var hidden = new SpatialRecord<string>("hidden", new SpatialBounds(100, 100, 10, 10), "hidden");

        service.AddRange([visible, hidden]);

        var results = service.Query(new SpatialBounds(-5, -5, 20, 20));

        Assert.That(results.Select(item => item.Id), Is.EquivalentTo(new[] { "visible" }));
        Assert.That(service.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task PublishSnapshotAsync_PromotesHotBufferWithoutDroppingNewItems()
    {
        var builder = new DelayedBuilder<SpatialRecord<string>>();
        var service = new LiveSpatialIndexService<SpatialRecord<string>>(builder);
        var first = new SpatialRecord<string>("first", new SpatialBounds(0, 0, 10, 10), "first");
        var second = new SpatialRecord<string>("second", new SpatialBounds(5, 5, 10, 10), "second");

        service.Add(first);
        var publishTask = service.PublishSnapshotAsync();
        await builder.BuildStarted.Task;
        service.Add(second);

        var duringPublication = service.Query(new SpatialBounds(-5, -5, 30, 30));
        Assert.That(duringPublication.Select(item => item.Id), Is.EquivalentTo(new[] { "first", "second" }));

        builder.ReleaseBuild();
        await publishTask;

        var results = service.Query(new SpatialBounds(-5, -5, 30, 30));

        Assert.That(results.Select(item => item.Id), Is.EquivalentTo(new[] { "first", "second" }));
        Assert.That(service.Count, Is.EqualTo(2));
        Assert.That(service.LastPublishedAtUtc, Is.Not.Null);
    }

    [Test]
    public async Task ConcurrentQueriesAndPublications_DoNotLoseOrDuplicateItems()
    {
        var service = CreateService();
        var items = Enumerable.Range(0, 500)
            .Select(index => new SpatialRecord<string>(
                index.ToString(),
                new SpatialBounds(index, index, 1, 1),
                index.ToString()))
            .ToArray();

        service.AddRange(items);

        var queryTask = Task.Run(() =>
        {
            for (var iteration = 0; iteration < 250; iteration++)
            {
                var results = service.Query(new SpatialBounds(0, 0, 501, 501));
                Assert.That(results.Select(item => item.Id).Distinct().Count(), Is.EqualTo(results.Count));
            }
        });

        var publishTask = service.PublishSnapshotAsync();
        await Task.WhenAll(queryTask, publishTask);

        Assert.That(service.Query(new SpatialBounds(0, 0, 501, 501)), Has.Count.EqualTo(items.Length));
    }

    [Test]
    public async Task PublishSnapshotAsync_RebuildsPublishedSnapshot()
    {
        var service = CreateService();
        var first = new SpatialRecord<string>("first", new SpatialBounds(0, 0, 10, 10), "first");
        var second = new SpatialRecord<string>("second", new SpatialBounds(25, 25, 10, 10), "second");

        service.AddRange([first, second]);
        await service.PublishSnapshotAsync();

        var results = service.Query(new SpatialBounds(20, 20, 20, 20));

        Assert.That(results.Select(item => item.Id), Is.EquivalentTo(new[] { "second" }));
        Assert.That(service.Count, Is.EqualTo(2));
    }

    private static LiveSpatialIndexService<SpatialRecord<string>> CreateService()
    {
        return new(new LinearSpatialIndexBuilder<SpatialRecord<string>>());
    }

    private sealed class DelayedBuilder<T> : ISpatialIndexBuilder<T> where T : ISpatialEntity
    {
        private readonly TaskCompletionSource _buildStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseBuild = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource BuildStarted => _buildStarted;

        public ISpatialIndexService<T> Build(IReadOnlyList<T> items)
        {
            _buildStarted.TrySetResult();
            _releaseBuild.Task.GetAwaiter().GetResult();
            return new ImmutableSpatialIndexService<T>(items);
        }

        public void ReleaseBuild()
        {
            _releaseBuild.TrySetResult();
        }
    }
}
