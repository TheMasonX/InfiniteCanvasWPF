using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class SampleImageTileTests
{
    [Test]
    public void ResetImageCache_PreventsInFlightGenerationFromPublishingStalePixels()
    {
        var generationStarted = new ManualResetEventSlim(false);
        var releaseGeneration = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-reset",
            new SpatialBounds(0, 0, 2, 2),
            2,
            2,
            () =>
            {
                generationStarted.Set();
                releaseGeneration.Wait();
                return [1, 2, 3, 4];
            },
            []);

        Assert.That(tile.TryGetPixelsNonBlocking(out _), Is.False);
        Assert.That(generationStarted.Wait(TimeSpan.FromSeconds(2)), Is.True);

        tile.ResetImageCache();
        releaseGeneration.Set();

        Assert.That(tile.IsImageGenerated, Is.False);
    }

    [Test]
    public void DefectOverlaySampler_UsesLastApplicableAnnotationValueAndFallsBackToBackground()
    {
        var annotations = new List<SampleAnnotation>
        {
            new(
                "first",
                "tile",
                "object",
                new SpatialBounds(0, 0, 10, 10),
                new Bgra32Color(0, 0, 255, 255),
                "First",
                new Dictionary<string, double>(),
                2,
                2,
                [10, 20, 30, 40]),
            new(
                "second",
                "tile",
                "object",
                new SpatialBounds(0, 0, 10, 10),
                new Bgra32Color(0, 0, 255, 255),
                "Second",
                new Dictionary<string, double>(),
                2,
                2,
                [50, 60, 70, 80])
        };

        var firstMatch = DefectOverlaySampler.ResolveDisplayValue(128, annotations, 5, 5);
        var noMatch = DefectOverlaySampler.ResolveDisplayValue(128, annotations, 20, 20);

        Assert.Multiple(() =>
        {
            Assert.That(firstMatch, Is.EqualTo(80));
            Assert.That(noMatch, Is.EqualTo(128));
        });
    }
}
