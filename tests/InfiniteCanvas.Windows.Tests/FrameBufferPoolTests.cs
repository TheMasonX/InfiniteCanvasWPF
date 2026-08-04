using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Windows.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class FrameBufferPoolTests
{
    [Test]
    public void Publish_PresentedBufferIsNotReusedAsImmediateBack()
    {
        using var pool = new FrameBufferPool();

        var first = pool.AcquireBackBuffer(100, 80);
        pool.Publish(first);

        var second = pool.AcquireBackBuffer(100, 80);

        Assert.That(second, Is.Not.SameAs(first),
            "A presented buffer must not be reused as the next back buffer.");
        Assert.That(pool.Front, Is.SameAs(first));
    }

    [Test]
    public void Rotation_RecyclesRetiredBufferAfterFullFrameCycle()
    {
        using var pool = new FrameBufferPool();

        var first = pool.AcquireBackBuffer(100, 80);
        pool.Publish(first);

        var second = pool.AcquireBackBuffer(100, 80);
        pool.Publish(second);

        var third = pool.AcquireBackBuffer(100, 80);

        Assert.That(third, Is.SameAs(first),
            "The first buffer may be recycled only after a full frame cycle.");
        Assert.That(pool.Back, Is.SameAs(first));
        Assert.That(pool.Retired, Is.Null,
            "Acquiring the retired buffer consumes the retired slot.");
    }

    [Test]
    public void Rotation_NeverHoldsTheSameBufferInTwoSlots()
    {
        using var pool = new FrameBufferPool();

        for (var i = 0; i < 8; i++)
        {
            var back = pool.AcquireBackBuffer(64, 48);

            Assert.That(pool.Front, Is.Not.SameAs(back), "Back must never equal front.");
            Assert.That(pool.Retired, Is.Not.SameAs(back), "Back must never equal retired.");

            pool.Publish(back);

            Assert.That(pool.Back, Is.Null, "Publish clears the back slot.");
            Assert.That(pool.Front, Is.SameAs(back));
            Assert.That(pool.Retired, Is.Not.SameAs(back), "Retired must never equal the presented front.");
        }
    }

    [Test]
    public void Rotation_ReusesAtMostThreeBuffers()
    {
        using var pool = new FrameBufferPool();
        var presented = new List<ZeroCopyBitmapFactory>();

        for (var i = 0; i < 12; i++)
        {
            var back = pool.AcquireBackBuffer(80, 60);
            presented.Add(back);
            pool.Publish(back);
        }

        Assert.That(presented.Distinct().Count(), Is.LessThanOrEqualTo(3),
            "Triple-buffering must not allocate a new section per frame.");
    }

    [Test]
    public void AcquireBackBuffer_SizeMismatchAllocatesNewSection()
    {
        using var pool = new FrameBufferPool();

        var small = pool.AcquireBackBuffer(100, 80);
        pool.Publish(small);

        var larger = pool.AcquireBackBuffer(200, 160);

        Assert.That(larger, Is.Not.SameAs(small));
        Assert.That(larger.Width, Is.EqualTo(200));
        Assert.That(larger.Height, Is.EqualTo(160));
    }

    [Test]
    public void Publish_ReleasesRetiredBufferWhenSizeChanges()
    {
        using var pool = new FrameBufferPool();

        var first = pool.AcquireBackBuffer(100, 80);
        pool.Publish(first);
        var second = pool.AcquireBackBuffer(100, 80);
        pool.Publish(second);

        var resized = pool.AcquireBackBuffer(300, 200);

        Assert.That(resized, Is.Not.SameAs(first));
        Assert.That(resized, Is.Not.SameAs(second));
        Assert.That(pool.Retired, Is.Null,
            "A retired buffer that no longer matches the target size must be released.");
    }
}
