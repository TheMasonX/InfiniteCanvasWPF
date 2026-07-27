#if WINDOWS
using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class ProjectionAndBitmapBenchmarks
{
    private readonly CameraTransform _camera = new();
    private BenchmarkEntity[]? _entities = null;
    private ZeroCopyBitmapFactory? _bitmapFactory = null;

    [Params(1_000, 10_000, 100_000)]
    public int VisiblePointCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _entities = BenchmarkData.CreateUniformEntities(VisiblePointCount);
        _bitmapFactory = new ZeroCopyBitmapFactory(1_920, 1_080);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bitmapFactory.Dispose();
    }

    [Benchmark]
    public object ProjectAndRender()
    {
        var screenPoints = _entities.Select(entity =>
            _camera.WorldToScreen(
                entity.Bounds.X / BenchmarkData.WorldSize * _bitmapFactory.Width,
                entity.Bounds.Y / BenchmarkData.WorldSize * _bitmapFactory.Height));

        return _bitmapFactory.GenerateFrozenBitmap(
            screenPoints,
            new Bgra32Color(186, 208, 53, 255));
    }
}
#endif