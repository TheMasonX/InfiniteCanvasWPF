using InfiniteCanvas.Core;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class CanvasUserSettingsTests
{
    [Test]
    public void SaveAndLoad_RoundTripsValidSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"InfiniteCanvas-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        var expected = new CanvasUserSettings
        {
            TileColumns = 4,
            TileRows = 10,
            ObjectsPerTile = 7,
            GenerationSeed = 42,
            AnnotationDisplayMode = 2,
            OutlineThickness = 3.5,
            LabelSize = 9,
            LabelDisplay = 1,
            ShowLabels = false,
            ShowBoxes = false,
            ShowSparseImageTiles = false,
            ShowImageTiles = false,
            ShowBackgroundImages = false,
            BackgroundTargetValue = 160,
            MinimumSparseTilePixelSize = 128
        };

        try
        {
            CanvasUserSettingsStore.Save(path, expected);

            Assert.That(CanvasUserSettingsStore.Load(path), Is.EqualTo(expected));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void SaveAndLoad_RoundTripsLayerVisibilitySettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"InfiniteCanvas-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        var settings = new CanvasUserSettings
        {
            ShowSparseImageTiles = false,
            ShowImageTiles = false,
            ShowBackgroundImages = false,
            BackgroundTargetValue = 160
        };

        try
        {
            CanvasUserSettingsStore.Save(path, settings);
            var loaded = CanvasUserSettingsStore.Load(path);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loaded.ShowSparseImageTiles, Is.False);
                Assert.That(loaded.ShowImageTiles, Is.False);
                Assert.That(loaded.ShowBackgroundImages, Is.False);
                Assert.That(loaded.BackgroundTargetValue, Is.EqualTo(160));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void Load_ReturnsDefaultsForMalformedOrInvalidSettings()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ invalid json");
            Assert.That(CanvasUserSettingsStore.Load(path), Is.EqualTo(new CanvasUserSettings()));

            File.WriteAllText(path, "{\"Version\":1,\"TileColumns\":0}");
            Assert.That(CanvasUserSettingsStore.Load(path), Is.EqualTo(new CanvasUserSettings()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Load_ReturnsDefaultsWhenObjectsPerTileExceedsGeneratorLimit()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"Version\":1,\"ObjectsPerTile\":500}");

            var loaded = CanvasUserSettingsStore.Load(path);

            Assert.That(loaded, Is.EqualTo(new CanvasUserSettings()));
            Assert.That(CanvasUserSettings.ValidateObjectsPerTile(500), Is.False);
            Assert.That(CanvasUserSettings.ValidateObjectsPerTile(CanvasUserSettings.MaxObjectsPerTile), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ValidationFunctions_RejectNonFiniteSparseTileThreshold()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CanvasUserSettings.ValidateMinimumSparseTilePixelSize(double.NaN), Is.False);
            Assert.That(CanvasUserSettings.ValidateMinimumSparseTilePixelSize(double.PositiveInfinity), Is.False);
            Assert.That(CanvasUserSettings.ValidateMinimumSparseTilePixelSize(4096), Is.True);
        }
    }

    [Test]
    public void DefaultMinimumSparseTilePixelSize_AllowsBackgroundTilesAtAnyProjectedSize()
    {
        var settings = new CanvasUserSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CanvasUserSettings.DefaultMinimumSparseTilePixelSize, Is.EqualTo(0));
            Assert.That(settings.MinimumSparseTilePixelSize, Is.EqualTo(CanvasUserSettings.DefaultMinimumSparseTilePixelSize));
        }
    }
}