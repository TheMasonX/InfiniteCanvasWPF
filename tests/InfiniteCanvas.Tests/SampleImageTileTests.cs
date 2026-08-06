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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.IsMipGenerated(2), Is.True);
            Assert.That(nativeGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstMatch, Is.EqualTo(80));
            Assert.That(noMatch, Is.EqualTo(128));
        }
    }

    [Test]
    public void ClaimantTokenFire_AllowsTileToRegenerateInLaterFrame()
    {
        // Regression test for ICW-204: per-frame claimant token cancellation
        // orphaned coordinator work without resetting the tile's generation-
        // queued flag. The tile never regenerated after scrolling and only
        // recovered on zoom (which changed the mip cache key).
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var generationStarted = new ManualResetEventSlim(false);
        var releaseGeneration = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-frame-token",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)7, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                generationStarted.Set();
                releaseGeneration.Wait();
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(40 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        tile.Coordinator = coordinator;

        // Simulate the per-frame CTS design used by MainWindow.RenderFrameAsync:
        // each frame provides a fresh claimant token.
        using var frame1 = new CancellationTokenSource();
        var currentToken = frame1.Token;
        tile.ClaimantTokenProvider = () => currentToken;

        // Frame 1: tile requests mip 2. Generation starts and blocks.
        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.False);
        Assert.That(generationStarted.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // Frame 2: the previous frame's token is cancelled, which is how the
        // coordinator removes the previous frame's claimants.
        using var frame2 = new CancellationTokenSource();
        currentToken = frame2.Token;
        frame1.Cancel();

        // Frame 2 renders the tile again. It must re-request generation even
        // though the previous request was dropped by the token cancellation.
        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.False);

        // Let the re-claimed generation complete.
        releaseGeneration.Set();
        SpinWait.SpinUntil(() => tile.IsMipGenerated(2), TimeSpan.FromSeconds(2));

        Assert.That(tile.IsMipGenerated(2), Is.True);
    }

    [Test]
    public void MipFactoryFailure_ClearsQueuedFlagAndAllowsRetry()
    {
        // Regression test for ICW-204: OnCoordinatorPixelsGenerationFailed only
        // reset the mip-0 flag. A failed mip left _mipGenerationQueued set, so
        // the mip never retried.
        using var coordinator = new TileWorkCoordinator(maxConcurrency: 4);
        var attempts = 0;
        var failed = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-mip-fail-retry",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)7, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                var count = Interlocked.Increment(ref attempts);
                if (count == 1)
                {
                    throw new InvalidOperationException("first attempt fails");
                }

                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(40 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        tile.Coordinator = coordinator;
        tile.ClaimantTokenProvider = () => CancellationToken.None;
        tile.PixelsGenerationFailed += (_, _) => failed.Set();

        // First request fails in the factory. The failure callback resets the
        // mip queued flag before the event fires.
        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.False);
        Assert.That(failed.Wait(TimeSpan.FromSeconds(2)), Is.True);

        // The failure must have cleared the mip queued flag so a retry is possible.
        Assert.That(tile.TryGetPixelsNonBlocking(2, out _, out _), Is.False);
        SpinWait.SpinUntil(() => tile.IsMipGenerated(2), TimeSpan.FromSeconds(2));

        Assert.That(tile.IsMipGenerated(2), Is.True);
        Assert.That(Volatile.Read(ref attempts), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void ResidentRead_DoesNotStartGenerationWhenNothingIsResident()
    {
        // ICW-312: the pixelometer-safe read must never initiate tile
        // generation (ICW-P0-PIXELOMETER-READOUT). A tile with no resident
        // payload returns false and leaves generation untouched.
        var nativeGenerationStarted = new ManualResetEventSlim(false);
        var mipGenerationStarted = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-resident-read-empty",
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
                mipGenerationStarted.Set();
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(20 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        Assert.That(tile.TryGetResidentPixels(2, out _, out _), Is.False);
        Assert.That(tile.IsGenerationQueued, Is.False);
        Assert.That(nativeGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
        Assert.That(mipGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
    }

    [Test]
    public void ResidentRead_ReturnsResidentNativePixels_WithoutStartingMipWork()
    {
        var mipGenerationStarted = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-resident-read-native",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)11, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                mipGenerationStarted.Set();
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(20 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        // Native pixels become resident through the render path, not the read.
        _ = tile.Pixels;
        Assert.That(tile.IsImageGenerated, Is.True);

        Assert.That(tile.TryGetResidentPixels(2, out var pixels, out var residentMipLevel), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(residentMipLevel, Is.EqualTo(0));
            Assert.That(pixels[0], Is.EqualTo((byte)11));
        }

        Assert.That(mipGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
    }

    [Test]
    public void ResidentRead_PrefersClosestResidentMip_WithoutStartingGeneration()
    {
        // Mip 1 is resident, mip 2 is requested and absent. The read returns
        // mip 1 without starting mip-2 generation.
        var mipTwoGenerationStarted = new ManualResetEventSlim(false);
        var tile = new SampleImageTile(
            "tile-resident-read-fallback",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)11, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                if (mipLevel == 2)
                {
                    mipTwoGenerationStarted.Set();
                }

                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(20 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        // Make mip 1 resident through the generating path.
        Assert.That(tile.TryGetPixelsNonBlocking(1, out _, out _), Is.False);
        SpinWait.SpinUntil(() => tile.IsMipGenerated(1), TimeSpan.FromSeconds(2));
        Assert.That(tile.IsMipGenerated(1), Is.True);

        Assert.That(tile.TryGetResidentPixels(2, out var fallbackPixels, out var residentMipLevel), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(residentMipLevel, Is.EqualTo(1));
            Assert.That(fallbackPixels[0], Is.EqualTo((byte)21));
        }

        Assert.That(mipTwoGenerationStarted.Wait(TimeSpan.FromMilliseconds(50)), Is.False);
    }

    [Test]
    public void ResidentRead_EqualDistance_PrefersHigherResolutionMip()
    {
        // ICW-329: at equal absolute distance the fallback prefers the lower
        // mip level (higher resolution). Request mip 2 with mip 1 and mip 3
        // both resident: distance 1 to each, so mip 1 wins.
        var tile = new SampleImageTile(
            "tile-resident-read-tiebreak",
            new SpatialBounds(0, 0, 8, 8),
            8,
            8,
            () => Enumerable.Repeat((byte)11, 64).ToArray(),
            [],
            mipPixelFactory: mipLevel =>
            {
                var dims = BackgroundTileMipPolicy.GetDimensions(8, 8, mipLevel);
                return Enumerable.Repeat((byte)(20 + mipLevel), dims.Width * dims.Height).ToArray();
            });

        // Make mip 1 resident through the generating path.
        Assert.That(tile.TryGetPixelsNonBlocking(1, out _, out _), Is.False);
        SpinWait.SpinUntil(() => tile.IsMipGenerated(1), TimeSpan.FromSeconds(2));
        Assert.That(tile.IsMipGenerated(1), Is.True);

        // Requesting mip 3 starts mip-3 generation. The call itself returns
        // true via the mip-1 fallback, so only the generation flag is asserted.
        _ = tile.TryGetPixelsNonBlocking(3, out _, out _);
        SpinWait.SpinUntil(() => tile.IsMipGenerated(3), TimeSpan.FromSeconds(2));
        Assert.That(tile.IsMipGenerated(3), Is.True);

        Assert.That(tile.TryGetResidentPixels(2, out var fallbackPixels, out var residentMipLevel), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(residentMipLevel, Is.EqualTo(1),
                "At equal distance the fallback must prefer the higher-resolution mip.");
            Assert.That(fallbackPixels[0], Is.EqualTo((byte)21));
        }
    }
}
