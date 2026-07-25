using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Tests;

[TestFixture]
public class PixelometerTileInfoTests
{
    [Test]
    public void FormatIncludesTileIdMipAndCanonicalDimensions()
    {
        var info = new BackgroundTileReadoutInfo("tile-01", 2, 2048, 1024);

        Assert.That(info.Format(), Is.EqualTo("TILE tile-01 mip 2 (2048x1024)"));
    }

    [Test]
    public void FormatUsesFallbackWhenTileIdIsMissing()
    {
        var info = new BackgroundTileReadoutInfo(string.Empty, 0, 256, 128);

        Assert.That(info.Format(), Is.EqualTo("TILE -- mip 0 (256x128)"));
    }
}
