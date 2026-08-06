using Serilog;

namespace InfiniteCanvas.Rendering;

/// <summary>
/// Tracks the lifecycle state of a tile work item managed by <see cref="TileWorkCoordinator"/>.
/// </summary>
public enum TileWorkItemState
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled
}

/// <summary>
/// Immutable snapshot of coordinator diagnostic counters.
/// </summary>
public readonly record struct TileWorkCoordinatorCounters(
    int ActiveCount,
    int QueuedCount,
    int AdmittedCount,
    int CoalescedCount,
    int CompletedCount,
    int CanceledCount,
    int FailedCount)
{
    public int TotalCount => AdmittedCount + CoalescedCount;

    public int PendingCount => ActiveCount + QueuedCount;

    public override string ToString() =>
        $"Active {ActiveCount}  Queued {QueuedCount}  |  " +
        $"Admitted {AdmittedCount}  Coalesced {CoalescedCount}  |  " +
        $"Completed {CompletedCount}  Canceled {CanceledCount}  Failed {FailedCount}";
}

/// <summary>
/// Deterministic priority tuple for the tile work heap (ICW-205).
/// Orders by visibility class, then squared center distance, then mip
/// suitability, then a monotonic FIFO sequence assigned at admission.
/// </summary>
internal readonly struct TileWorkPriority : IComparable<TileWorkPriority>, IEquatable<TileWorkPriority>
{
    public TileWorkPriority(int rank, double squaredDistance, int mipDistance, long sequence)
    {
        Rank = rank;
        SquaredDistance = squaredDistance;
        MipDistance = mipDistance;
        Sequence = sequence;
    }

    /// <summary>0 = visible, 1 = prefetch, 2 = stale/outside interest set.</summary>
    public int Rank { get; }

    /// <summary>Squared distance from the camera center. Smaller is higher priority.</summary>
    public double SquaredDistance { get; }

    /// <summary>Absolute difference from the selected mip level. Smaller is higher priority.</summary>
    public int MipDistance { get; }

    /// <summary>Monotonic FIFO sequence assigned at admission. Smaller is earlier.</summary>
    public long Sequence { get; }

    public int CompareTo(TileWorkPriority other)
    {
        var rankCompare = Rank.CompareTo(other.Rank);
        if (rankCompare != 0) return rankCompare;
        var distanceCompare = SquaredDistance.CompareTo(other.SquaredDistance);
        if (distanceCompare != 0) return distanceCompare;
        var mipCompare = MipDistance.CompareTo(other.MipDistance);
        if (mipCompare != 0) return mipCompare;
        return Sequence.CompareTo(other.Sequence);
    }

    public bool Equals(TileWorkPriority other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is TileWorkPriority other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Rank, SquaredDistance, MipDistance, Sequence);
}

/// <summary>
/// Bounded, deduplicated, cancellable coordinator for background tile generation work.
/// Manages concurrency, coalesces equal cache-key requests, separates claimant interest
/// from shared-fill ownership, and exposes structured diagnostic counters.
/// </summary>
/// <remarks>
/// This is the foundation for ICW-142 (bounded cancellable tile materialization).
/// ICW-143 adds viewport interest snapshots and priority ordering.
/// ICW-205 replaces the FIFO queue with a heap ordered by visibility class,
/// center distance, and mip suitability, and enforces the hard no-flash rule:
/// a visible tile's in-flight work survives frame-boundary token fire.
///
/// Design rules (from ADR-0006):
/// - A frame's viewport update publishes an interest snapshot; only the claimants
///   (current visible frames) own the request.
/// - Cancellation of a stale claimant removes its interest but *must not* cancel
///   the underlying generation if another claimant still needs it.
/// - Cache reservations are acquired at admission and released exactly once on
///   cancellation, failure, or rejected admission.
/// - In-flight work for a key still in the interest set survives its last
///   claimant leaving (no-flash rule).
/// </remarks>
public sealed class TileWorkCoordinator : IDisposable
{
    /// <summary>Default maximum concurrent generation operations.</summary>
    public const int DefaultMaxConcurrency = 4;

    private readonly int _maxConcurrency;
    private readonly CancellationTokenSource _disposeCts = new();

