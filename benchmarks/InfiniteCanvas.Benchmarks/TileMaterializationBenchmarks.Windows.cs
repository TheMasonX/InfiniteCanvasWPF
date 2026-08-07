#if WINDOWS
using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

[SimpleJob(warmupCount: 2, iterationCount: 5, invocationCount: 1)]
[MemoryDiagnoser]
public class TileMaterializationBenchmarks
{
    [Params(2_048, 8_192)]
    public int TileWidth { get; set; }

    [Params(0, 1, 3)]
    public int MipLevel { get; set; }

    [Params("NoiseOnly", "CircleOnly", "FullTile")]
    public string Workload { get; set; } = "FullTile";

    private SampleImageTile? _tile;

    [GlobalSetup]
    public void Setup()
    {
        _tile = new SampleImageTile(
            "benchmark-tile",
            new InfiniteCanvas.Core.SpatialBounds(0, 0, TileWidth, TileWidth / 2),
            TileWidth,
            TileWidth / 2,
            () => GeneratePixels(0),
            Array.Empty<SampleAnnotation>(),
            placeholderValue: 128,
            mipPixelFactory: GeneratePixels);

        EnsureResidentMip();
    }

    [Benchmark(Baseline = true)]
    public byte[] GeneratePixelsByStage() => GeneratePixels(MipLevel);

    [Benchmark]
    public byte[] ColdTileMaterialization()
    {
        _tile!.ResetImageCache();
        return _tile.Pixels;
    }

    [Benchmark]
    public byte[] WarmTileReuse()
    {
        _ = _tile!.Pixels;
        return _tile.Pixels;
    }

    [Benchmark]
    public bool ResidentMipReuse()
    {
        EnsureResidentMip();
        _tile!.TryGetResidentPixels(MipLevel, out var pixels, out _);
        return pixels.Length > 0;
    }

    private void EnsureResidentMip()
    {
        if (MipLevel == 0)
        {
            _ = _tile!.Pixels;
            return;
        }

        _tile!.TryGetPixelsNonBlocking(MipLevel, out _, out _);
        SpinWait.SpinUntil(() => _tile.IsMipGenerated(MipLevel));
    }

    private byte[] GeneratePixels(int mipLevel)
    {
        var noise = Workload is "CircleOnly" ? (byte)0 : (byte)8;
        var circles = Workload is "NoiseOnly" ? 0 : 3;
        return SampleImageGenerator.GenerateMonochromeMipPixels(
            TileWidth,
            TileWidth / 2,
            targetValue: 128,
            noise,
            mipLevel,
            seed: 1729,
            circleCount: circles,
            noiseSettings: SampleImageGenerator.NoiseSettings.Default);
    }
}
#endif