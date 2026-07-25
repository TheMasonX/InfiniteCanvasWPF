using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class RenderRequestTrackerTests
{
    [Test]
    public void BeginRequest_ReturnsMonotonicVersions()
    {
        var tracker = new RenderRequestTracker();

        var first = tracker.BeginRequest();
        var second = tracker.BeginRequest();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.LessThan(second));
            Assert.That(tracker.IsCurrent(second), Is.True);
            Assert.That(tracker.IsCurrent(first), Is.False);
        });
    }

    [Test]
    public void Advance_InvalidatesOlderPendingRenderRequests()
    {
        var tracker = new RenderRequestTracker();
        var requestVersion = tracker.BeginRequest();

        tracker.Advance();

        Assert.Multiple(() =>
        {
            Assert.That(tracker.CurrentVersion, Is.GreaterThan(requestVersion));
            Assert.That(tracker.IsCurrent(requestVersion), Is.False);
        });
    }
}