    // All mutable state is guarded by _lock.
    private readonly Lock _lock = new();
    private readonly Dictionary<BackgroundTileCacheKey, TileWorkItem> _items = new();
    private readonly PriorityQueue<BackgroundTileCacheKey, TileWorkPriority> _queue = new();
    private readonly Dictionary<BackgroundTileCacheKey, long> _removedKeys = new();
    private ViewportInterestSet _interestSet = ViewportInterestSet.Empty;
    private int _activeCount;
    private long _sequence;

    // Diagnostic counters (interlocked for lock-free reads).
    private int _admittedCount;
    private int _coalescedCount;
    private int _completedCount;
    private int _canceledCount;
    private int _failedCount;

    private bool _disposed;

    /// <summary>
    /// Creates a new coordinator with the given maximum concurrency.
    /// </summary>
    public TileWorkCoordinator(int maxConcurrency = DefaultMaxConcurrency)
    {
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency,
                "Max concurrency must be at least 1.");
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Submits a tile generation request. If a request with the same cache key
    /// is already queued or running, this call coalesces — it adds the claimant
    /// without starting a second fill.
    /// </summary>
    /// <param name="key">The complete source/revision/mip cache key.</param>
    /// <param name="factory">Factory that produces the tile pixel data. Called at most once per distinct cache key.</param>
    /// <param name="claimantId">Identifies the frame or viewport interest that owns this request.</param>
    /// <param name="claimantToken">Cancellation token scoped to the claimant's lifetime.
    /// When this token fires, the claimant is considered removed.</param>
    /// <param name="onCompleted">Optional callback invoked (on an unknown thread) when the fill completes successfully.</param>
    /// <param name="onFailed">Optional callback invoked (on an unknown thread) when the fill fails.</param>
    /// <param name="tryReserve">Optional reservation function that must return a non-null lease for the work to be admitted.
    /// The lease is disposed on cancellation or failure.</param>
    /// <returns>True if the request was admitted or coalesced; false if the reservation was rejected.</returns>
    public bool Request(
        BackgroundTileCacheKey key,
        Func<CancellationToken, ValueTask<byte[]>> factory,
        object claimantId,
        CancellationToken claimantToken,
        Action<BackgroundTileCacheKey, byte[]>? onCompleted = null,
        Action<BackgroundTileCacheKey, Exception>? onFailed = null,
        Func<BackgroundTileCacheKey, ICacheReservation?>? tryReserve = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(claimantId);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // If the key already has a work item, coalesce — add claimant, don't
            // start a new fill. A running item canceled mid-flight stays in
            // _items until its worker physically stops (ICW-P0-ACTIVECOUNT
            // residual B). Coalescing onto that terminal item would swallow a
            // scroll-away-and-back re-request: its factory token is already
            // canceled, so the fresh regeneration never starts (ICW-320 F-006).
            // Treat a canceled item as not present and admit fresh work. The
            // duplicate-CPU cost of the overlapping worker is accepted; the
            // tile's OnCoordinatorPixelsGenerated guard (_pixels is null
            // first-writer check plus the epoch comparison) discards the
            // stale result (ICW-330). For the eviction case specifically the
            // epoch is NOT bumped, so the _pixels is null check is what drops
            // the evicted-but-still-running generation.
            if (_items.TryGetValue(key, out var existing)
                && existing.State is not (TileWorkItemState.Canceled
                    or TileWorkItemState.Completed
                    or TileWorkItemState.Failed))
            {
                existing.AddClaimant(claimantId, claimantToken, onCompleted, onFailed);
                Interlocked.Increment(ref _coalescedCount);
                Log.Debug("CoordReq COALESCE {SourceId}/{TileId} mip{MipLevel} rev{Rev} claimant={Claimant} active={Active} queued={Queued}",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, claimantId, _activeCount, _queue.Count);
                return true;
            }

            // Attempt reservation before admission.
            //
            // REENTRANT-LOCK CHAIN (ICW-322 F-009): tryReserve can call back
            // into this coordinator's _lock on the same thread through
            // TileCacheBudget.TryReserve -> SampleImageTile.EvictCacheEntry ->
            // RemoveClaimant. This is safe ONLY through same-thread Lock
            // reentrancy (System.Threading.Lock.Enter re-enters for the current
            // thread). Never add an await or a thread hop anywhere inside this
            // chain; it would become a hard deadlock.
            var reservation = tryReserve?.Invoke(key);
            if (tryReserve is not null && reservation is null)
            {
                Log.Warning("CoordReq REJECTED {SourceId}/{TileId} mip{MipLevel} rev{Rev} — reservation failed (budget full/no evictable tiles)",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision);
                return false;
            }

            var item = new TileWorkItem(key, factory, reservation, _disposeCts.Token, ShouldCancelWhenNoClaimants)
            {
                Sequence = ++_sequence
            };
            item.AddClaimant(claimantId, claimantToken, onCompleted, onFailed);
            _items[key] = item;
            Interlocked.Increment(ref _admittedCount);

            if (_activeCount < _maxConcurrency)
            {
                Log.Debug("CoordReq START {SourceId}/{TileId} mip{MipLevel} rev{Rev} active={Active}",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, _activeCount);
                StartWorkItem(item);
            }
            else
            {
                Log.Debug("CoordReq QUEUE {SourceId}/{TileId} mip{MipLevel} rev{Rev} queueDepth={QueueDepth}",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, LiveQueuedCount);
                _queue.Enqueue(key, ComputePriority(key));
            }

            return true;
        }
    }

    /// <summary>
    /// Removes a specific claimant from a work item. If no claimants remain,
    /// the work item is canceled and its reservation released.
    /// </summary>
    public void RemoveClaimant(BackgroundTileCacheKey key, object claimantId)
    {
        ArgumentNullException.ThrowIfNull(claimantId);

        lock (_lock)
        {
            if (_disposed) return;

            if (!_items.TryGetValue(key, out var item))
                return;

            if (!item.RemoveClaimant(claimantId))
                return;

            // No-flash rule (ICW-205): cancel only when the key is not held
            // by the published interest set. A visible tile's work survives
            // its frame-token fire so the next frame re-claims the same key
            // and generation completes exactly once instead of restarting
            // every frame.
            if (item.ClaimantCount == 0 && !_interestSet.Contains(key))
            {
                Log.Information("Coord CANCEL {SourceId}/{TileId} mip{MipLevel} rev{Rev} — last claimant {Claimant} removed",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, claimantId);
                CancelWorkItem(key, item);
            }
        }
    }

    /// <summary>
    /// Removes all claimants matching the given owner from all work items.
    /// Any work item that loses its last claimant is canceled.
    /// </summary>
    public void RemoveAllClaimants(object claimantId)
    {
        ArgumentNullException.ThrowIfNull(claimantId);

        lock (_lock)
        {
            if (_disposed) return;

            // Collect keys whose last claimant was removed and which are not
            // held by the published interest set (no-flash rule, ICW-205).
            var toCancel = new List<(BackgroundTileCacheKey Key, TileWorkItem Item)>();
            foreach (var (key, item) in _items)
            {
                if (item.RemoveClaimant(claimantId)
                    && item.ClaimantCount == 0
                    && !_interestSet.Contains(key))
                {
                    toCancel.Add((key, item));
                }
            }

            if (toCancel.Count > 0)
            {
                Log.Information("Coord RemoveAllClaimants {Claimant}: canceling {Count} orphaned work items",
                    claimantId, toCancel.Count);
            }

            foreach (var (key, item) in toCancel)
            {
                CancelWorkItem(key, item);
            }
        }
    }

    /// <summary>
    /// Cancels all in-flight and queued work, releasing all reservations.
    /// Used during shutdown or full scene regeneration.
    /// </summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            if (_disposed) return;

            var count = _items.Count;
            Log.Information("Coord CancelAll: canceling {Count} active/queued work items", count);

            var keys = _items.Keys.ToArray();
            foreach (var key in keys)
            {
                if (_items.TryGetValue(key, out var item))
                {
                    CancelWorkItem(key, item);
                }
            }

            _queue.Clear();
            _removedKeys.Clear();
        }
    }

    /// <summary>
    /// Publishes the current viewport interest set to the coordinator.
    /// Any queued or running work whose cache key is not in the interest
    /// set is canceled, since no current frame or viewport claims it.
    /// The interest set is also used by DrainQueueWithLivenessCheck for
    /// priority ordering (visible items drain before prefetch items).
    /// </summary>
    /// <param name="interestSet">
    /// The set of tile cache keys that are currently interesting to the
    /// viewport. Can include both visible and prefetch keys.
    /// Use <see cref="ViewportInterestSet.Empty"/> when no frame is active.
    /// </param>
    /// <remarks>
    /// This is the primary hook for ICW-143 viewport culling. Call this
    /// from the render pipeline (RenderFrameAsync) before starting tile
    /// generation for the current frame so that stale work from previous
    /// frames is removed.
    /// </remarks>
    public void PublishInterestSet(ViewportInterestSet interestSet)
    {
        lock (_lock)
        {
            if (_disposed) return;

            _interestSet = interestSet;

            // Cancel any queued items whose keys are not in the interest set.
            // Running items are NOT cancelled — they are allowed to complete
            // since their pixels may still be useful for cache warming.
            // Only queued (not yet started) items are culled.
            //
            // NOTE: Call CancelWorkItem directly instead of removing claimants
            // first. CancelWorkItem calls DispatchFailed which snapshots the
            // claimant list — if we remove claimants first, the failure callback
            // is never delivered and the tile's _generationQueued flag stays set
            // permanently (ICW-143 bug fix).
            var toCancel = new List<BackgroundTileCacheKey>();
            foreach (var (key, item) in _items)
            {
                if (item.State != TileWorkItemState.Queued)
                    continue;

                if (!interestSet.Contains(key) && item.ClaimantCount > 0)
                {
                    toCancel.Add(key);
                }
            }

            foreach (var key in toCancel)
            {
                if (_items.TryGetValue(key, out var item))
                {
                    CancelWorkItem(key, item);
                }
            }

            // Rebuild the heap so queued priorities match the new interest
            // set and camera center. Once per published frame.
            RebuildQueue();
        }
    }

    /// <summary>
    /// Returns an atomic snapshot of diagnostic counters.
    /// </summary>
    public TileWorkCoordinatorCounters GetCounters()
    {
        lock (_lock)
        {
            return new TileWorkCoordinatorCounters(
                ActiveCount: _activeCount,
                QueuedCount: LiveQueuedCount,
                AdmittedCount: Volatile.Read(ref _admittedCount),
                CoalescedCount: Volatile.Read(ref _coalescedCount),
                CompletedCount: Volatile.Read(ref _completedCount),
                CanceledCount: Volatile.Read(ref _canceledCount),
                FailedCount: Volatile.Read(ref _failedCount));
        }
    }

    public void Dispose()
    {
        CancelAll();
        _disposeCts.Cancel();
        _disposeCts.Dispose();

        lock (_lock)
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Starts a work item's background factory. Must be called while holding
    /// <c>_lock</c> (ICW-330): it mutates shared item state and the active
    /// count. Never await inside this method.
    /// </summary>
    private void StartWorkItem(TileWorkItem item)
    {
        item.State = TileWorkItemState.Running;
        item.SetRunning(); // Mark as running for atomic cancel detection.
        _activeCount++;

        Log.Debug("Coord START {SourceId}/{TileId} mip{MipLevel} rev{Rev} (active now {Active})",
            item.CacheKey.SourceId, item.CacheKey.TileId, item.CacheKey.MipLevel,
            item.CacheKey.ContentRevision, _activeCount);

        _ = Task.Run(async () =>
        {
            try
            {
                var pixels = await item.Factory(item.WorkToken).ConfigureAwait(false);

                var wasCanceled = false;
                lock (_lock)
                {
                    if (_disposed)
                        return;

                    wasCanceled = item.State == TileWorkItemState.Canceled;
                    if (!wasCanceled)
                    {
                        item.State = TileWorkItemState.Completed;
                        Interlocked.Increment(ref _completedCount);
                        _items.Remove(item.CacheKey);
                    }

                    // Always decrement active count when the worker physically stops.
                    // This ensures the concurrency cap reflects real execution, not
                    // cancellation-request state. If CancelWorkItem ran first, it does
                    // not decrement — only this termination path does.
                    _activeCount--;
                    Log.Debug("Coord COMPLETE {SourceId}/{TileId} mip{MipLevel} rev{Rev} (active now {Active})",
                        item.CacheKey.SourceId, item.CacheKey.TileId, item.CacheKey.MipLevel,
                        item.CacheKey.ContentRevision, _activeCount);
                }

                // Always dispatch completion so the tile can reset its
                // generation-queued flag, even if the item was canceled
                // (the factory ran to completion). The tile's callback
                // handles epoch checks and decides whether to publish.
                item.DispatchCompleted(pixels);
                DrainQueue();
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Coord CANCELED (inflight) {SourceId}/{TileId} mip{MipLevel} rev{Rev}",
                    item.CacheKey.SourceId, item.CacheKey.TileId, item.CacheKey.MipLevel,
                    item.CacheKey.ContentRevision);
                HandleWorkStopped(item, TileWorkItemState.Canceled);
                // Notify tile that work was canceled so it can reset flags.
                item.DispatchFailed(new OperationCanceledException(
                    "Tile work was canceled during generation"));
                DrainQueue();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Coord FAILED {SourceId}/{TileId} mip{MipLevel} rev{Rev}",
                    item.CacheKey.SourceId, item.CacheKey.TileId, item.CacheKey.MipLevel,
                    item.CacheKey.ContentRevision);
                HandleWorkStopped(item, TileWorkItemState.Failed);
                item.DispatchFailed(ex);
                DrainQueue();
            }
        }, _disposeCts.Token);
    }

    private void HandleWorkStopped(TileWorkItem item, TileWorkItemState finalState)
    {
        lock (_lock)
        {
            // Always decrement active count when a worker physically stops,
            // even if the coordinator is disposed or the item was already
            // canceled externally. This ensures the concurrency cap correctly
            // represents physical execution.
            //
            // If disposed, skip other cleanup — CancelAll/Dispose already
            // removed the item and released its reservation.
            if (_disposed)
            {
                _activeCount = Math.Max(0, _activeCount - 1);
                return;
            }

            // If the item was already canceled externally (by CancelWorkItem),
            // skip state/diagnostic changes but still decrement active count
            // because this worker is physically done.
            var alreadyCanceled = item.State == TileWorkItemState.Canceled;
            if (!alreadyCanceled)
            {
                item.State = finalState;
                if (finalState == TileWorkItemState.Canceled)
                    Interlocked.Increment(ref _canceledCount);
                else
                    Interlocked.Increment(ref _failedCount);
            }

            _activeCount = Math.Max(0, _activeCount - 1);

            // Remove only when this worker's item is still the current item for
            // the key (ICW-320 F-007). A cancel-and-re-request may have admitted
            // a fresh item for the same key while this worker was still stopping.
            // Removing by key alone would clobber the newer item and orphan its
            // running work from coordinator tracking.
            if (_items.TryGetValue(item.CacheKey, out var current)
                && ReferenceEquals(current, item))
            {
                _items.Remove(item.CacheKey);
            }

            item.DisposeReservation();
        }
    }

    /// <summary>
    /// Cancels a work item: signals the work token for running items, or
    /// removes and releases queued items. Must be called while holding
    /// <c>_lock</c> (ICW-330): it mutates shared item state, the queue, and
    /// reservations. Never await inside this method.
    /// </summary>
    private void CancelWorkItem(BackgroundTileCacheKey key, TileWorkItem item)
    {
        if (item.State is TileWorkItemState.Completed or TileWorkItemState.Failed or TileWorkItemState.Canceled)
            return;

        // ICW-330: query prior state with IsRunning(). Reusing the mutating
        // SetRunning() transition purely to query state would flip a queued
        // item's _running flag as a side effect of cancellation.
        var wasRunning = item.IsRunning() && _activeCount > 0;
        item.State = TileWorkItemState.Canceled;
        Interlocked.Increment(ref _canceledCount);

        Log.Warning("Coord CANCEL {SourceId}/{TileId} mip{MipLevel} rev{Rev} state={WasRunning}",
            key.SourceId, key.TileId, key.MipLevel, key.ContentRevision,
            wasRunning ? "in-flight" : "queued");

        if (wasRunning)
        {
            // In-flight work: signal cancellation via the work token source.
            // Do NOT decrement _activeCount here — the worker's termination path
            // (completion, OperationCanceledException, or exception) will handle
            // the decrement when the factory delegate actually stops. This ensures
            // the concurrency cap represents physically executing work, not
            // cancellation-request state.
            item.CancelWork();
        }
        else
        {
            // Queued work: notify the tile so it can reset its generation flag.
            // Tombstone the exact item sequence so a later re-admission of the
            // same cache key is never skipped by this cancellation.
            _removedKeys[key] = item.Sequence;
            item.DispatchFailed(new OperationCanceledException(
                "Tile work was canceled before execution"));

            // Queued work never reaches HandleWorkStopped, so it owns removal
            // and reservation cleanup on this path.
            _items.Remove(key);
            item.DisposeReservation();
        }

        // Running work remains in _items until the worker physically stops.
        // A cancel-and-re-request can therefore admit duplicate work briefly.
        // Epoch guards discard stale results, but the duplicate still costs CPU.
        // The worker termination path owns removal and reservation cleanup.
    }

    /// <summary>
    /// True when the work item is still needed: it has a live claimant, or its
    /// key is held by the published interest set. The interest-set case covers
    /// the momentary zero-claimant window between frame boundaries (no-flash
    /// rule, ICW-205).
    /// </summary>
    private bool IsItemAlive(TileWorkItem item) =>
        item.ClaimantCount > 0 || _interestSet.Contains(item.CacheKey);

    /// <summary>
    /// True when a work token must be canceled once its last claimant leaves.
    /// Work is canceled only when the key is not held by the interest set.
    /// </summary>
    private bool ShouldCancelWhenNoClaimants(BackgroundTileCacheKey key) => !_interestSet.Contains(key);

    /// <summary>
    /// Number of queued entries that are not tombstoned by cancellation.
    /// </summary>
    private int LiveQueuedCount => Math.Max(0, _queue.Count - _removedKeys.Count);

    /// <summary>
    /// Computes the heap priority for a key from the current interest set.
    /// </summary>
    private TileWorkPriority ComputePriority(BackgroundTileCacheKey key)
    {
        var rank = _interestSet.IsVisible(key) ? 0
            : _interestSet.Contains(key) ? 1
            : 2;
        var squaredDistance = _interestSet.SquaredDistanceFromCenter is not null
            ? _interestSet.SquaredDistanceFromCenter(key)
            : 0d;
        var mipDistance = _interestSet.SelectedMipLevel is { } selected
            ? Math.Abs(key.MipLevel - selected)
            : 0;
        var sequence = _items.TryGetValue(key, out var item) ? item.Sequence : 0;
        return new TileWorkPriority(rank, squaredDistance, mipDistance, sequence);
    }

    /// <summary>
    /// Rebuilds the heap from live queued items in <c>_items</c>.
    /// Cancelled or orphaned entries are dropped. Priorities are recomputed
    /// for the current interest set; FIFO sequence is preserved.
    /// </summary>
    private void RebuildQueue()
    {
        var liveQueued = new List<(BackgroundTileCacheKey Key, TileWorkPriority Priority)>(_items.Count);
        foreach (var (key, item) in _items)
        {
            if (item.State != TileWorkItemState.Queued)
                continue;

            if (!IsItemAlive(item))
                continue;

            liveQueued.Add((key, ComputePriority(key)));
        }

        _queue.Clear();
        _removedKeys.Clear();
        foreach (var (key, priority) in liveQueued)
        {
            _queue.Enqueue(key, priority);
        }
    }

    private void DrainQueue()
    {
        DrainQueueWithLivenessCheck(CancellationToken.None);
    }

    /// <summary>
    /// Drains the priority queue and promotes queued items while slots are free.
    /// Queued items are already ordered by visibility class, center distance,
    /// and mip suitability (ICW-205), so no scan-ahead is needed.
    ///
    /// A queued item with no live claimants is canceled only when its key is
    /// not held by the published interest set. An in-interest-set item may
    /// have a momentary zero-claimant window between frame boundaries; the
    /// next frame re-claims the same key through coalescing (no-flash rule).
    ///
    /// The claimantToken parameter is provided for backward compatibility
    /// with the Phase 0 skeleton. The primary liveness check uses the item's
    /// ClaimantCount plus interest-set membership.
    /// </summary>
    public void DrainQueueWithLivenessCheck(CancellationToken claimantToken)
    {
        lock (_lock)
        {
            if (_disposed) return;

            while (_activeCount < _maxConcurrency && _queue.Count > 0)
            {
                var key = _queue.Dequeue();

                if (!_items.TryGetValue(key, out var item) || item.State != TileWorkItemState.Queued)
                {
                    // Orphaned heap entry (item removed or already promoted).
                    _removedKeys.Remove(key);
                    continue;
                }

                // Skip a queued entry canceled at this sequence. A later
                // re-admission of the same key has a new sequence and is live.
                if (_removedKeys.TryGetValue(key, out var canceledSequence)
                    && canceledSequence == item.Sequence)
                {
                    _removedKeys.Remove(key);
                    continue;
                }

                // If no live claimants remain and the key is not held by the
                // interest set, cancel and skip rather than promoting stale work.
                if (!IsItemAlive(item))
                {
                    CancelWorkItem(key, item);
                    continue;
                }

                _removedKeys.Remove(key);
                StartWorkItem(item);
            }
        }
    }

    /// <summary>
    /// Internal representation of a tile work item tracked by the coordinator.
    /// Holds the factory, state, claimant list, completion/failure callbacks,
    /// and a work-level CancellationTokenSource for cancellation.
    /// </summary>
    private sealed class TileWorkItem
    {
        private readonly List<ClaimantEntry> _claimants = new();
        private readonly Lock _claimantLock = new();
        private readonly CancellationTokenSource _workCts;
        private readonly ICacheReservation? _reservation;
        private readonly Func<BackgroundTileCacheKey, bool>? _cancelWhenNoClaimants;
        private int _running;
        private int _reservationDisposed;

        public TileWorkItem(
            BackgroundTileCacheKey cacheKey,
            Func<CancellationToken, ValueTask<byte[]>> factory,
            ICacheReservation? reservation,
            CancellationToken disposeToken,
            Func<BackgroundTileCacheKey, bool>? cancelWhenNoClaimants = null)
        {
            CacheKey = cacheKey;
            Factory = factory;
            _reservation = reservation;
            _workCts = CancellationTokenSource.CreateLinkedTokenSource(disposeToken);
            _cancelWhenNoClaimants = cancelWhenNoClaimants;
        }

        public BackgroundTileCacheKey CacheKey { get; }
        public Func<CancellationToken, ValueTask<byte[]>> Factory { get; }
        public TileWorkItemState State { get; set; } = TileWorkItemState.Queued;

        /// <summary>
        /// Monotonic FIFO sequence assigned at admission. Used as the final
        /// priority tie-break so equal-priority items drain in admission order.
        /// </summary>
        public long Sequence { get; set; }

        public void DisposeReservation()
        {
            if (Interlocked.Exchange(ref _reservationDisposed, 1) != 0)
            {
                return;
            }

            _reservation?.Dispose();
        }

        /// <summary>
        /// Token passed to the factory. Canceled when the last claimant is removed
        /// or the coordinator is disposed.
        /// </summary>
        public CancellationToken WorkToken => _workCts.Token;

        /// <summary>
        /// The number of current claimants. Thread-safe via the claimant lock.
        /// </summary>
        public int ClaimantCount
        {
            get { lock (_claimantLock) return _claimants.Count; }
        }

        /// <summary>
        /// Registers a claimant. If the claimant's token fires, the claimant is
        /// automatically removed. Returns true if newly added; false if duplicate.
        /// Duplicates are silently accepted because the claimant is already tracking.
        /// </summary>
        public void AddClaimant(
            object claimantId,
            CancellationToken claimantToken,
            Action<BackgroundTileCacheKey, byte[]>? onCompleted,
            Action<BackgroundTileCacheKey, Exception>? onFailed)
        {
            lock (_claimantLock)
            {
                // If already registered, refresh the registration and callbacks.
                // A multi-frame generation re-claims the same key every frame
                // with a fresh frame token (ICW-327). The old registration is
                // bound to a token the host already canceled; a spent
                // registration can never fire again, so without a refresh the
                // claimant becomes permanently uncancellable.
                var existing = _claimants.Find(c => c.Id.Equals(claimantId));
                if (existing is not null)
                {
                    existing.Registration?.Dispose();
                    if (claimantToken.CanBeCanceled)
                    {
                        var registration = claimantToken.Register(() => RemoveClaimant(claimantId));
                        var index = _claimants.FindIndex(c => c.Id.Equals(claimantId));
                        if (index >= 0)
                        {
                            _claimants[index] = _claimants[index] with
                            {
                                OnCompleted = onCompleted,
                                OnFailed = onFailed,
                                Registration = registration
                            };
                        }
                        else
                        {
                            // The token was already canceled; the callback ran
                            // synchronously (same-thread Lock reentrancy) and
                            // removed the claimant. Nothing to keep tracking;
                            // dispose the fired registration.
                            registration.Dispose();
                        }
                    }
                    else
                    {
                        _claimants[_claimants.IndexOf(existing)] = existing with
                        {
                            OnCompleted = onCompleted,
                            OnFailed = onFailed,
                            Registration = null
                        };
                    }

                    return;
                }

                // Add the claimant before registering the token callback
                // (ICW-320 F-014). Registering first lets a pre-canceled token
                // fire its callback synchronously before the claimant is in the
                // list; the removal is a no-op and a ghost claimant is left
                // behind that nothing ever removes.
                _claimants.Add(new ClaimantEntry(claimantId, onCompleted, onFailed, null));
                if (claimantToken.CanBeCanceled)
                {
                    var registration = claimantToken.Register(() => RemoveClaimant(claimantId));
                    var index = _claimants.FindIndex(c => c.Id.Equals(claimantId));
                    if (index >= 0)
                    {
                        _claimants[index] = _claimants[index] with { Registration = registration };
                    }
                    else
                    {
                        // The callback already ran synchronously (the token was
                        // already canceled) and removed the claimant. Nothing to
                        // keep tracking; dispose the fired registration.
                        registration.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Removes a claimant by ID. Returns true if found and removed.
        /// If no claimants remain, the work token is canceled.
        /// </summary>
        public bool RemoveClaimant(object claimantId)
        {
            lock (_claimantLock)
            {
                var idx = _claimants.FindIndex(c => c.Id.Equals(claimantId));
                if (idx < 0)
                    return false;

                var entry = _claimants[idx];
                entry.Registration?.Dispose();
                _claimants.RemoveAt(idx);

                // No-flash rule (ICW-205): cancel the work token only when the
                // key is not held by the published interest set. A visible
                // tile's fill survives its frame-token fire; the next frame
                // re-claims the same key through coalescing.
                if (_claimants.Count == 0
                    && (_cancelWhenNoClaimants is null || _cancelWhenNoClaimants(CacheKey)))
                {
                    CancelWork();
                }

                return true;
            }
        }

        /// <summary>
        /// Atomically attempts to mark this item as Running.
        /// Returns true if the transition from Queued to Running succeeded.
        /// </summary>
        public bool SetRunning()
        {
            return Interlocked.Exchange(ref _running, 1) == 0;
        }

        /// <summary>
        /// Non-mutating query: true when this item was already marked Running.
        /// Use this for read-only state checks (ICW-330). SetRunning() is a
        /// transition and must not be reused purely to query prior state.
        /// </summary>
        public bool IsRunning()
        {
            return Volatile.Read(ref _running) == 1;
        }

        /// <summary>
        /// Signals cancellation to the work token. Called when no claimants remain
        /// or the coordinator is shutting down.
        /// </summary>
        public void CancelWork()
        {
            try { _workCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Dispatches completion callbacks to all registered claimants.
        /// </summary>
        public void DispatchCompleted(byte[] pixels)
        {
            Action<BackgroundTileCacheKey, byte[]>?[] callbacks;
            lock (_claimantLock)
            {
                callbacks = _claimants
                    .Select(c => c.OnCompleted)
                    .Where(c => c is not null)
                    .Distinct()
                    .ToArray()!;
            }

            foreach (var cb in callbacks)
            {
                try { cb?.Invoke(CacheKey, pixels); }
                catch { }
            }
        }

        /// <summary>
        /// Dispatches failure callbacks to all registered claimants.
        /// </summary>
        public void DispatchFailed(Exception ex)
        {
            Action<BackgroundTileCacheKey, Exception>?[] callbacks;
            lock (_claimantLock)
            {
                callbacks = _claimants
                    .Select(c => c.OnFailed)
                    .Where(c => c is not null)
                    .Distinct()
                    .ToArray()!;
            }

            foreach (var cb in callbacks)
            {
                try { cb?.Invoke(CacheKey, ex); }
                catch { }
            }
        }

        /// <summary>
        /// Returns a snapshot of all registered claimant IDs.
        /// Used by PublishInterestSet to remove claimants for non-interest tiles.
        /// </summary>
        public object[] GetClaimantIds()
        {
            lock (_claimantLock)
            {
                return _claimants.Select(c => c.Id).ToArray();
            }
        }

        private sealed record ClaimantEntry(
            object Id,
            Action<BackgroundTileCacheKey, byte[]>? OnCompleted,
            Action<BackgroundTileCacheKey, Exception>? OnFailed,
            CancellationTokenRegistration? Registration);
    }
}
