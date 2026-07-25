using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class ViewportZoomPolicyTests
{
    [Test]
    public void ComputeWheelDeltas_ContinuesFreeAxisAfterOtherAxisClamps()
    {
        var deltas = ViewportZoomPolicy.ComputeWheelDeltas(
            currentScaleX: 0.5,
            currentScaleY: 1,
            minimumScaleX: 0.5,
            minimumScaleY: 0.25,
            requestedScaleDelta: 0.8);

        Assert.Multiple(() =>
        {
            Assert.That(deltas.ScaleX, Is.EqualTo(1));
            Assert.That(deltas.ScaleY, Is.EqualTo(0.8));
            Assert.That(deltas.HasChange, Is.True);
        });
    }

    [Test]
    public void ComputeWheelDeltas_ZoomInKeepsClampedAxisUntilUniformTargetIsLegal()
    {
        var deltas = ViewportZoomPolicy.ComputeWheelDeltas(
            currentScaleX: 0.5,
            currentScaleY: 0.4,
            minimumScaleX: 0.5,
            minimumScaleY: 0.25,
            requestedScaleDelta: 1.2);

        Assert.Multiple(() =>
        {
            Assert.That(deltas.ScaleX, Is.EqualTo(1));
            Assert.That(deltas.ScaleY, Is.EqualTo(1.2));
        });
    }

    [Test]
    public void ComputeWheelDeltas_ZoomInRecoversUniformScaleWhenFreeAxisClearsFloor()
    {
        var deltas = ViewportZoomPolicy.ComputeWheelDeltas(
            currentScaleX: 0.5,
            currentScaleY: 0.48,
            minimumScaleX: 0.5,
            minimumScaleY: 0.25,
            requestedScaleDelta: 1.2);

        Assert.Multiple(() =>
        {
            Assert.That(deltas.ScaleX, Is.EqualTo(1.152));
            Assert.That(deltas.ScaleY, Is.EqualTo(1.2));
        });
    }

    [Test]
    public void ComputeWheelDeltas_ZoomInRecoversUniformScaleFromYClamp()
    {
        var deltas = ViewportZoomPolicy.ComputeWheelDeltas(
            currentScaleX: 0.48,
            currentScaleY: 0.5,
            minimumScaleX: 0.25,
            minimumScaleY: 0.5,
            requestedScaleDelta: 1.2);

        Assert.Multiple(() =>
        {
            Assert.That(deltas.ScaleX, Is.EqualTo(1.2));
            Assert.That(deltas.ScaleY, Is.EqualTo(1.152));
        });
    }

    [Test]
    public void ComputeDisplayPercent_UsesAxisWithLargestMinimumScale()
    {
        var percent = ViewportZoomPolicy.ComputeDisplayPercent(
            scaleX: 0.8,
            scaleY: 0.5,
            minimumScaleX: 0.4,
            minimumScaleY: 0.5);

        Assert.That(percent, Is.EqualTo(100));
    }
}