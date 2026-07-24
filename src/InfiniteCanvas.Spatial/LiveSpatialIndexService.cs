using InfiniteCanvas.Core;
using System.Collections.Immutable;
using System.Threading;

namespace InfiniteCanvas.Spatial;

public sealed class LiveSpatialIndexService<T> : ISpatialIndexService<T> where T : ISpatialEntity
{
    private readonly ISpatialIndexBuilder<T> _indexBuilder;
    private SnapshotState _snapshot = SnapshotState.Empty;
    private ImmutableArray<T> _pendingItems = ImmutableArray<T>.Empty;
    private ImmutableArray<T> _publishingItems = ImmutableArray<T>.Empty;
    private int _publishInProgress;

    public LiveSpatialIndexService(ISpatialIndexBuilder<T> indexBuilder)
    {
        _indexBuilder = indexBuilder;
    }

    public int Count
    {
        get
        {
            var snapshot = Volatile.Read(ref _snapshot);
            var pendingCount = Volatile.Read(ref _pendingItems).Length;
            var publishingCount = Volatile.Read(ref _publishingItems).Length;
            return snapshot.Items.Length + pendingCount + publishingCount;
        }
    }

    public DateTimeOffset? LastPublishedAtUtc => Volatile.Read(ref _snapshot).PublishedAtUtc;

    public void Add(T item)
    {
        ImmutableInterlocked.Update(ref _pendingItems, items => items.Add(item));
    }

    public void AddRange(IEnumerable<T> items)
    {
        var buffered = items as IReadOnlyCollection<T> ?? items.ToArray();
        if (buffered.Count == 0)
        {
            return;
        }

        ImmutableInterlocked.Update(ref _pendingItems, existing => existing.AddRange(buffered));
    }

    public IReadOnlyList<T> Query(SpatialBounds viewport)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var pendingItems = Volatile.Read(ref _pendingItems);
        var publishingItems = Volatile.Read(ref _publishingItems);

        var results = new List<T>(snapshot.Items.Length + pendingItems.Length + publishingItems.Length);
        results.AddRange(snapshot.Index.Query(viewport));
        AppendMatches(results, publishingItems, viewport);
        AppendMatches(results, pendingItems, viewport);
        return results;
    }

    public async Task PublishSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _publishInProgress, 1) == 1)
        {
            return;
        }

        ImmutableArray<T> capturedItems = ImmutableArray<T>.Empty;

        try
        {
            capturedItems = ImmutableInterlocked.InterlockedExchange(ref _pendingItems, ImmutableArray<T>.Empty);
            if (capturedItems.IsDefaultOrEmpty)
            {
                return;
            }

            Volatile.Write(ref _publishingItems, capturedItems);

            var previousSnapshot = Volatile.Read(ref _snapshot);
            var mergedItems = previousSnapshot.Items.AddRange(capturedItems);
            var rebuiltIndex = await Task.Run(() => _indexBuilder.Build(mergedItems), cancellationToken).ConfigureAwait(false);

            var nextSnapshot = new SnapshotState(mergedItems, rebuiltIndex, DateTimeOffset.UtcNow);
            Volatile.Write(ref _snapshot, nextSnapshot);
            Volatile.Write(ref _publishingItems, ImmutableArray<T>.Empty);
        }
        catch
        {
            if (!capturedItems.IsDefaultOrEmpty)
            {
                ImmutableInterlocked.Update(ref _pendingItems, items => capturedItems.AddRange(items));
                Volatile.Write(ref _publishingItems, ImmutableArray<T>.Empty);
            }

            throw;
        }
        finally
        {
            Volatile.Write(ref _publishingItems, ImmutableArray<T>.Empty);
            Interlocked.Exchange(ref _publishInProgress, 0);
        }
    }

    private static void AppendMatches(List<T> results, ImmutableArray<T> source, SpatialBounds viewport)
    {
        foreach (var item in source)
        {
            if (item.Bounds.Intersects(viewport))
            {
                results.Add(item);
            }
        }
    }

    private sealed record SnapshotState(ImmutableArray<T> Items, ISpatialIndexService<T> Index, DateTimeOffset? PublishedAtUtc)
    {
        public static SnapshotState Empty { get; } = new(ImmutableArray<T>.Empty, new ImmutableSpatialIndexService<T>([]), null);
    }
}
