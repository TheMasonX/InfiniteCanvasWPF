using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class Bgra32BufferLayoutTests
{
    [Test]
    public void Constructor_ComputesAlignedBgra32Layout()
    {
        var layout = new Bgra32BufferLayout(100, 50);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Stride, Is.EqualTo(400));
            Assert.That(layout.ByteCount, Is.EqualTo(20_000));
            Assert.That(layout.GetPixelOffset(99, 49), Is.EqualTo(19_996));
        });
    }

    [Test]
    public void Constructor_RejectsOverflowingBuffer()
    {
        Assert.That(
            () => new Bgra32BufferLayout(int.MaxValue, 2),
            Throws.TypeOf<OverflowException>());
    }
}
