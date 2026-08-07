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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bitmap.IsFrozen, Is.True);
            Assert.That(bitmap.PixelWidth, Is.EqualTo(100));
            Assert.That(bitmap.PixelHeight, Is.EqualTo(80));
            Assert.That(bitmap.Format, Is.EqualTo(System.Windows.Media.PixelFormats.Bgra32));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bitmap.IsFrozen, Is.True);
            Assert.That(bitmap.PixelWidth, Is.EqualTo(64));
            Assert.That(bitmap.PixelHeight, Is.EqualTo(32));
        }
    }

    [Test]
    public void GenerateFrozenBitmap_GeneratesOnlyTilesWithVisiblePixels()
    {
        var tiles = SampleImageGenerator.GenerateSet(2, 64, 32, objectsPerTile: 0, columns: 2, seed: 42);
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        factory.GenerateFrozenBitmap(tiles, [], new CameraTransform().Capture());
        SpinWait.SpinUntil(() => tiles[0].IsImageGenerated, TimeSpan.FromSeconds(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiles[0].IsImageGenerated, Is.True);
            Assert.That(tiles[1].IsImageGenerated, Is.False);
        }
    }

    [Test]
    public void GenerateSet_UsesNativeGray8PixelsWithoutBitmapConversion()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0, seed: 42)[0];

        _ = tile.Pixels;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.BitmapGenerationDuration, Is.Null);
            Assert.That(tile.BitmapConversionDuration, Is.Null);
        }
    }

    [Test]
    public void GenerateFrozenBitmap_DoesNotStartMissingTileBelowSparsePixelThreshold()
    {
        var tile = new SampleImageTile(
            "small-tile",
            new SpatialBounds(0, 0, 1, 1),
            64,
            32,
            () => Enumerable.Repeat((byte)77, 64 * 32).ToArray(),
            []);
        using var factory = new ZeroCopyBitmapFactory(64, 32);

        factory.GenerateFrozenBitmap(
            [tile],
            [],
            new CameraTransform().Capture(),
            minimumSparseTilePixelSize: 2);

        Assert.That(tile.IsImageGenerated, Is.False);
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(native.Length, Is.EqualTo(64 * 32));
            Assert.That(mip.Length, Is.EqualTo(16 * 8));
            Assert.That(native.Take(12 * 44).Any(value => value < 64), Is.True);
            Assert.That(mip.Any(value => value < 64), Is.True);
        }
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
    public void GenerateFrozenBitmap_PreservesExactPixelsForClampedTileBounds()
    {
        var pixels = Enumerable.Range(0, 16).Select(value => (byte)(10 + value)).ToArray();
        var tile = new SampleImageTile(
            "edge-tile",
            new SpatialBounds(-1, -1, 4, 4),
            4,
            4,
            () => pixels,
            []);
        _ = tile.Pixels;
        using var factory = new ZeroCopyBitmapFactory(4, 4);

        var bitmap = factory.GenerateFrozenBitmap([tile], [], new CameraTransform().Capture());
        var output = new byte[4 * 4 * 4];
        bitmap.CopyPixels(output, 4 * 4, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadGray(output, 4, 0, 0), Is.EqualTo(15));
            Assert.That(ReadGray(output, 4, 2, 2), Is.EqualTo(25));
            Assert.That(ReadGray(output, 4, 3, 0), Is.EqualTo(0));
            Assert.That(output[((0 * 4) + 0) * 4 + 3], Is.EqualTo(byte.MaxValue));
        }
    }

    [Test]
    public void GenerateFrozenBitmap_UsesResidentSelectedMip()
    {
        var tile = new SampleImageTile(
            "mip-tile",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)11, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel => Enumerable.Repeat((byte)(40 + mipLevel), 16).ToArray());
        tile.TryGetPixelsNonBlocking(1, out _, out _);
        Assert.That(SpinWait.SpinUntil(() => tile.IsMipGenerated(1), TimeSpan.FromSeconds(1)), Is.True);
        using var factory = new ZeroCopyBitmapFactory(4, 4);
        var camera = new CameraTransform();
        Assert.That(camera.Zoom(0.5, new ScreenPoint(0, 0)), Is.True);

        var bitmap = factory.GenerateFrozenBitmap([tile], [], camera.Capture());
        var output = new byte[4 * 4 * 4];
        bitmap.CopyPixels(output, 4 * 4, 0);

        Assert.That(ReadGray(output, 4, 2, 2), Is.EqualTo(41));
    }

    [Test]
    public void GenerateFrozenBitmap_RendersDefectPayloadUnalteredOutsideLogicalBounds()
    {
        // ICW-321: the display value comes from DefectPixels via the sampler.
        // The GDI+ DefectBitmap source read was dead and is removed; the pixel
        // payload is the single defect-content source.
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
            Enumerable.Repeat((byte)150, 16).ToArray());
        using var factory = new ZeroCopyBitmapFactory(24, 24);

        var bitmap = factory.GenerateFrozenBitmap([], [annotation], new CameraTransform().Capture());
        var pixels = new byte[24 * 24 * 4];
        bitmap.CopyPixels(pixels, 24 * 4, 0);
        var outsideLogicalBoundsOffset = ((9 * 24) + 9) * 4;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pixels[outsideLogicalBoundsOffset], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 1], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 2], Is.EqualTo(150));
            Assert.That(pixels[outsideLogicalBoundsOffset + 3], Is.EqualTo(255));
        }
    }

    [Test]
    public void GenerateFrozenBitmap_UsesSameLastWinsDefectValueAsSampler()
    {
        var annotations = new[]
        {
            CreateDefectAnnotation("first", 20),
            CreateDefectAnnotation("second", 80)
        };
        using var factory = new ZeroCopyBitmapFactory(4, 4);

        var bitmap = factory.GenerateFrozenBitmap([], annotations, new CameraTransform().Capture());
        var pixels = new byte[4 * 4 * 4];
        bitmap.CopyPixels(pixels, 4 * 4, 0);

        var expected = DefectOverlaySampler.ResolveDisplayValue(
            0,
            annotations,
            1,
            1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(expected, Is.EqualTo(80));
            Assert.That(ReadGray(pixels, 4, 1, 1), Is.EqualTo(expected));
        }
    }

    private static byte ReadGray(byte[] pixels, int width, int x, int y)
    {
        return pixels[((y * width) + x) * 4];
    }

    private static SampleAnnotation CreateDefectAnnotation(string id, byte value)
    {
        return new SampleAnnotation(
            id,
            "tile",
            id,
            new SpatialBounds(0, 0, 4, 4),
            new Bgra32Color(0, 0, 255, 255),
            "Scratch",
            () => new Dictionary<string, object>(),
            2,
            2,
            [value, value, value, value]);
    }
}
