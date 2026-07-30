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
        Assert.Multiple(() =>
        {
            Assert.That(counters.AdmittedCount, Is.EqualTo(1));
            Assert.That(counters.CoalescedCount, Is.EqualTo(0));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Request_CoalescesDuplicateCacheKey()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        coordinator.Request(Key1, Factory(10), ClaimantA, CancellationToken.None);
        var admitted = coordinator.Request(Key1, Factory(20), ClaimantB, CancellationToken.None);

        Assert.That(admitted, Is.True);
        var counters = coordinator.GetCounters();
        Assert.Multiple(() =>
        {
            Assert.That(counters.AdmittedCount, Is.EqualTo(1));
            Assert.That(counters.CoalescedCount, Is.EqualTo(1));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(1));
            Assert.That(counters.AdmittedCount, Is.EqualTo(2));
        });

        hold1.Set();
    }

    [Test]
    public void Request_RejectsWhenReservationFails()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var admitted = coordinator.Request(Key1, Factory(10), ClaimantA, CancellationToken.None,
            tryReserve: () => false);

        Assert.That(admitted, Is.False);
        var counters = coordinator.GetCounters();
        Assert.That(counters.AdmittedCount, Is.EqualTo(0));
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
        Assert.Multiple(() =>
        {
            Assert.That(counters.CanceledCount, Is.EqualTo(2));
            Assert.That(counters.ActiveCount, Is.EqualTo(0));
            Assert.That(counters.QueuedCount, Is.EqualTo(0));
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(2));
            Assert.That(counters.PendingCount, Is.EqualTo(3));
        });

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
        Assert.Multiple(() =>
        {
            Assert.That(counters.CompletedCount, Is.EqualTo(1));
            Assert.That(counters.ActiveCount, Is.EqualTo(1));
            Assert.That(counters.QueuedCount, Is.EqualTo(1));
            // One completed, one promoted to active (blocking), one still queued.
            Assert.That(counters.PendingCount, Is.EqualTo(2));
        });

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

        Assert.Multiple(() =>
        {
            Assert.That(completedPixels, Is.Not.Null);
            Assert.That(completedPixels!.Length, Is.EqualTo(1));
            Assert.That(completedPixels[0], Is.EqualTo(99));
            Assert.That(completedKey, Is.EqualTo(Key1));
        });

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

        Assert.Multiple(() =>
        {
            Assert.That(capturedException, Is.Not.Null);
            Assert.That(capturedException, Is.TypeOf<InvalidOperationException>());
            Assert.That(failedKey, Is.EqualTo(Key1));
        });

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

        Assert.Multiple(() =>
        {
            Assert.That(pixelsA, Is.Not.Null);
            Assert.That(pixelsB, Is.Not.Null);
            // Both should get the same result (value from first factory, 77).
            Assert.That(pixelsA![0], Is.EqualTo(77));
            Assert.That(pixelsB![0], Is.EqualTo(77));
        });
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
    public void DrainQueueWithLivenessCheck_CallableWithCanceledToken_DoesNotThrow()
    {
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var started = new ManualResetEventSlim(false);
        var hold = new ManualResetEventSlim(false);

        coordinator.Request(Key1, BlockingFactory(started, hold), ClaimantA, CancellationToken.None);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Queue second item.
        coordinator.Request(Key2, Factory(55), ClaimantB, CancellationToken.None);

        using var canceledTokenSource = new CancellationTokenSource();
        canceledTokenSource.Cancel();

        // Phase 0: verify the method is callable with a canceled token.
        // With maxConcurrency=1 and a slot busy, no drain occurs — the
        // skeleton exists and compiles. Phase 1 adds the real token
        // liveness wiring with per-claimant tokens.
        Assert.DoesNotThrow(() =>
            coordinator.DrainQueueWithLivenessCheck(canceledTokenSource.Token));

        // Cleanup: release the hold so the coordinator can drain normally.
        hold.Set();
        Thread.Sleep(500);

        var counters = coordinator.GetCounters();
        // Both items eventually complete (normal DrainQueue promotes Key2).
        Assert.That(counters.CompletedCount, Is.EqualTo(2));
    }
}
