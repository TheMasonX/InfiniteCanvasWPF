using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class SampleImageGeneratorTests
{
    [Test]
    public void GenerateSet_CreatesDeterministicTiledImagesAndAnnotations()
    {
        var first = SampleImageGenerator.GenerateSet(2, 64, 32, 128, 8, 3, 2, 42);
        var second = SampleImageGenerator.GenerateSet(2, 64, 32, 128, 8, 3, 2, 42);

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first[0].Pixels, Is.EqualTo(second[0].Pixels));
            Assert.That(first[0].Pixels, Has.All.InRange((byte)120, (byte)136));
            Assert.That(first[0].Annotations, Has.Count.EqualTo(3));
            Assert.That(first[0].Annotations.Select(item => item.Id),
                Is.EqualTo(second[0].Annotations.Select(item => item.Id)));
            Assert.That(first[0].Annotations[0].DefectPixels,
                Is.EqualTo(second[0].Annotations[0].DefectPixels));
            Assert.That(first[1].Bounds.X, Is.EqualTo(64));
        });
    }

    [Test]
    public void GenerateSet_CreatesDefaultLayoutWithoutGeneratingImages()
    {
        var tiles = SampleImageGenerator.GenerateSet(objectsPerTile: 1);

        Assert.Multiple(() =>
        {
            Assert.That(tiles, Has.Count.EqualTo(64));
            Assert.That(tiles, Has.All.Property(nameof(SampleImageTile.IsImageGenerated)).False);
            Assert.That(tiles[0].PixelWidth, Is.EqualTo(8192));
            Assert.That(tiles[0].PixelHeight, Is.EqualTo(2048));
            Assert.That(tiles[0].Annotations[0].DefectPixelWidth, Is.EqualTo(2 * (int)tiles[0].Annotations[0].Bounds.Width));
            Assert.That(tiles[0].Annotations[0].DefectPixelHeight, Is.EqualTo(2 * (int)tiles[0].Annotations[0].Bounds.Height));
            Assert.That(tiles[1].Bounds.X, Is.EqualTo(8192));
            Assert.That(tiles[1].Bounds.Y, Is.EqualTo(0));
            Assert.That(tiles[2].Bounds.X, Is.EqualTo(0));
            Assert.That(tiles[2].Bounds.Y, Is.EqualTo(2048));
            Assert.That(tiles[63].Bounds.X, Is.EqualTo(8192));
            Assert.That(tiles[63].Bounds.Y, Is.EqualTo(63488));
            Assert.That(tiles.Sum(tile => tile.Annotations.Count), Is.EqualTo(64));
        });
    }

    [Test]
    public void Pixels_AreGeneratedOnceOnFirstAccess()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0)[0];

        var first = tile.Pixels;
        var second = tile.Pixels;

        Assert.Multiple(() =>
        {
            Assert.That(tile.IsImageGenerated, Is.True);
            Assert.That(second, Is.SameAs(first));
            Assert.That(first, Has.Length.EqualTo(64 * 32));
        });
    }

    [Test]
    public void AnnotationDefectPixels_AreGeneratedForObjectPatch()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 1)[0];
        var annotation = tile.Annotations[0];

        Assert.Multiple(() =>
        {
            Assert.That(annotation.DefectPixels, Has.Length.EqualTo(annotation.DefectPixelWidth * annotation.DefectPixelHeight));
            Assert.That(annotation.DefectPixels.Any(value => value > 0), Is.True);
        });
    }

    [Test]
    public void TryGetPixelValue_ReturnsTileSampleForWorldCoordinate()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0, seed: 42)[0];

        var inside = tile.TryGetPixelValue(tile.Bounds.X + 10, tile.Bounds.Y + 5, out var insideValue);
        var outside = tile.TryGetPixelValue(tile.Bounds.Right, tile.Bounds.Bottom, out _);

        Assert.Multiple(() =>
        {
            Assert.That(inside, Is.True);
            Assert.That(insideValue, Is.InRange((byte)120, (byte)136));
            Assert.That(outside, Is.False);
        });
    }

    [Test]
    public void AnnotationTryGetDefectValue_ReturnsDetailSampleForWorldCoordinate()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 1, seed: 42)[0];
        var annotation = tile.Annotations[0];

        var inside = annotation.TryGetDefectValue(annotation.Bounds.X + (annotation.Bounds.Width / 2), annotation.Bounds.Y + (annotation.Bounds.Height / 2), out var insideValue);
        var outside = annotation.TryGetDefectValue(annotation.Bounds.Right + 1, annotation.Bounds.Bottom + 1, out _);

        Assert.Multiple(() =>
        {
            Assert.That(inside, Is.True);
            Assert.That(insideValue, Is.InRange((byte)0, byte.MaxValue));
            Assert.That(outside, Is.False);
        });
    }
}