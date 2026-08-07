#if WINDOWS
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

/// <summary>
/// Benchmarks for TileWorkCoordinator viewport culling and priority scheduling (ICW-144).
///
/// These exercises measure:
/// - PublishInterestSet throughput under zero, moderate, and full cancellation load
/// - DrainQueueWithLivenessCheck visible-item priority promotion
/// - Combined stress (rapid interest-set publication + drain cycles) simulating fast scroll
///
/// All scenarios use deterministic, repeatable inputs. Coordinate stage counters with
/// ICW-132 (stage instrumentation) and benchmark structure with ICW-133 (benchmark matrix).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
public class TileWorkCoordinatorBenchmarks
{
    // Pre-built cache keys for reuse across iterations.
    // Group A: visible-tile simulation (keys 0-19).
    // Group B: non-visible-tile simulation (keys 20-49).
    private static readonly BackgroundTileCacheKey[] VisibleKeys;
    private static readonly BackgroundTileCacheKey[] NonVisibleKeys;

    private const int VisibleCount = 20;
    private const int NonVisibleCount = 30;

    // Large visible set for center-distance ordering benchmarks (ICW-205).
    private const int LargeVisibleCount = 1000;
    private static readonly BackgroundTileCacheKey[] LargeVisibleKeys;
    private static readonly BackgroundTileCacheKey SingleKey;

    private static readonly object Claimant = new();

    // A factory that completes synchronously.
    private static readonly Func<CancellationToken, ValueTask<byte[]>> FastFactory =
        _ => new ValueTask<byte[]>([42]);

    static TileWorkCoordinatorBenchmarks()
    {
        VisibleKeys = Enumerable.Range(0, VisibleCount)
            .Select(i => new BackgroundTileCacheKey("bench", $"tile-{i}", 1, 0))
            .ToArray();

        NonVisibleKeys = Enumerable.Range(0, NonVisibleCount)
            .Select(i => new BackgroundTileCacheKey("bench", $"tile-off-{i}", 1, 0))
            .ToArray();

        LargeVisibleKeys = Enumerable.Range(0, LargeVisibleCount)
            .Select(i => new BackgroundTileCacheKey("bench", $"tile-big-{i}", 1, 0))
            .ToArray();

        SingleKey = new BackgroundTileCacheKey("bench", "single", 1, 0);
    }

    /// <summary>
    /// Number of queued items in the coordinator before each benchmark operation.
    /// </summary>
    [Params(10, 50)]
    public int QueueDepth { get; set; }

    private TileWorkCoordinator? _coordinator;

    [IterationSetup]
    public void SetupCoordinator()
    {
        _coordinator?.Dispose();
        _coordinator = new TileWorkCoordinator(maxConcurrency: 1); // Force queue buildup
    }

    [IterationCleanup]
    public void CleanupCoordinator()
    {
        _coordinator?.Dispose();
        _coordinator = null;
    }

    /// <summary>
    /// Baseline: PublishInterestSet with no queued items.
    /// Measures the fixed cost of interest-set publication.
    /// </summary>
    [Benchmark]
    public void PublishInterestSet_EmptyQueue()
    {
        var interestSet = BuildInterestSet(VisibleKeys, prefetchCount: 0);
        _coordinator!.PublishInterestSet(interestSet);
    }

    /// <summary>
    /// PublishInterestSet with a full queue where all items are visible.
    /// Measures overhead when no cancellation is needed (best case).
    /// </summary>
    [Benchmark]
    public void PublishInterestSet_AllVisible()
    {
        EnqueueAll(_coordinator!, VisibleKeys);
        var interestSet = BuildInterestSet(VisibleKeys, prefetchCount: 0);
        _coordinator!.PublishInterestSet(interestSet);
    }

    /// <summary>
    /// PublishInterestSet with a full queue where no items are visible.
    /// Measures cancellation throughput when all queued work is stale (worst case).
    /// </summary>
    [Benchmark]
    public void PublishInterestSet_NoneVisible()
    {
        EnqueueAll(_coordinator!, NonVisibleKeys);
        // Publish an empty interest set — nothing is visible.
        _coordinator!.PublishInterestSet(ViewportInterestSet.Empty);
    }

    /// <summary>
    /// PublishInterestSet with a mixed queue: some visible, some not.
    /// Measures real-world cancellation throughput during fast scroll.
    /// </summary>
    [Benchmark]
    public void PublishInterestSet_MixedVisibility()
    {
        var mixedKeys = VisibleKeys.Concat(NonVisibleKeys).ToArray();
        EnqueueAll(_coordinator!, mixedKeys);
        var interestSet = BuildInterestSet(VisibleKeys, prefetchCount: 0);
        _coordinator!.PublishInterestSet(interestSet);
    }

    /// <summary>
    /// DrainQueueWithLivenessCheck baseline with empty interest set (FIFO fallback).
    /// </summary>
    [Benchmark]
    public void DrainQueue_FifoFallback()
    {
        EnqueueAll(_coordinator!, VisibleKeys);
        _coordinator!.PublishInterestSet(ViewportInterestSet.Empty);
        // Drain is triggered by completions — simulate by completing the active item.
        CompleteActiveWork();
    }

    /// <summary>
    /// DrainQueueWithLivenessCheck with visible items promoted over non-visible.
    /// </summary>
    [Benchmark]
    public void DrainQueue_VisiblePromoted()
    {
        // Fill queue with non-visible items first, then visible items at the back.
        EnqueueAll(_coordinator!, NonVisibleKeys.Take(QueueDepth / 2));
        EnqueueAll(_coordinator!, VisibleKeys.Take(QueueDepth / 2));

        _coordinator!.PublishInterestSet(BuildInterestSet(VisibleKeys, prefetchCount: 0));
        CompleteActiveWork();
    }

