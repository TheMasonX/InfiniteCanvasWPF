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
            Assert.That(first[1].Bounds.X, Is.EqualTo(64));
        });
    }

    [Test]
    public void GenerateSet_UsesDefaultInspectionImageDimensions()
    {
        var tile = SampleImageGenerator.GenerateSet(imageCount: 1, objectsPerTile: 0)[0];

        Assert.Multiple(() =>
        {
            Assert.That(tile.PixelWidth, Is.EqualTo(8192));
            Assert.That(tile.PixelHeight, Is.EqualTo(2048));
            Assert.That(tile.Pixels, Has.Length.EqualTo(8192 * 2048));
        });
    }
}