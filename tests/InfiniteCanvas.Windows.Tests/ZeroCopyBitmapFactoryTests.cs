using System.Drawing;
using System.Drawing.Imaging;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

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

    [Test]
    public void GenerateFrozenBitmap_ComposesImageTilesAndAnnotations()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 1, seed: 42)[0];
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        var bitmap = factory.GenerateFrozenBitmap([tile], tile.Annotations, new CameraTransform().Capture());

        Assert.Multiple(() =>
        {
            Assert.That(bitmap.IsFrozen, Is.True);
            Assert.That(bitmap.PixelWidth, Is.EqualTo(64));
            Assert.That(bitmap.PixelHeight, Is.EqualTo(32));
        });
    }

    [Test]
    public void GenerateFrozenBitmap_GeneratesOnlyTilesWithVisiblePixels()
    {
        var tiles = SampleImageGenerator.GenerateSet(2, 64, 32, objectsPerTile: 0, columns: 2, seed: 42);
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        factory.GenerateFrozenBitmap(tiles, [], new CameraTransform().Capture());
        SpinWait.SpinUntil(() => tiles[0].IsImageGenerated, TimeSpan.FromSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(tiles[0].IsImageGenerated, Is.True);
            Assert.That(tiles[1].IsImageGenerated, Is.False);
        });
    }

    [Test]
    public void GenerateSet_UsesNativeGray8PixelsWithoutBitmapConversion()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0, seed: 42)[0];

        _ = tile.Pixels;

        Assert.Multiple(() =>
        {
            Assert.That(tile.BitmapGenerationDuration, Is.Null);
            Assert.That(tile.BitmapConversionDuration, Is.Null);
        });
    }

    [Test]
    public void GenerateSet_BakesTileIndexIntoGray8Payload()
    {
        var tile = SampleImageGenerator.GenerateSet(
            1,
            64,
            32,
            targetValue: 128,
            noise: 0,
            objectsPerTile: 0,
            seed: 42)[0];
        Assert.That(tile.Pixels.Take(12 * 44).Any(value => value < 64), Is.True);
    }

    [Test]
    public void GenerateMonochromeMipPixels_BakesTileIndexAtEachMipResolution()
    {
        var native = SampleImageGenerator.GenerateMonochromeMipPixels(
            64,
            32,
            targetValue: 128,
            noise: 0,
            mipLevel: 0,
            circleCount: 0,
            tileLabel: "TILE-01");
        var mip = SampleImageGenerator.GenerateMonochromeMipPixels(
            64,
            32,
            targetValue: 128,
            noise: 0,
            mipLevel: 2,
            circleCount: 0,
            tileLabel: "TILE-01");

        Assert.Multiple(() =>
        {
            Assert.That(native.Length, Is.EqualTo(64 * 32));
            Assert.That(mip.Length, Is.EqualTo(16 * 8));
            Assert.That(native.Take(12 * 44).Any(value => value < 64), Is.True);
            Assert.That(mip.Any(value => value < 64), Is.True);
        });
    }

    [Test]
    public void GenerateFrozenBitmap_SkipsTileIndexLabelsWhenBackgroundImagesHidden()
    {
        var tile = SampleImageGenerator.GenerateSet(
            1,
            64,
            32,
            targetValue: 128,
            noise: 0,
            objectsPerTile: 0,
            seed: 42)[0];
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        var bitmap = factory.GenerateFrozenBitmap(
            [tile],
            [],
            new CameraTransform().Capture(),
            showBackgroundImages: false);
        var pixels = new byte[64 * 32 * 4];
        bitmap.CopyPixels(pixels, 64 * 4, 0);

        Assert.That(pixels.All(p => p == 0), Is.True);
    }

    [Test]
    public void GenerateFrozenBitmap_ComposesPartiallyVisibleTiles()
    {
        var tiles = SampleImageGenerator.GenerateSet(
            2,
            64,
            32,
            targetValue: 128,
            noise: 0,
            objectsPerTile: 0,
            columns: 2,
            seed: 42);
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        var bitmap = factory.GenerateFrozenBitmap(tiles, [], new CameraTransform().Capture());

        Assert.That(bitmap.IsFrozen, Is.True);
    }

    [Test]
    public void GenerateFrozenBitmap_RendersDefectBitmapUnalteredOutsideLogicalBounds()
    {
        using var defectBitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(defectBitmap))
        {
            graphics.Clear(Color.FromArgb(150, 150, 150));
        }

        var annotation = new SampleAnnotation(
            "annotation",
            "tile",
            "object",
            new SpatialBounds(10, 10, 2, 2),
            new Bgra32Color(0, 0, 255, 255),
            "Scratch",
            () => new Dictionary<string, object>(),
            4,
            4,
            Enumerable.Repeat((byte)150, 16).ToArray())
        {
            DefectBitmap = defectBitmap
        };
        using var factory = new ZeroCopyBitmapFactory(24, 24);

        var bitmap = factory.GenerateFrozenBitmap([], [annotation], new CameraTransform().Capture());
        var pixels = new byte[24 * 24 * 4];
        bitmap.CopyPixels(pixels, 24 * 4, 0);
        var outsideLogicalBoundsOffset = ((9 * 24) + 9) * 4;

        Assert.Multiple(() =>
        {
            Assert.That(pixels[outsideLogicalBoundsOffset], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 1], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 2], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 3], Is.EqualTo(255));
        });
    }
}
