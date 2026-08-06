using System.Text.Json;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public sealed class TileCacheDiagnosticsExporterTests
{
    [Test]
    public void Serialize_ContainsStructuredSnapshotFields()
    {
        var key = new BackgroundTileCacheKey("source", "tile", 4, 2);
        var snapshot = new TileCacheDiagnosticsSnapshot(
            Guid.NewGuid(),
            1,
            [new TileCacheVariantDiagnostics(key, 128, true)],
            3,
            1,
            2,
            DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(TileCacheDiagnosticsExporter.Serialize(snapshot));
        var root = document.RootElement;

        Assert.That(root.GetProperty("ActiveCacheId").GetGuid(), Is.EqualTo(snapshot.ActiveCacheId));
        Assert.That(root.GetProperty("ResidentCount").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("QueuedWorkCount").GetInt32(), Is.EqualTo(3));
        Assert.That(root.GetProperty("ReservationCount").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("ResidentVariants")[0].GetProperty("Key").GetProperty("MipLevel").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task WriteAsync_WritesJsonToRequestedPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tile-cache-{Guid.NewGuid():N}.json");
        var snapshot = new TileCacheDiagnosticsSnapshot(
            Guid.NewGuid(),
            0,
            [],
            0,
            0,
            0,
            DateTimeOffset.UtcNow);

        try
        {
            await TileCacheDiagnosticsExporter.WriteAsync(path, snapshot);

            Assert.That(File.Exists(path), Is.True);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.That(document.RootElement.GetProperty("EvictionCount").GetInt32(), Is.Zero);
        }
        finally
        {
            File.Delete(path);
        }
    }
}