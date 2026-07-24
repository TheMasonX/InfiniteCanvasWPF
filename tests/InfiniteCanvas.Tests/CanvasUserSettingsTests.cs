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
            AnnotationDisplayMode = 2,
            OutlineThickness = 3.5,
            LabelSize = 9,
            LabelDisplay = 1,
            ShowLabels = false
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