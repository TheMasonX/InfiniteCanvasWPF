#if WINDOWS
using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class TileMaterializationBenchmarks
{
    [Params(2_048, 8_192)]
    public int TileWidth { get; set; }

    private SampleImageTile[] _tiles = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tileHeight = TileWidth / 2;
        _tiles = SampleImageGenerator.GenerateSet(
            imageCount: 4,
            pixelWidth: TileWidth,
            pixelHeight: tileHeight,
            objectsPerTile: 0,
            columns: 2,
            seed: 1729).ToArray();
    }

    [IterationSetup]
    public void ResetTiles()
    {
        foreach (var tile in _tiles)
        {
            tile.ResetImageCache();
        }
    }

    [Benchmark]
    public byte[] GenerateAndConvertOneTile() => _tiles[0].Pixels;

    [Benchmark]
    public byte[][] GenerateAndConvertFourTiles() => _tiles.Select(tile => tile.Pixels).ToArray();
}
#endif