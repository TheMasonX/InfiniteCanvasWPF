using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;
using System.Threading;

namespace InfiniteCanvas.Windows.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ZeroCopyBitmapFactoryTests
{
    [Test]
    public void GenerateFrozenBitmap_ReturnsCrossThreadSafeBgra32Image()
    {
        using var factory = new ZeroCopyBitmapFactory(100, 80);

        var bitmap = factory.GenerateFrozenBitmap(
            [new ScreenPoint(50, 40), new ScreenPoint(double.NaN, 0)]);

        Assert.Multiple(() =>
        {
            Assert.That(bitmap.IsFrozen, Is.True);
            Assert.That(bitmap.PixelWidth, Is.EqualTo(100));
            Assert.That(bitmap.PixelHeight, Is.EqualTo(80));
            Assert.That(bitmap.Format, Is.EqualTo(System.Windows.Media.PixelFormats.Bgra32));
        });
    }

    [Test]
    public void GenerateFrozenBitmap_RejectsUseAfterDispose()
    {
        var factory = new ZeroCopyBitmapFactory(10, 10);
        factory.Dispose();

        Assert.That(
            () => factory.GenerateFrozenBitmap([]),
            Throws.TypeOf<ObjectDisposedException>());
    }
}
