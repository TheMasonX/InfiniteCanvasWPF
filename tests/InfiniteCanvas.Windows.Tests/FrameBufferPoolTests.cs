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
    public void RetiredBuffer_BecomesReusableOnlyAfterTwoCompositionPasses()
    {
        using var pool = new FrameBufferPool();

        var first = pool.AcquireBackBuffer(100, 80);
        pool.Publish(first);
        var second = pool.AcquireBackBuffer(100, 80);
        pool.Publish(second);

        // First pass: the retired first buffer moves to the confirmed stage.
        pool.OnCompositionFrame();

        // Acquire a fresh buffer and publish it to clear the staged back slot.
        var third = pool.AcquireBackBuffer(100, 80);
        Assert.That(third, Is.Not.SameAs(first),
            "One composition pass must not make the retired buffer reusable.");
        pool.Publish(third);

        // Second pass: the confirmed buffer moves to the reusable stage.
        pool.OnCompositionFrame();
        Assert.That(pool.AcquireBackBuffer(100, 80), Is.SameAs(first),
            "The retired buffer is reusable only after two composition passes.");
    }

    [Test]
    public void Rotation_ReusesBuffersAfterCompositionAdvances()
    {
        using var pool = new FrameBufferPool();
        var presented = new List<ZeroCopyBitmapFactory>();

        for (var i = 0; i < 8; i++)
        {
            var back = pool.AcquireBackBuffer(80, 60);
            presented.Add(back);
            pool.Publish(back);
            pool.OnCompositionFrame();
            pool.OnCompositionFrame();
        }

        Assert.That(presented.Distinct().Count(), Is.LessThanOrEqualTo(4),
            "The pool must reuse buffers instead of allocating one per frame.");
    }

    [Test]
    public void Rotation_NeverHoldsTheSameBufferInFrontAndBack()
    {
        using var pool = new FrameBufferPool();

        for (var i = 0; i < 8; i++)
        {
            var back = pool.AcquireBackBuffer(64, 48);

            Assert.That(pool.Front, Is.Not.SameAs(back), "Back must never equal front.");

            pool.Publish(back);

            Assert.That(pool.Back, Is.Null, "Publish clears the back slot.");
            Assert.That(pool.Front, Is.SameAs(back));
        }
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
    public void AcquireBackBuffer_DisposesReusableBufferOnSizeMismatch()
    {
        using var pool = new FrameBufferPool();

        var first = pool.AcquireBackBuffer(100, 80);
        pool.Publish(first);
        var second = pool.AcquireBackBuffer(100, 80);
        pool.Publish(second);
        pool.OnCompositionFrame();
        pool.OnCompositionFrame();

        var resized = pool.AcquireBackBuffer(300, 200);

        Assert.That(resized, Is.Not.SameAs(first));
        Assert.That(resized, Is.Not.SameAs(second));
        Assert.That(resized.Width, Is.EqualTo(300));
    }
}
