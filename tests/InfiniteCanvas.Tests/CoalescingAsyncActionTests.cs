using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class CoalescingAsyncActionTests
{
    [Test]
    public async Task RequestsDuringExecution_AreCoalescedIntoOneFollowUpRun()
    {
        var firstRunStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runCount = 0;
        await using var action = new CoalescingAsyncAction(async cancellationToken =>
        {
            if (Interlocked.Increment(ref runCount) == 1)
            {
                firstRunStarted.TrySetResult();
                await releaseFirstRun.Task.WaitAsync(cancellationToken);
            }
        });

        var completion = action.RequestAsync();
        await firstRunStarted.Task;

        var pendingCompletion1 = action.RequestAsync();
        var pendingCompletion2 = action.RequestAsync();
        var pendingCompletion3 = action.RequestAsync();
        releaseFirstRun.TrySetResult();
        await Task.WhenAll(completion, pendingCompletion1, pendingCompletion2, pendingCompletion3);

        Assert.That(runCount, Is.EqualTo(2));
    }

    [Test]
    public async Task FaultedRun_ReportsFailureAndProcessesQueuedFollowUp()
    {
        var firstRunStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reportedFaults = new List<Exception>();
        var runCount = 0;
        await using var action = new CoalescingAsyncAction(
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref runCount) == 1)
                {
                    firstRunStarted.TrySetResult();
                    await releaseFirstRun.Task.WaitAsync(cancellationToken);
                    throw new InvalidOperationException("Expected render fault.");
                }
            },
            reportedFaults.Add);

        var completion = action.RequestAsync();
        await firstRunStarted.Task;
        var followUp = action.RequestAsync();
        releaseFirstRun.TrySetResult();

        await Task.WhenAll(completion, followUp);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(runCount, Is.EqualTo(2));
            Assert.That(reportedFaults, Has.Count.EqualTo(1));
            Assert.That(reportedFaults[0], Is.TypeOf<InvalidOperationException>());
        }
    }

    [Test]
    public async Task DisposeAsync_AfterHandledFault_DoesNotRethrowFailure()
    {
        var reportedFaults = new List<Exception>();
        var action = new CoalescingAsyncAction(
            _ => Task.FromException(new InvalidOperationException("Expected render fault.")),
            reportedFaults.Add);

        await action.RequestAsync();
        await action.DisposeAsync();

        Assert.That(reportedFaults, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DisposeAsync_CancelsActiveRunAndRejectsLaterRequests()
    {
        var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var action = new CoalescingAsyncAction(async cancellationToken =>
        {
            runStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });

        var completion = action.RequestAsync();
        await runStarted.Task;
        await action.DisposeAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(completion.IsCanceled, Is.True);
            Assert.That(
                () => action.RequestAsync(),
                Throws.TypeOf<ObjectDisposedException>());
        }
    }
}