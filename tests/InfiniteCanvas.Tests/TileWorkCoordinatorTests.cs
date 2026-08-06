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

    /// <summary>
    /// A factory that ignores the work token while waiting, then returns.
    /// It simulates a non-cooperative worker that keeps running after the
    /// coordinator requests cancellation (ICW-320 cancel-and-re-request
    /// window).
    /// </summary>
    private static Func<CancellationToken, ValueTask<byte[]>> NonCooperativeFactory(
        ManualResetEventSlim startSignal,
        ManualResetEventSlim releaseSignal,
        byte result = 42) =>
        async _ =>
        {
            startSignal.Set();
            releaseSignal.Wait();
            return [result];
        };

    /// <summary>
    /// A factory that runs until released, then observes the work token.
    /// It simulates a worker that stops (and faults) after a late release,
    /// exercising the HandleWorkStopped path after a re-request.
    /// </summary>
    private static Func<CancellationToken, ValueTask<byte[]>> ReleaseThenThrowIfCanceledFactory(
        ManualResetEventSlim startSignal,
        ManualResetEventSlim releaseSignal,
        byte result = 42) =>
        async token =>
        {
            startSignal.Set();
            releaseSignal.Wait();
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
    public void RunningWorkCanceled_ReRequest_AdmitsFreshItem()
    {
        // ICW-320 F-006: a scroll-away-and-back re-request during the cancel
        // window must admit fresh work instead of coalescing onto the canceled
        // running item. Fails on HEAD, where the re-request coalesces and the
        // fresh factory never runs.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started1 = new ManualResetEventSlim(false);
        var release1 = new ManualResetEventSlim(false);
        var freshStarted = new ManualResetEventSlim(false);

        // First request: a non-cooperative worker that ignores cancellation.
        coordinator.Request(Key1, NonCooperativeFactory(started1, release1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Cancel it mid-flight. The item stays in _items as Canceled until the
        // worker physically stops.
        coordinator.RemoveClaimant(Key1, ClaimantA);

        // Re-request while the old worker is still running. This must admit a
        // fresh item whose factory actually executes.
        coordinator.Request(Key1, NonCooperativeFactory(freshStarted, release1, result: 7), ClaimantB, CancellationToken.None);

        Assert.That(freshStarted.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(coordinator.GetCounters().AdmittedCount, Is.EqualTo(2));

        release1.Set();
    }

    [Test]
    public void LateWorkerStop_DoesNotRemoveNewerItem()
    {
        // ICW-320 F-007: a late old-worker stop must never remove or invalidate
        // the newer item for the same key. On HEAD, HandleWorkStopped removes by
        // key and clobbers the fresh running item, so a third request admits a
        // duplicate worker instead of coalescing.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started1 = new ManualResetEventSlim(false);
        var release1 = new ManualResetEventSlim(false);
        var started2 = new ManualResetEventSlim(false);
        var release2 = new ManualResetEventSlim(false);
        var started3 = new ManualResetEventSlim(false);

        // First request: a worker that faults (cancellation) after a late release.
        coordinator.Request(Key1, ReleaseThenThrowIfCanceledFactory(started1, release1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);
        coordinator.RemoveClaimant(Key1, ClaimantA);

        // Re-request: a fresh running item for the same key.
        coordinator.Request(Key1, NonCooperativeFactory(started2, release2), ClaimantB, CancellationToken.None);
        Assert.That(started2.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Let the old worker stop. Its HandleWorkStopped must not remove the
        // fresh item from coordinator tracking.
        release1.Set();
        Thread.Sleep(300);

        // A third request must coalesce onto the still-running fresh item, not
        // admit a duplicate worker.
        coordinator.Request(Key1, NonCooperativeFactory(started3, release2, result: 9), ClaimantA, CancellationToken.None);
        Assert.That(started3.Wait(TimeSpan.FromMilliseconds(400)), Is.False);
        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CoalescedCount, Is.EqualTo(1));
            Assert.That(counters.AdmittedCount, Is.EqualTo(2));
        }

        release2.Set();
    }

    [Test]
    public void PreCanceledToken_DoesNotLeaveGhostClaimant()
    {
        // ICW-320 F-014: a pre-canceled token must never leave a claimant
        // behind. On HEAD, AddClaimant registers the token callback before
        // adding the claimant, so the already-fired callback removes nothing
        // and the claimant sticks. The ghost keeps queued work alive; it is
        // promoted instead of canceled.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        using var preCanceled = new CancellationTokenSource();
        preCanceled.Cancel();

        // Fill the single slot so the second request is queued.
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Request(Key2, Factory(20), ClaimantB, preCanceled.Token);

        // When the slot opens, unclaimed queued work is canceled instead of
        // promoted and run. The queued item has no claimant callbacks, so the
        // observable is the coordinator counter, not an onFailed signal.
        hold1.Set();
        Thread.Sleep(500);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(1));
            Assert.That(counters.CompletedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ReCoalescedClaimant_RegistersNewestToken_CancelStopsWork()
    {
        // ICW-327: a multi-frame generation re-claims the same key each frame
        // with a fresh frame token. The re-coalesce path must refresh the
        // token registration. On HEAD it kept the registration on the already
        // fired (or not-yet-relevant) old token, so the newest token was never
        // registered and the claimant became permanently uncancellable.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        using var frame1 = new CancellationTokenSource();
        using var frame2 = new CancellationTokenSource();
        var started = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);

        // Frame 1 admits the work and generation starts.
        coordinator.Request(Key1, BlockingFactory(started, release), ClaimantA, frame1.Token);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Frame 2 re-claims the same key while generation is still running.
        // The re-coalesce path must register frame2's token.
        coordinator.Request(Key1, BlockingFactory(started, release), ClaimantA, frame2.Token);

        // Canceling the latest frame token must remove the claimant and cancel
        // the work. The interest set is empty, so no no-flash exemption holds.
        frame2.Cancel();

        // Release the worker so it observes its work token.
        release.Set();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => coordinator.GetCounters().CanceledCount,
                Is.EqualTo(1).After(2, 100),
                "Canceling the newest claimant token must cancel the work.");
            Assert.That(coordinator.GetCounters().CompletedCount, Is.EqualTo(0),
                "The work must not run to completion after the newest token fires.");
        }
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

    [Test]
    public void PriorityQueue_DrainsVisibleBeforePrefetch()
    {
        // ICW-205: with a published interest set, a visible queued item must
        // drain before a prefetch queued item, regardless of insertion order.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);
        var started2 = new ManualResetEventSlim(false);
        var hold2 = new ManualResetEventSlim(false);

        var completedOrder = new List<BackgroundTileCacheKey>();
        var orderLock = new object();
        Action<BackgroundTileCacheKey, byte[]> record = (key, _) =>
        {
            lock (orderLock) completedOrder.Add(key);
        };

        // Key1: active filler that blocks.
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None,
            onCompleted: record);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Key2: prefetch, blocks if promoted. Key3: visible, completes fast.
        coordinator.Request(Key2, BlockingFactory(started2, hold2), ClaimantB, CancellationToken.None,
            onCompleted: record);
        coordinator.Request(Key3, Factory(66), ClaimantA, CancellationToken.None,
            onCompleted: record);

        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { Key1, Key3 },
            new HashSet<BackgroundTileCacheKey> { Key2 }));

        // Release Key1. Drain must promote visible Key3 before prefetch Key2.
        hold1.Set();
        Thread.Sleep(500);

        // Release the prefetch filler so the test can finish.
        hold2.Set();
        Thread.Sleep(500);

        lock (orderLock)
        {
            Assert.That(completedOrder, Is.EqualTo(new[] { Key1, Key3, Key2 }));
        }
    }

    [Test]
    public void PriorityQueue_OrdersVisibleByCenterDistance()
    {
        // ICW-205: two visible keys must drain closest-first using the
        // squared-distance provider, not insertion order.
        var nearKey = new BackgroundTileCacheKey("source-a", "tile-near", 1, 0);
        var farKey = new BackgroundTileCacheKey("source-a", "tile-far", 1, 0);

        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        var completedOrder = new List<BackgroundTileCacheKey>();
        var orderLock = new object();
        Action<BackgroundTileCacheKey, byte[]> record = (key, _) =>
        {
            lock (orderLock) completedOrder.Add(key);
        };

        // Fill the single slot with a blocking item.
        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None,
            onCompleted: record);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue nearKey then farKey. Both visible. farKey is admitted second
        // but is farther from the center, so it must drain last.
        coordinator.Request(farKey, Factory(21), ClaimantB, CancellationToken.None, onCompleted: record);
        coordinator.Request(nearKey, Factory(22), ClaimantB, CancellationToken.None, onCompleted: record);

        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { nearKey, farKey },
            new HashSet<BackgroundTileCacheKey>(),
            centerX: 0,
            centerY: 0,
            selectedMipLevel: 0,
            squaredDistanceFromCenter: key => key.TileId == "tile-near" ? 1d : 100d));

        hold.Set();
        Thread.Sleep(500);

        lock (orderLock)
        {
            Assert.That(completedOrder, Is.EqualTo(new[] { Key1, nearKey, farKey }));
        }
    }

    [Test]
    public void PriorityQueue_MipSuitabilityBreaksDistanceTie()
    {
        // ICW-205: two visible keys at equal distance must drain the mip
        // closest to the selected mip level first.
        var keyMip0 = new BackgroundTileCacheKey("source-a", "tile-mip0", 1, 0);
        var keyMip1 = new BackgroundTileCacheKey("source-a", "tile-mip1", 1, 1);

        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        var completedOrder = new List<BackgroundTileCacheKey>();
        var orderLock = new object();
        Action<BackgroundTileCacheKey, byte[]> record = (key, _) =>
        {
            lock (orderLock) completedOrder.Add(key);
        };

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None,
            onCompleted: record);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // keyMip0 (mip 0) admitted before keyMip1 (mip 1). Equal distance.
        coordinator.Request(keyMip0, Factory(10), ClaimantB, CancellationToken.None, onCompleted: record);
        coordinator.Request(keyMip1, Factory(30), ClaimantB, CancellationToken.None, onCompleted: record);

        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { keyMip0, keyMip1 },
            new HashSet<BackgroundTileCacheKey>(),
            centerX: 0,
            centerY: 0,
            selectedMipLevel: 1,
            squaredDistanceFromCenter: _ => 0d));

        hold.Set();
        Thread.Sleep(500);

        lock (orderLock)
        {
            Assert.That(completedOrder, Is.EqualTo(new[] { Key1, keyMip1, keyMip0 }));
        }
    }

    [Test]
    public void PriorityQueue_NullSchedulingContext_PreservesFifoWithinClass()
    {
        // ICW-205: with no distance provider or selected mip, equal-class
        // items must drain in admission (FIFO) order.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        var completedOrder = new List<BackgroundTileCacheKey>();
        var orderLock = new object();
        Action<BackgroundTileCacheKey, byte[]> record = (key, _) =>
        {
            lock (orderLock) completedOrder.Add(key);
        };

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None,
            onCompleted: record);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.Request(Key2, Factory(20), ClaimantB, CancellationToken.None, onCompleted: record);
        coordinator.Request(Key3, Factory(30), ClaimantB, CancellationToken.None, onCompleted: record);

        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { Key2, Key3 },
            new HashSet<BackgroundTileCacheKey>()));

        hold.Set();
        Thread.Sleep(500);

        lock (orderLock)
        {
            Assert.That(completedOrder, Is.EqualTo(new[] { Key1, Key2, Key3 }));
        }
    }

    [Test]
    public void VisibleInFlightWork_SurvivesClaimantTokenFire()
    {
        // No-flash rule (ICW-205): a running item whose key is in the published
        // interest set must NOT be canceled when its claimant token fires. The
        // next frame re-claims the same key and generation completes once.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        using var claimantCts = new CancellationTokenSource();
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, claimantCts.Token);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Key1 is still visible. Publish an interest set that includes it.
        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { Key1 },
            new HashSet<BackgroundTileCacheKey>()));

        // Frame boundary: the previous frame's token fires.
        claimantCts.Cancel();
        Thread.Sleep(500);

        // The work must still be running, not canceled.
        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(0));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
        }

        // The next frame re-claims the same key (coalesce) and the fill
        // completes exactly once.
        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantB, CancellationToken.None);
        hold.Set();
        Assert.That(() => coordinator.GetCounters().CompletedCount,
            Is.EqualTo(1).After(2, 100));
        Assert.That(coordinator.GetCounters().CanceledCount, Is.EqualTo(0));
    }

    [Test]
    public void NonVisibleInFlightWork_CanceledOnClaimantTokenFire()
    {
        // No-flash rule (ICW-205): a running item whose key is NOT in the
        // published interest set must be canceled when its claimant token fires.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        using var claimantCts = new CancellationTokenSource();
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, claimantCts.Token);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Key1 is NOT in the interest set (empty).
        coordinator.PublishInterestSet(ViewportInterestSet.Empty);

        claimantCts.Cancel();
        hold.Set();
        Thread.Sleep(500);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(1));
            Assert.That(counters.ActiveCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void RemoveClaimant_InterestHeld_DoesNotCancelWork()
    {
        // No-flash rule (ICW-205): removing the last claimant of a running
        // item whose key is in the interest set must not cancel the fill.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        coordinator.PublishInterestSet(new ViewportInterestSet(
            new HashSet<BackgroundTileCacheKey> { Key1 },
            new HashSet<BackgroundTileCacheKey>()));

        coordinator.RemoveClaimant(Key1, ClaimantA);

        var counters = coordinator.GetCounters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(0));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
        }

        hold.Set();
        Assert.That(() => coordinator.GetCounters().CompletedCount,
            Is.EqualTo(1).After(2, 100));
        Assert.That(coordinator.GetCounters().CanceledCount, Is.EqualTo(0));
    }

    [Test]
    public void QueuedCancellation_ReAdmittedSameKey_IsNotSkipped()
    {
        // ICW-205: canceling a queued item and re-requesting the same cache
        // key must admit a fresh item. The cancellation tombstone is scoped
        // to the canceled item's sequence, so the new item is not skipped.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started1 = new ManualResetEventSlim(false);
        var hold1 = new ManualResetEventSlim(false);

        byte[]? requeuedResult = null;
        coordinator.Request(Key1, BlockingFactory(started1, hold1), ClaimantA, CancellationToken.None);
        Assert.That(started1.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue Key2, then cancel it before it runs.
        coordinator.Request(Key2, Factory(20), ClaimantB, CancellationToken.None);
        coordinator.RemoveClaimant(Key2, ClaimantB);

        // Re-request the same key. It must be admitted as a fresh item.
        var admitted = coordinator.Request(Key2, Factory(55), ClaimantA, CancellationToken.None,
            onCompleted: (_, p) => requeuedResult = p);
        Assert.That(admitted, Is.True);

        // Release the slot. The fresh Key2 must be promoted and complete.
        hold1.Set();
        Assert.That(() => coordinator.GetCounters().CompletedCount,
            Is.EqualTo(2).After(2, 100));

        Assert.That(requeuedResult, Is.Not.Null);
        Assert.That(requeuedResult![0], Is.EqualTo(55));
        Assert.That(coordinator.GetCounters().QueuedCount, Is.EqualTo(0));
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
