using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class SampleImageTileTests
{
    [Test]
    public void CoarseMipRequest_DoesNotStartNativeGeneration()
    {
        var nativeGenerationStarted = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-coarse-only",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () =>
            {
                nativeGenerationStarted.Set();
                return Enumerable.Repeat((byte)11, 64).ToArray();
            },
            [],
            mipPixelFactory: mipLevel =>
            {
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(20 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        _ = tile.TryGetPixelsNonBlocking(2, out _, out _);
        SpinWait.SpinUntil(() => tile.IsMipGenerated(2), TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(tile.IsMipGenerated(2), Is.True);
            Assert.That(nativeGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
        });
    }

    [Test]
    public void ResidentByteCount_IncludesNativeAndResidentMips()
    {
        var tile = new SampleImageTile(
            "tile-resident-bytes",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)1, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)mipLevel, dims.Width * dims.Height).ToArray();
            });

        _ = tile.Pixels;
        Assert.That(tile.TryGetPixelsNonBlocking(1, out _, out _), Is.True);
        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.True);

        SpinWait.SpinUntil(() => tile.IsMipGenerated(1) && tile.IsMipGenerated(2), TimeSpan.FromSeconds(2));

        var mip1 = BackgroundTileMipPolicy.GetDimensions(8, 8, 1);
        var mip2 = BackgroundTileMipPolicy.GetDimensions(8, 8, 2);
        var expected = 64 + (mip1.Width * mip1.Height) + (mip2.Width * mip2.Height);

        Assert.That(tile.ResidentByteCount, Is.EqualTo(expected));
    }

    [Test]
    public void ResidentMipFallback_PrefersClosestResidentMipOverNativeLevelZero()
    {
        var mipThreeGenerationStarted = new ManualResetEventSlim(false);
        var releaseMipThreeGeneration = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-mip-fallback",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)0, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                if (mipLevel == 3)
                {
                    mipThreeGenerationStarted.Set();
                    releaseMipThreeGeneration.Wait();
                }

                var dimensions = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)mipLevel, dimensions.Width * dimensions.Height).ToArray();
            });

        var nativePixels = tile.Pixels;
        Assert.That(nativePixels.Length, Is.EqualTo(64));

        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.True);
        Assert.That(mipThreeGenerationStarted.Wait(TimeSpan.FromSeconds(2)), Is.False);

        Assert.That(tile.TryGetPixelsNonBlocking(3, out var fallbackPixels, out var residentMipLevel), Is.True);
        Assert.That(residentMipLevel, Is.EqualTo(2));
        Assert.That(fallbackPixels, Is.Not.SameAs(nativePixels));
        Assert.That(fallbackPixels[0], Is.EqualTo((byte)2));

        releaseMipThreeGeneration.Set();
    }

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
    public void CoordinatorCompletion_WithStaleEpoch_DiscardsPixels()
    {
        var coordinator = new TileWorkCoordinator(maxConcurrency: 1);
        var generationStarted = new ManualResetEventSlim(false);
        var releaseGeneration = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-stale-coordinator",
            new SpatialBounds(0, 0, 2, 2),
            2,
            2,
            () =>
            {
                generationStarted.Set();
                releaseGeneration.Wait();
                return [10, 20, 30, 40];
            },
            []);

        // Wire the coordinator to the tile so it uses the coordinator path.
        tile.Coordinator = coordinator;
        tile.ClaimantTokenProvider = () => CancellationToken.None;

        // Trigger generation (goes through coordinator).
        Assert.That(tile.TryGetPixelsNonBlocking(out _), Is.False);
        Assert.That(generationStarted.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Reset the tile — this bumps _generationEpoch. When the coordinator
        // completes, OnCoordinatorPixelsGenerated will see that the key's
        // ContentRevision (old epoch) doesn't match the current epoch and
        // discard the result.
        tile.ResetImageCache();
        releaseGeneration.Set();

        // Wait for coordinator to process the completion.
        Thread.Sleep(500);

        // Tile must not have accepted the stale pixels.
        Assert.That(tile.IsImageGenerated, Is.False);

        coordinator.Dispose();
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
                () => new Dictionary<string, object>(),
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
                () => new Dictionary<string, object>(),
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
