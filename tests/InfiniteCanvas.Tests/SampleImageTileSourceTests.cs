using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public sealed class SampleImageTileSourceTests
{
    [Test]
    public async Task SetTiles_ResolvesRegisteredTilePayload()
    {
        var tile = CreateTile("tile-a", 23);
        var source = new SampleImageTileSource();
        source.SetTiles([tile]);

        var request = tile.CreateBackgroundTileRequest(0);
        var payload = await source.ResolveAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(payload.Request.CacheKey, Is.EqualTo(request.CacheKey));
            Assert.That(payload.Pixels[0], Is.EqualTo(23));
            Assert.That(payload.Width, Is.EqualTo(4));
            Assert.That(payload.Height, Is.EqualTo(4));
        });
    }

    [Test]
    public void ResolveAsync_RejectsRequestFromAnotherSource()
    {
        var tile = CreateTile("tile-source", 31);
        var source = new SampleImageTileSource();
        source.SetTiles([tile]);
        var descriptor = new BackgroundTileDescriptor(
            "other-source",
            tile.Id,
            tile.CurrentGenerationEpoch,
            tile.Bounds,
            tile.PixelWidth,
            tile.PixelHeight);

        Assert.Throws<InvalidOperationException>(() => source.ResolveAsync(new BackgroundTileRequest(descriptor, 0)));
    }

    [Test]
    public void ResolveAsync_RejectsMissingTile()
    {
        var source = new SampleImageTileSource();
        var descriptor = new BackgroundTileDescriptor(
            SampleImageTile.SourceId,
            "missing",
            0,
            new SpatialBounds(0, 0, 4, 4),
            4,
            4);

        Assert.Throws<KeyNotFoundException>(() => source.ResolveAsync(new BackgroundTileRequest(descriptor, 0)));
    }

    [Test]
    public void ResolveAsync_RejectsRequestFromPreviousTileRevision()
    {
        var tile = CreateTile("tile-revision", 47);
        var source = new SampleImageTileSource();
        source.SetTiles([tile]);
        var request = tile.CreateBackgroundTileRequest(0);

        tile.ResetImageCache();

        Assert.Throws<InvalidOperationException>(() => source.ResolveAsync(request));
    }

    private static SampleImageTile CreateTile(string id, byte value) => new(
        id,
        new SpatialBounds(0, 0, 4, 4),
        4,
        4,
        () => Enumerable.Repeat(value, 16).ToArray(),
        []);
}