    /// <summary>
    /// Combined stress: multiple PublishInterestSet + drain cycles simulating fast scroll.
    /// Each iteration publishes three different interest sets with intervening drains.
    /// </summary>
    [Benchmark]
    public void FastScrollStress_ThreeCycles()
    {
        // Cycle 1: all visible
        EnqueueAll(_coordinator!, VisibleKeys);
        _coordinator!.PublishInterestSet(BuildInterestSet(VisibleKeys, prefetchCount: 0));
        CompleteActiveWork();

        // Cycle 2: none visible (viewport moved away)
        EnqueueAll(_coordinator!, NonVisibleKeys);
        _coordinator!.PublishInterestSet(ViewportInterestSet.Empty);
        CompleteActiveWork();

        // Cycle 3: half visible (viewport returned)
        var halfVisible = VisibleKeys.Take(VisibleCount / 2).ToArray();
        var halfNonVisible = NonVisibleKeys.Take(NonVisibleCount / 2).ToArray();
        EnqueueAll(_coordinator!, halfVisible.Concat(halfNonVisible).ToArray());
        _coordinator!.PublishInterestSet(BuildInterestSet(halfVisible, prefetchCount: 0));
        CompleteActiveWork();
    }

    /// <summary>
    /// Priority drain with 1000 visible keys ordered by center distance.
    /// Keys are enqueued in reverse index order, so the heap must reorder
    /// them into distance order. Measures true heap ordering cost, not
    /// insertion order (ICW-205).
    /// </summary>
    [Benchmark]
    public void DrainQueue_PriorityDistanceOrdered()
    {
        EnqueueAll(_coordinator!, LargeVisibleKeys.Reverse());
        _coordinator!.PublishInterestSet(BuildDistanceInterestSet(LargeVisibleKeys, centerIndex: 0));
        CompleteActiveWork();
    }

    /// <summary>
    /// Combined stress: three publish cycles that move the camera center.
    /// Each cycle re-orders the same 1000-key queue by a different center.
    /// </summary>
    [Benchmark]
    public void FastScrollStress_PriorityCenterChange()
    {
        // Cycle 1: center near the low-index tiles.
        EnqueueAll(_coordinator!, LargeVisibleKeys.Reverse());
        _coordinator!.PublishInterestSet(BuildDistanceInterestSet(LargeVisibleKeys, centerIndex: 0));
        CompleteActiveWork();

        // Cycle 2: center far from every tile (same visibility, new order).
        EnqueueAll(_coordinator!, LargeVisibleKeys.Reverse());
        _coordinator!.PublishInterestSet(BuildDistanceInterestSet(LargeVisibleKeys, centerIndex: LargeVisibleCount * 10));
        CompleteActiveWork();

        // Cycle 3: center back near the middle of the tile strip.
        EnqueueAll(_coordinator!, LargeVisibleKeys.Reverse());
        _coordinator!.PublishInterestSet(BuildDistanceInterestSet(LargeVisibleKeys, centerIndex: LargeVisibleCount / 2));
        CompleteActiveWork();
    }

    // --- Helpers ---

    private static ViewportInterestSet BuildInterestSet(
        BackgroundTileCacheKey[] visible, int prefetchCount)
    {
        var visibleSet = new HashSet<BackgroundTileCacheKey>(visible);
        var prefetchSet = prefetchCount > 0
            ? new HashSet<BackgroundTileCacheKey>(visible.Take(prefetchCount))
            : new HashSet<BackgroundTileCacheKey>();
        return new ViewportInterestSet(visibleSet, prefetchSet);
    }

    /// <summary>
    /// Builds an interest set whose squared-distance provider ranks keys by
    /// distance from the given center index (ICW-205).
    /// </summary>
    private static ViewportInterestSet BuildDistanceInterestSet(
        BackgroundTileCacheKey[] visible, double centerIndex)
    {
        return new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey>(visible),
            new HashSet<BackgroundTileCacheKey>(),
            centerX: centerIndex,
            centerY: 0,
            selectedMipLevel: 0,
            squaredDistanceFromCenter: key =>
            {
                var dx = LargeKeyIndex(key.TileId) - centerIndex;
                return dx * dx;
            });
    }

    private static double LargeKeyIndex(string tileId)
    {
        const string prefix = "tile-big-";
        return tileId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(tileId.AsSpan(prefix.Length), out var index)
                ? index
                : 0;
    }

    private static void EnqueueAll(
        TileWorkCoordinator coordinator, IEnumerable<BackgroundTileCacheKey> keys)
    {
        foreach (var key in keys)
        {
            coordinator.Request(key, FastFactory, Claimant, CancellationToken.None);
        }
    }

    /// <summary>
    /// Completes the single active work item so DrainQueue runs.
    /// With maxConcurrency=1, only the first admitted item is active;
    /// the rest are queued. We cannot directly invoke DrainQueue, so we
    /// wait for the active item to complete (it's synchronous).
    /// </summary>
    private void CompleteActiveWork()
    {
        // The FastFactory completes synchronously, so the active work
        // should finish almost immediately. Spin-wait briefly.
        var deadline = Environment.TickCount + 5000; // 5 second timeout
        while (_coordinator!.GetCounters().ActiveCount > 0)
        {
            if (Environment.TickCount - deadline > 0)
                break;
            Thread.Yield();
        }
    }
}
#endif
