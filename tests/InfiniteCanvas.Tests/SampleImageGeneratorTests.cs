using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class SampleImageGeneratorTests
{
    [Test]
    public void GenerateSet_CreatesDeterministicTiledImagesAndAnnotations()
    {
        var first = SampleImageGenerator.GenerateSet(2, 64, 32, 128, 8, objectsPerTile: 3, columns: 2, seed: 42);
        var second = SampleImageGenerator.GenerateSet(2, 64, 32, 128, 8, objectsPerTile: 3, columns: 2, seed: 42);

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(2));
            Assert.That(first[0].Pixels, Is.EqualTo(second[0].Pixels));
            Assert.That(first[0].Pixels.Distinct().Count(), Is.GreaterThan(1));
            Assert.That(first[0].Annotations, Has.Count.EqualTo(3));
            Assert.That(first[0].Annotations.Select(item => item.Id),
                Is.EqualTo(second[0].Annotations.Select(item => item.Id)));
            Assert.That(first[0].Annotations[0].DefectPixels,
                Is.EqualTo(second[0].Annotations[0].DefectPixels));
            Assert.That(first[1].Bounds.X, Is.EqualTo(64));
        });
    }

    [Test]
    public async Task GenerateSet_UsesIndependentDeterministicStreamsDuringParallelTileGeneration()
    {
        var serialTiles = SampleImageGenerator.GenerateSet(8, 128, 64, objectsPerTile: 0, seed: 42);
        var serialPixels = serialTiles.Select(tile => tile.Pixels).ToArray();

        var parallelTiles = SampleImageGenerator.GenerateSet(8, 128, 64, objectsPerTile: 0, seed: 42);
        var parallelPixels = await Task.WhenAll(parallelTiles.Select(tile => Task.Run(() => tile.Pixels)));

        Assert.That(parallelPixels, Is.EqualTo(serialPixels));
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
            Assert.That(tiles[0].PixelHeight, Is.EqualTo(4096));
            Assert.That(tiles[0].Annotations[0].DefectPixelWidth, Is.GreaterThan(0));
            Assert.That(tiles[0].Annotations[0].DefectPixelHeight, Is.GreaterThan(0));
            Assert.That(tiles[1].Bounds.X, Is.EqualTo(8192));
            Assert.That(tiles[1].Bounds.Y, Is.EqualTo(0));
            Assert.That(tiles[2].Bounds.X, Is.EqualTo(0));
            Assert.That(tiles[2].Bounds.Y, Is.EqualTo(4096));
            Assert.That(tiles[63].Bounds.X, Is.EqualTo(8192));
            Assert.That(tiles[63].Bounds.Y, Is.EqualTo(126976));
            Assert.That(tiles.Sum(tile => tile.Annotations.Count), Is.EqualTo(64));
        });
    }

    [Test]
    public void TileCacheBudget_DefaultCapacity_AcceptsDefaultTileCost()
    {
        var cacheBudget = new TileCacheBudget(TileCacheBudget.DefaultMaxBytes);
        var defaultTileCost = checked(SampleImageGenerator.DefaultPixelWidth * SampleImageGenerator.DefaultPixelHeight);
        var defaultBudget = TileCacheBudget.DefaultMaxBytes;

        Assert.Multiple(() =>
        {
            Assert.That(defaultBudget, Is.GreaterThanOrEqualTo((long)defaultTileCost));
            Assert.That(cacheBudget.TryReserve(SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0)[0]), Is.True);
        });
    }

    [Test]
    public void TileCacheBudget_RejectsNewTileWhenCapacityIsReservedByPinnedTiles()
    {
        var tiles = SampleImageGenerator.GenerateSet(2, 64, 32, objectsPerTile: 0);
        var cacheBudget = new TileCacheBudget(tiles[0].PixelCost);
        cacheBudget.SetPinnedTiles([tiles[0]]);

        Assert.Multiple(() =>
        {
            Assert.That(cacheBudget.TryReserve(tiles[0]), Is.True);
            Assert.That(cacheBudget.TryReserve(tiles[1]), Is.False);
            Assert.That(cacheBudget.ResidentTileCount, Is.EqualTo(1));
            Assert.That(cacheBudget.UsedBytes, Is.EqualTo(tiles[0].PixelCost));
        });
    }

    [Test]
    public void Tile_ShouldSkipGenerationWhenViewportSizeFallsBelowThreshold()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 0)[0];
        var smallCamera = new CameraSnapshot(0.25, 0.25, 0, 0);
        var largeCamera = new CameraSnapshot(4, 4, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(tile.ShouldGenerateForPixelSize(smallCamera, 64), Is.False);
            Assert.That(tile.ShouldGenerateForPixelSize(largeCamera, 64), Is.True);
            Assert.That(tile.ShouldGenerateForPixelSize(largeCamera, 0), Is.True);
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
            Assert.That(insideValue, Is.EqualTo((byte)128));
            Assert.That(outside, Is.False);
        });
    }

    [Test]
    public void GenerateSet_ValidatesEachParameterWithAccurateName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => SampleImageGenerator.GenerateSet(pixelWidth: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pixelWidth"));
            Assert.That(
                () => SampleImageGenerator.GenerateSet(pixelHeight: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pixelHeight"));
            Assert.That(
                () => SampleImageGenerator.GenerateSet(objectsPerTile: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("objectsPerTile"));
            Assert.That(
                () => SampleImageGenerator.GenerateSet(columns: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("columns"));
            Assert.That(
                () => SampleImageGenerator.GenerateSet(defectPoolSize: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("defectPoolSize"));
            Assert.That(
                () => SampleImageGenerator.GenerateSet(objectsPerTile: SampleImageGenerator.MaxObjectsPerTile + 1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("objectsPerTile"));
        });
    }

    [Test]
    public void GenerateSet_RequiresImageCountToMatchExplicitRows()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SampleImageGenerator.GenerateSet(imageCount: 3, columns: 2, rows: 2, objectsPerTile: 0));

        Assert.That(exception!.ParamName, Is.EqualTo("imageCount"));
    }

    [Test]
    public void GenerateSet_UsesExplicitRowsAndColumnsForTileCount()
    {
        var tiles = SampleImageGenerator.GenerateSet(imageCount: 6, columns: 2, rows: 3, objectsPerTile: 0);

        Assert.That(tiles, Has.Count.EqualTo(6));
    }

    [Test]
    public void GenerateSet_ProducesBackgroundNoiseAndSmallDefectCircles()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, targetValue: 128, noise: 8, objectsPerTile: 0, seed: 42)[0];
        var pixels = tile.Pixels;

        Assert.Multiple(() =>
        {
            Assert.That(pixels.Distinct().Count(), Is.GreaterThan(3), "Background tiles should include visible variation rather than a uniform fill.");
            Assert.That(pixels.Any(value => value < 110), Is.True, "Background tiles should contain darker defect-like circles near the target gray.");
            Assert.That(pixels.Any(value => value > 140), Is.True, "Background tiles should contain brighter noise variation.");
        });
    }

    [Test]
    public void GenerateMonochromePixels_AppliesDeterministicOffsetToEveryPixel()
    {
        var pixels = SampleImageGenerator.GenerateMonochromeMipPixels(32, 32, 128, 8, seed: 42, circleCount: 0, SampleImageGenerator.NoiseSettings.Default);

        Assert.Multiple(() =>
        {
            Assert.That(pixels, Has.Length.EqualTo(32 * 32));
            Assert.That(pixels.Distinct().Count(), Is.GreaterThan(3));
            Assert.That(pixels.Count(value => value != 128), Is.GreaterThan(32 * 32 / 2));
        });
    }

    [Test]
    public void GenerateMonochromePixels_IsStableForTheSameSeed()
    {
        var first = SampleImageGenerator.GenerateMonochromeMipPixels(32, 32, 128, 8, seed: 42, circleCount: 0, SampleImageGenerator.NoiseSettings.Default);
        var second = SampleImageGenerator.GenerateMonochromeMipPixels(32, 32, 128, 8, seed: 42, circleCount: 0, SampleImageGenerator.NoiseSettings.Default);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void GenerateSet_UsesWorldspaceOriginsForDeterministicTileVariation()
    {
        var firstSet = SampleImageGenerator.GenerateSet(2, 32, 32, targetValue: 128, noise: 8, objectsPerTile: 0, columns: 2, rows: 1, seed: 42);
        var secondSet = SampleImageGenerator.GenerateSet(2, 32, 32, targetValue: 128, noise: 8, objectsPerTile: 0, columns: 2, rows: 1, seed: 42);

        Assert.Multiple(() =>
        {
            Assert.That(firstSet[0].Pixels, Is.EqualTo(secondSet[0].Pixels));
            Assert.That(firstSet[1].Pixels, Is.Not.EqualTo(firstSet[0].Pixels));
        });
    }

    [Test]
    public void GenerateSet_RespectsConfiguredNoiseAndCircleCount()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, targetValue: 128, noise: 0, objectsPerTile: 0, seed: 42, circleCount: 0)[0];
        var pixels = tile.Pixels;

        Assert.That(pixels, Has.All.EqualTo(128));
    }

    [Test]
    public void AnnotationFeatureDisplayItems_ExposeReadableRows()
    {
        var tile = SampleImageGenerator.GenerateSet(1, 64, 32, objectsPerTile: 1, seed: 42)[0];
        var annotation = tile.Annotations[0];

        var rows = annotation.GetFeatureDisplayItems();

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(item => item.Name), Is.EquivalentTo(new[] { "Confidence", "Severity" }));
            Assert.That(rows.First(item => item.Name == "Confidence").Value, Does.Contain("%"));
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

    [Test]
    public void BackgroundTileMipPolicy_UsesCanonicalCeilingDimensionsAcrossEightLevels()
    {
        var dimensions = Enumerable.Range(0, BackgroundTileMipPolicy.MaxMipLevel + 1)
            .Select(level => BackgroundTileMipPolicy.GetDimensions(9, 5, level))
            .ToArray();

        Assert.That(dimensions, Is.EqualTo(new[]
        {
            (9, 5),
            (5, 3),
            (3, 2),
            (2, 1),
            (1, 1),
            (1, 1),
            (1, 1),
            (1, 1)
        }));
    }

    [Test]
    public void BackgroundTilePayload_RejectsNonCanonicalPixelCount()
    {
        var descriptor = new BackgroundTileDescriptor(
            "synthetic",
            "tile-1",
            4,
            new SpatialBounds(0, 0, 9, 5),
            9,
            5);
        var request = new BackgroundTileRequest(descriptor, 1);

        Assert.That(
            () => new BackgroundTilePayload(request, new byte[9 * 5]),
            Throws.ArgumentException);
    }

    [Test]
    public void BackgroundTileRequest_CacheKeyIncludesSourceRevisionAndMip()
    {
        var descriptor = new BackgroundTileDescriptor(
            "source-a",
            "tile-1",
            8,
            new SpatialBounds(0, 0, 4, 4),
            4,
            4);
        var request = new BackgroundTileRequest(descriptor, 2);

        Assert.That(request.CacheKey, Is.EqualTo(new BackgroundTileCacheKey("source-a", "tile-1", 8, 2)));
    }

    [Test]
    public void GenerateMonochromeMipPixels_IsDeterministicAndUsesCanonicalDimensions()
    {
        var first = SampleImageGenerator.GenerateMonochromeMipPixels(17, 9, 128, 8, 3, seed: 1729, circleCount: 2);
        var second = SampleImageGenerator.GenerateMonochromeMipPixels(17, 9, 128, 8, 3, seed: 1729, circleCount: 2);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(3 * 2));
        });
    }

    [Test]
    public void ReduceGray8Box_AveragesCoveredTexelsInsteadOfFloorSampling()
    {
        var reduced = SampleImageGenerator.ReduceGray8Box(
            [0, 255, 255, 0],
            (4, 1),
            (2, 1));

        Assert.That(reduced, Is.EqualTo(new byte[] { 128, 128 }));
    }

    [Test]
    public void GenerateMonochromeMipPixels_WithCanceledToken_ThrowsPromptly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            () => SampleImageGenerator.GenerateMonochromeMipPixels(
                64, 64, 128, 8, 0, seed: 1729, circleCount: 2,
                cancellationToken: cts.Token),
            Throws.TypeOf<OperationCanceledException>());
    }

    [Test]
    public async Task GenerateMonochromeMipPixels_WithTokenCanceledMidGeneration_StopsWithinBound()
    {
        using var cts = new CancellationTokenSource();
        var generation = Task.Run(() =>
            SampleImageGenerator.GenerateMonochromeMipPixels(
                2048, 2048, 128, 16, 0, seed: 1729, circleCount: 6,
                cancellationToken: cts.Token));

        // Cancel after generation starts so the expensive phases observe the token.
        await Task.Delay(10);
        cts.Cancel();

        Assert.That(
            async () => await generation.WaitAsync(TimeSpan.FromSeconds(2)),
            Throws.TypeOf<OperationCanceledException>());
    }
}