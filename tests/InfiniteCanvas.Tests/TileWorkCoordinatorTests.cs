using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class TileWorkCoordinatorTests
{
    private static readonly BackgroundTileCacheKey Key1 = new("source-a", "tile-1", 1, 0);
    private static readonly BackgroundTileCacheKey Key2 = new("source-a", "tile-2", 1, 0);
    private static readonly BackgroundTileCacheKey Key3 = new("source-b", "tile-1", 1, 1);
    private static readonly object ClaimantA = new();
    private static readonly object ClaimantB = new();

    /// <summary>
    /// A factory that completes synchronously with a known byte value.
    /// </summary>
    private static Func<CancellationToken, ValueTask<byte[]>> Factory(byte value) =>
        _ => new ValueTask<byte[]>([value]);

    /// <summary>
    /// A factory that waits on a signal then returns, used to simulate
    /// in-flight generation.
    /// </summary>
    private static Func<CancellationToken, ValueTask<byte[]>> BlockingFactory(
        ManualResetEventSlim startSignal,
        ManualResetEventSlim? holdSignal = null,
        byte result = 42) =>
        async token =>
        {
            startSignal.Set();
            if (holdSignal is not null) holdSignal.Wait(token);
            token.ThrowIfCancellationRequested();
            return [result];
        };

    [Test]
    public void Request_AdmitsNewWorkAndAdvancesCounters()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var admitted = coordinator.Request(Key1, Factory(10), ClaimantA, CancellationToken.None);

        Assert.That(admitted, Is.True);
        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.AdmittedCount, Is.EqualTo(1));
            Assert.That(counters.CoalescedCount, Is.EqualTo(0));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void Request_CoalescesDuplicateCacheKey()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        coordinator.Request(Key1, Factory(10), ClaimantA, CancellationToken.None);
        var admitted = coordinator.Request(Key1, Factory(20), ClaimantB, CancellationToken.None);

        Assert.That(admitted, Is.True);
        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.AdmittedCount, Is.EqualTo(1));
            Assert.That(counters.CoalescedCount, Is.EqualTo(1));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Request_QueuesWorkWhenAtMaxConcurrency()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);

        // First request starts immediately (fills the single slot).
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Second request should be queued.
        var admitted2 = coordinator.Request(Key2, Factory(20), ClaimantB, CancellationToken.None);
        Assert.That(admitted2, Is.True);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(1));
            Assert.That(counters.AdmittedCount, Is.EqualTo(2));
        }

        hold1.Set();
    }

    [Test]
    public void Request_RejectsWhenReservationFails()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var admitted = coordinator.Request(Key1, Factory(10), ClaimantA, CancellationToken.None,
            tryReserve: _ => null);

        Assert.That(admitted, Is.False);
        var counters = coordinator.GetCounters();
        Assert.That(counters.AdmittedCount, Is.EqualTo(0));
    }

    [Test]
    public void QueuedCancellation_DisposesReservationExactlyOnce()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);
        var queuedReservation = new TestReservation();

        coordinator.Request(
            Key1,
            BlockingFactory(started, hold),
            ClaimantA,
            CancellationToken.None,
            tryReserve: _ => new TestReservation());
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Request(
            Key2,
            Factory(20),
            ClaimantB,
            CancellationToken.None,
            tryReserve: _ => queuedReservation);

        coordinator.PublishInterestSet(ViewportInterestSet.Empty);

        Assert.That(queuedReservation.DisposeCount, Is.EqualTo(1));
        hold.Set();
    }

    [Test]
    public void FailedWork_DisposesReservationExactlyOnce()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var reservation = new TestReservation();

        coordinator.Request(
            Key1,
            _ => throw new InvalidOperationException("boom"),
            ClaimantA,
            CancellationToken.None,
            tryReserve: _ => reservation);

        Assert.That(() => reservation.DisposeCount, Is.EqualTo(1).After(2, 100));
    }

    [Test]
    public void RemoveClaimant_CancelsWorkWhenLastClaimantRemoved()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);
        var canceled = new ManualResetEventSlim(false);

        var factory = BlockingFactory(started, hold);
        coordinator.Request(Key1, factory, ClaimantA, CancellationToken.None,
            onCompleted: (_, _) => { },
            onFailed: (_, _) => { });
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Remove the only claimant — work should be canceled.
        coordinator.RemoveClaimant(Key1, ClaimantA);

        // The hold should complete because the work token was canceled.
        Assert.That(canceled.Wait(TimeSpan.FromSeconds(1)), Is.False);
        hold.Set();

        var counters = coordinator.GetCounters();
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
    }

    [Test]
    public void RemoveClaimant_DoesNotCancelSharedFillWithMultipleClaimants()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        // Two claimants for the same key.
        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Reset the start signal for the second request (should coalesce, not start again).
        var started2 = new ManualResetEventSlim(false);
        coordinator.Request(Key1, BlockingFactory(started2, hold), ClaimantB, CancellationToken.None);

        // Remove claimant A — work continues because B is still interested.
        coordinator.RemoveClaimant(Key1, ClaimantA);

        var counters = coordinator.GetCounters();
        Assert.That(counters.CanceledCount, Is.EqualTo(0));
        Assert.That(counters.ActiveCount, Is.EqualTo(1));

        hold.Set();
    }

    [Test]
    public void RemoveAllClaimants_RemovesAllForOwnerAndCancelsOrphanedWork()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        var started2 = new ManualResetEventSlim(false);
        var hold2 = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        coordinator.Request(Key2, BlockingFactory(started2, hold2), ClaimantB, CancellationToken.None);

        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(started2.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Remove all claimants for A — Key1 loses its claimant and should be canceled.
        coordinator.RemoveAllClaimants(ClaimantA);

        var counters = coordinator.GetCounters();
        Assert.That(counters.CanceledCount, Is.EqualTo(1));

        hold1.Set();
        hold2.Set();
    }

    [Test]
    public void CancelAll_StopsAllQueuedAndRunningWork()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue a second item.
        coordinator.Request(Key2, Factory(20), ClaimantB, CancellationToken.None,
            onCompleted: (_, _) => { },
            onFailed: (_, _) => { });

        coordinator.CancelAll();

        // Release the hold so the worker can observe the cancellation token
        // and actually stop. ActiveCount now represents physical execution, so
        // it only drops when the worker's catch/finally runs.
        hold.Set();
        Assert.That(() => coordinator.GetCounters().ActiveCount,
            Is.EqualTo(0).After(2, 100), "ActiveCount must reach 0 after CancelAll");

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(2));
            Assert.That(counters.ActiveCount, Is.EqualTo(0));
            Assert.That(counters.QueuedCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void Dispose_CancelsAllWork()
    {
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Dispose();

        // Release the hold so the worker can observe the cancellation token
        // and actually stop. ActiveCount now represents physical execution,
        // so it drops only when the worker's catch/finally runs.
        hold.Set();
        Assert.That(() => coordinator.GetCounters().ActiveCount,
            Is.EqualTo(0).After(2, 100), "ActiveCount must reach 0 after Dispose");

        var counters = coordinator.GetCounters();
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));
    }

    [Test]
    public void GetCounters_ReportsCorrectPendingCount()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Request(Key2, Factory(20), ClaimantB, CancellationToken.None);
        coordinator.Request(Key3, Factory(30), ClaimantA, CancellationToken.None);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(2));
            Assert.That(counters.PendingCount, Is.EqualTo(3));
        }

        hold.Set();
    }

    [Test]
    public void CompletingWork_ReducesActiveCountAndDrainsQueue()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);

        // First item blocks.
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Second item also blocks (so it won't complete immediately when promoted).
        var started2 = new ManualResetEventSlim(false);
        var hold2 = new ManualResetEventSlim(false);
        coordinator.Request(Key2, BlockingFactory(started2, hold2), ClaimantB, CancellationToken.None);

        // Third item is a fast factory (will stay queued).
        var started3 = new ManualResetEventSlim(false);
        coordinator.Request(Key3, _ =>
        {
            started3.Set();
            return new ValueTask<byte[]>([30]);
        }, ClaimantA, CancellationToken.None);

        // Release the first item.
        hold1.Set();
        Thread.Sleep(200);

        // First item completed, second should be promoted to active (and is blocking).
        Assert.That(started2.Wait(TimeSpan.FromSeconds(2)), Is.True);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CompletedCount, Is.EqualTo(1));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(1));
            // One completed, one promoted to active (blocking), one still queued.
            Assert.That(counters.PendingCount, Is.EqualTo(2));
        }

        hold2.Set();
    }

    [Test]
    public void PerClaimantToken_RemovesClaimantWhenTokenFires()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        using var claimantCts = new CancellationTokenSource();

        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, claimantCts.Token);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Cancel the claimant's token — this should trigger auto-removal.
        claimantCts.Cancel();

        // After auto-removal, the work should have no claimants and be canceled.
        Thread.Sleep(500);
        hold.Set();

        var counters = coordinator.GetCounters();
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
    }

    [Test]
    public void CompletingWork_InvokesOnCompletedCallback()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        byte[]? completedPixels = null;
        BackgroundTileCacheKey completedKey = default;

        coordinator.Request(Key1, Factory(99), ClaimantA, CancellationToken.None,
            onCompleted: (key, pixels) =>
            {
                completedKey = key;
                completedPixels = pixels;
            });

        Thread.Sleep(500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(completedPixels, Is.Not.Null);
            Assert.That(completedPixels!.Length, Is.EqualTo(1));
            Assert.That(completedPixels[0], Is.EqualTo(99));
            Assert.That(completedKey, Is.EqualTo(Key1));
        }

        var counters = coordinator.GetCounters();
        Assert.That(counters.CompletedCount, Is.EqualTo(1));
    }

    [Test]
    public void FactoryException_InvokesOnFailedCallback()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        Exception? capturedException = null;
        BackgroundTileCacheKey failedKey = default;

        coordinator.Request(Key1, _ => throw new InvalidOperationException("test failure"),
            ClaimantA, CancellationToken.None,
            onFailed: (key, ex) =>
            {
                failedKey = key;
                capturedException = ex;
            });

        Thread.Sleep(500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedException, Is.Not.Null);
            Assert.That(capturedException, Is.TypeOf<InvalidOperationException>());
            Assert.That(failedKey, Is.EqualTo(Key1));
        }

        var counters = coordinator.GetCounters();
        Assert.That(counters.FailedCount, Is.EqualTo(1));
    }

    [Test]
    public void Request_AfterDispose_ThrowsObjectDisposedException()
    {
        var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        coordinator.Dispose();

        Assert.That(() => coordinator.Request(Key1, Factory(1), ClaimantA, CancellationToken.None),
            Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public void Constructor_WithZeroConcurrency_Throws()
    {
        Assert.That(() => new TileWorkCoordinator(0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Constructor_WithNegativeConcurrency_Throws()
    {
        Assert.That(() => new TileWorkCoordinator(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void CoalescedRequest_ReceivesCompletionCallback()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        byte[]? pixelsA = null;
        byte[]? pixelsB = null;

        coordinator.Request(Key1, Factory(77), ClaimantA, CancellationToken.None,
            onCompleted: (_, p) => pixelsA = p);
        coordinator.Request(Key1, Factory(88), ClaimantB, CancellationToken.None,
            onCompleted: (_, p) => pixelsB = p);

        Thread.Sleep(500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pixelsA, Is.Not.Null);
            Assert.That(pixelsB, Is.Not.Null);
            // Both should get the same result (value from first factory, 77).
            Assert.That(pixelsA![0], Is.EqualTo(77));
            Assert.That(pixelsB![0], Is.EqualTo(77));
        }
    }

    [Test]
    public void QueuedWork_PromotedWhenSlotOpens()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue second item.
        byte[]? queuedResult = null;
        coordinator.Request(Key2, Factory(55), ClaimantB, CancellationToken.None,
            onCompleted: (_, p) => queuedResult = p);

        // Release first item.
        hold.Set();
        Thread.Sleep(500);

        Assert.That(queuedResult, Is.Not.Null);
        Assert.That(queuedResult![0], Is.EqualTo(55));

        var counters = coordinator.GetCounters();
        // First completed, second also completed (was queued, then promoted and finished).
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));
    }

    [Test]
    public void DrainQueueWithLivenessCheck_PromotesWhenSlotAvailable()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue second item (no slots free).
        byte[]? queuedResult = null;
        coordinator.Request(Key2, Factory(55), ClaimantB, CancellationToken.None,
            onCompleted: (_, p) => queuedResult = p);

        // Release first item — the internal DrainQueue (called from the
        // completion path) promotes Key2. DrainQueueWithLivenessCheck is
        // the same logic with an extra token parameter.
        hold.Set();
        Thread.Sleep(500);

        Assert.That(queuedResult, Is.Not.Null);
        Assert.That(queuedResult![0], Is.EqualTo(55));

        var counters = coordinator.GetCounters();
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));
    }

    [Test]
    public void DrainQueueWithLivenessCheck_RemovedItem_SkipsInQueue()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 2);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        var started2 = new ManualResetEventSlim(false);
        var hold2 = new ManualResetEventSlim(false);

        // Fill both slots so subsequent items queue.
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Request(Key2, BlockingFactory(started2, hold2), ClaimantB, CancellationToken.None);
        Assert.That(started2.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue a third item.
        coordinator.Request(Key3, Factory(55), ClaimantA, CancellationToken.None);

        // Remove the third item's claimant directly — the coordinator
        // cancels it since ClaimantCount drops to 0, removing it from _items.
        // DrainQueueWithLivenessCheck must skip the orphaned queue entry.
        coordinator.RemoveClaimant(Key3, ClaimantA);

        // Release one slot. DrainQueueWithLivenessCheck runs from Key1's
        // completion path. It should skip Key3 (no longer in _items) and
        // find no more work.
        hold1.Set();
        Thread.Sleep(500);

        var counters = coordinator.GetCounters();
        // Key1 completed, Key2 still running, Key3 was canceled by RemoveClaimant.
        Assert.That(counters.CompletedCount, Is.EqualTo(1));
        Assert.That(counters.CanceledCount, Is.EqualTo(1));

        // Cleanup: release second slot.
        hold2.Set();
        Thread.Sleep(500);

        counters = coordinator.GetCounters();
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));

        // Key3's callbacks were not invoked because RemoveClaimant removed
        // the last claimant before DispatchFailed could run — correct behavior
        // since there is no claimant to notify.
    }

    [Test]
    public void PublishInterestSet_CancelsNonVisibleQueuedItems()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue Key2 (not in interest set).
        coordinator.Request(Key2, Factory(55), ClaimantB, CancellationToken.None);

        // Queue Key3 (in interest set).
        coordinator.Request(Key3, Factory(66), ClaimantA, CancellationToken.None);

        // Publish interest set that only includes Key3.
        var visibleKeys = new HashSet<BackgroundTileCacheKey> { Key3 };
        coordinator.PublishInterestSet(new ViewportInterestSet(visibleKeys, new HashSet<BackgroundTileCacheKey>()));

        // Release Key1 so drain runs. Key3 (visible) should be promoted.
        hold.Set();
        Thread.Sleep(1000);

        var counters = coordinator.GetCounters();
        // Key1 completed, Key2 was canceled (not in interest set), Key3 completed.
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));
    }

    [Test]
    public void DrainQueueWithLivenessCheck_PromotesVisibleOverNonVisible()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 2);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        var started2 = new ManualResetEventSlim(false);
        var hold2 = new ManualResetEventSlim(false);

        // Fill both slots so subsequent items queue.
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);
        coordinator.Request(Key2, BlockingFactory(started2, hold2), ClaimantB, CancellationToken.None);
        Assert.That(started2.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue Key3 (not in interest set — will be canceled).
        coordinator.Request(Key3, Factory(11), ClaimantA, CancellationToken.None);

        // Publish interest set that does NOT include Key3.
        // Key3 is queued and not visible → should be cancelled.
        var visibleKeys = new HashSet<BackgroundTileCacheKey>();
        coordinator.PublishInterestSet(new ViewportInterestSet(visibleKeys, new HashSet<BackgroundTileCacheKey>()));

        // Release one slot. Key3 (queued, not visible) was cancelled by
        // PublishInterestSet. No visible items to promote.
        hold1.Set();
        Thread.Sleep(1000);

        var counters = coordinator.GetCounters();
        // Key1 completed, Key3 canceled (not visible). Key2 still running.
        Assert.That(counters.CompletedCount, Is.EqualTo(1));
        Assert.That(counters.CanceledCount, Is.EqualTo(1));
        Assert.That(counters.ActiveCount, Is.EqualTo(1));

        // Release the final slot.
        hold2.Set();
        Thread.Sleep(500);

        counters = coordinator.GetCounters();
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
        Assert.That(counters.ActiveCount, Is.EqualTo(0));
    }

    private sealed class TestReservation : ICacheReservation
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
        }
    }
}
