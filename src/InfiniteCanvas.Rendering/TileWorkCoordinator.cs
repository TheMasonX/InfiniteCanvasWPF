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
    int FailedCount,
    int ReservationReleases)
{
    public int TotalCount => AdmittedCount + CoalescedCount;

    public int PendingCount => ActiveCount + QueuedCount;

    public override string ToString() =>
        $"Active {ActiveCount}  Queued {QueuedCount}  |  " +
        $"Admitted {AdmittedCount}  Coalesced {CoalescedCount}  |  " +
        $"Completed {CompletedCount}  Canceled {CanceledCount}  Failed {FailedCount}  |  " +
        $"ResReleases {ReservationReleases}";
}

/// <summary>
/// Bounded, deduplicated, cancellable coordinator for background tile generation work.
/// Manages concurrency, coalesces equal cache-key requests, separates claimant interest
/// from shared-fill ownership, and exposes structured diagnostic counters.
/// </summary>
/// <remarks>
/// This is the foundation for ICW-142 (bounded cancellable tile materialization).
/// ICW-143 adds viewport interest snapshots and priority ordering.
///
/// Design rules (from ADR-0006):
/// - A frame's viewport update publishes an interest snapshot; only the claimants
///   (current visible frames) own the request.
/// - Cancellation of a stale claimant removes its interest but *must not* cancel
///   the underlying generation if another claimant still needs it.
/// - Cache reservations are acquired at admission and released exactly once on
///   cancellation, failure, or rejected admission.
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
    private readonly Queue<BackgroundTileCacheKey> _queue = new();
    private int _activeCount;

    // Diagnostic counters (interlocked for lock-free reads).
    private int _admittedCount;
    private int _coalescedCount;
    private int _completedCount;
    private int _canceledCount;
    private int _failedCount;
    private int _reservationReleases;

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
    /// <param name="tryReserve">Optional reservation function that must return true for the work to be admitted.
    /// Released on cancellation, failure, or rejected admission.</param>
    /// <returns>True if the request was admitted or coalesced; false if the reservation was rejected.</returns>
    public bool Request(
        BackgroundTileCacheKey key,
        Func<CancellationToken, ValueTask<byte[]>> factory,
        object claimantId,
        CancellationToken claimantToken,
        Action<BackgroundTileCacheKey, byte[]>? onCompleted = null,
        Action<BackgroundTileCacheKey, Exception>? onFailed = null,
        Func<bool>? tryReserve = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(claimantId);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // If the key already has a work item, coalesce — add claimant, don't start a new fill.
            if (_items.TryGetValue(key, out var existing))
            {
                existing.AddClaimant(claimantId, claimantToken, onCompleted, onFailed);
                Interlocked.Increment(ref _coalescedCount);
                Log.Debug("CoordReq COALESCE {SourceId}/{TileId} mip{MipLevel} rev{Rev} claimant={Claimant} active={Active} queued={Queued}",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, claimantId, _activeCount, _queue.Count);
                return true;
            }

            // Attempt reservation before admission.
            if (tryReserve is not null && !tryReserve())
            {
                Log.Warning("CoordReq REJECTED {SourceId}/{TileId} mip{MipLevel} rev{Rev} — reservation failed (budget full/no evictable tiles)",
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision);
                return false;
            }

            var item = new TileWorkItem(key, factory, _disposeCts.Token);
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
                    key.SourceId, key.TileId, key.MipLevel, key.ContentRevision, _queue.Count);
                _queue.Enqueue(key);
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

            // Cancel the work only if no claimants remain.
            if (item.ClaimantCount == 0)
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

            // Collect keys whose last claimant was removed.
            var toCancel = new List<(BackgroundTileCacheKey Key, TileWorkItem Item)>();
            foreach (var (key, item) in _items)
            {
                if (item.RemoveClaimant(claimantId) && item.ClaimantCount == 0)
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
                QueuedCount: _queue.Count,
                AdmittedCount: Volatile.Read(ref _admittedCount),
                CoalescedCount: Volatile.Read(ref _coalescedCount),
                CompletedCount: Volatile.Read(ref _completedCount),
                CanceledCount: Volatile.Read(ref _canceledCount),
                FailedCount: Volatile.Read(ref _failedCount),
                ReservationReleases: Volatile.Read(ref _reservationReleases));
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
            _items.Remove(item.CacheKey);
            ReleaseReservation(item.CacheKey);
        }
    }

    private void CancelWorkItem(BackgroundTileCacheKey key, TileWorkItem item)
    {
        if (item.State is TileWorkItemState.Completed or TileWorkItemState.Failed or TileWorkItemState.Canceled)
            return;

        var wasRunning = !item.SetRunning() && _activeCount > 0;
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
            RemoveFromQueue(key);
            item.DispatchFailed(new OperationCanceledException(
                "Tile work was canceled before execution"));
        }

        _items.Remove(key);
        ReleaseReservation(key);
    }

    private void DrainQueue()
    {
        lock (_lock)
        {
            while (_activeCount < _maxConcurrency && _queue.Count > 0)
            {
                var key = _queue.Dequeue();
                if (_items.TryGetValue(key, out var item) && item.State == TileWorkItemState.Queued)
                {
                    StartWorkItem(item);
                }
            }
        }
    }

    private void RemoveFromQueue(BackgroundTileCacheKey key)
    {
        // Rebuild the queue excluding the canceled key.
        var remaining = new List<BackgroundTileCacheKey>(_queue.Count);
        while (_queue.Count > 0)
        {
            var k = _queue.Dequeue();
            if (!k.Equals(key))
                remaining.Add(k);
        }

        foreach (var k in remaining)
        {
            _queue.Enqueue(k);
        }
    }

    private void ReleaseReservation(BackgroundTileCacheKey key)
    {
        Interlocked.Increment(ref _reservationReleases);
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
        private int _running;

        public TileWorkItem(
            BackgroundTileCacheKey cacheKey,
            Func<CancellationToken, ValueTask<byte[]>> factory,
            CancellationToken disposeToken)
        {
            CacheKey = cacheKey;
            Factory = factory;
            _workCts = CancellationTokenSource.CreateLinkedTokenSource(disposeToken);
        }

        public BackgroundTileCacheKey CacheKey { get; }
        public Func<CancellationToken, ValueTask<byte[]>> Factory { get; }
        public TileWorkItemState State { get; set; } = TileWorkItemState.Queued;

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
                // If already registered, just update callbacks.
                var existing = _claimants.Find(c => c.Id.Equals(claimantId));
                if (existing is not null)
                {
                    // Update callbacks for the existing claimant.
                    _claimants[_claimants.IndexOf(existing)] = existing with
                    {
                        OnCompleted = onCompleted,
                        OnFailed = onFailed
                    };
                    return;
                }

                // Register a callback that removes this claimant if its token fires.
                CancellationTokenRegistration? registration = null;
                if (claimantToken.CanBeCanceled)
                {
                    registration = claimantToken.Register(() => RemoveClaimant(claimantId));
                }

                _claimants.Add(new ClaimantEntry(claimantId, onCompleted, onFailed, registration));
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

                if (_claimants.Count == 0)
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
                callbacks = _claimants.Select(c => c.OnCompleted).ToArray();
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
                callbacks = _claimants.Select(c => c.OnFailed).ToArray();
            }

            foreach (var cb in callbacks)
            {
                try { cb?.Invoke(CacheKey, ex); }
                catch { }
            }
        }

        private sealed record ClaimantEntry(
            object Id,
            Action<BackgroundTileCacheKey, byte[]>? OnCompleted,
            Action<BackgroundTileCacheKey, Exception>? OnFailed,
            CancellationTokenRegistration? Registration);
    }
}
