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

            Assert.Multiple(() =>
            {
                Assert.That(loaded.ShowSparseImageTiles, Is.False);
                Assert.That(loaded.ShowImageTiles, Is.False);
                Assert.That(loaded.ShowBackgroundImages, Is.False);
                Assert.That(loaded.BackgroundTargetValue, Is.EqualTo(160));
            });
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
}