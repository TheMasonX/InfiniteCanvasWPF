using InfiniteCanvas.Core;
using System.Collections.Immutable;
using System.Threading;

namespace InfiniteCanvas.Spatial;

public sealed class LiveSpatialIndexService<T> : ISpatialIndexService<T> where T : ISpatialEntity
{
    private readonly ISpatialIndexBuilder<T> _indexBuilder;
    private LiveState _state = LiveState.Empty;
    private int _publishInProgress;

    public LiveSpatialIndexService(ISpatialIndexBuilder<T> indexBuilder)
    {
        _indexBuilder = indexBuilder;
    }

    public int Count
    {
        get
        {
            var state = Volatile.Read(ref _state);
            return state.SnapshotItems.Length + state.HotItems.Length + state.PublishingItems.Length;
        }
    }

    public DateTimeOffset? LastPublishedAtUtc => Volatile.Read(ref _state).PublishedAtUtc;

    public void Add(T item)
    {
        UpdateState(state => state with { HotItems = state.HotItems.Add(item) });
    }

    public void AddRange(IEnumerable<T> items)
    {
        var buffered = items as IReadOnlyCollection<T> ?? items.ToArray();
        if (buffered.Count == 0)
        {
            return;
        }

        UpdateState(state => state with { HotItems = state.HotItems.AddRange(buffered) });
    }

    public IReadOnlyList<T> Query(SpatialBounds viewport)
    {
        var state = Volatile.Read(ref _state);

        var results = new List<T>();
        results.AddRange(state.SnapshotIndex.Query(viewport));
        AppendMatches(results, state.PublishingItems, viewport);
        AppendMatches(results, state.HotItems, viewport);
        return results;
    }

    public async Task PublishSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _publishInProgress, 1) == 1)
        {
            return;
        }

        LiveState? publishingState = null;

        try
        {
            while (true)
            {
                var current = Volatile.Read(ref _state);
                if (current.HotItems.IsDefaultOrEmpty)
                {
                    return;
                }

                var next = current with
                {
                    HotItems = ImmutableArray<T>.Empty,
                    PublishingItems = current.HotItems
                };

                if (ReferenceEquals(Interlocked.CompareExchange(ref _state, next, current), current))
                {
                    publishingState = next;
                    break;
                }
            }

            var mergedItems = publishingState.SnapshotItems.AddRange(publishingState.PublishingItems);
            var rebuiltIndex = await Task.Run(() => _indexBuilder.Build(mergedItems), cancellationToken).ConfigureAwait(false);
            UpdateState(state => state with
            {
                SnapshotItems = mergedItems,
                SnapshotIndex = rebuiltIndex,
                PublishingItems = ImmutableArray<T>.Empty,
                PublishedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch
        {
            if (publishingState is not null)
            {
                UpdateState(state => state with
                {
                    HotItems = state.PublishingItems.AddRange(state.HotItems),
                    PublishingItems = ImmutableArray<T>.Empty
                });
            }

            throw;
        }
        finally
        {
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

    private void UpdateState(Func<LiveState, LiveState> update)
    {
        while (true)
        {
            var current = Volatile.Read(ref _state);
            var next = update(current);

            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, next, current), current))
            {
                return;
            }
        }
    }

    private sealed record LiveState(
        ImmutableArray<T> SnapshotItems,
        ISpatialIndexService<T> SnapshotIndex,
        ImmutableArray<T> HotItems,
        ImmutableArray<T> PublishingItems,
        DateTimeOffset? PublishedAtUtc)
    {
        public static LiveState Empty { get; } = new(
            ImmutableArray<T>.Empty,
            new ImmutableSpatialIndexService<T>([]),
            ImmutableArray<T>.Empty,
            ImmutableArray<T>.Empty,
            null);
    }
}
