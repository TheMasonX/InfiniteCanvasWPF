#if WINDOWS
using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

[SimpleJob(warmupCount: 2, iterationCount: 5, invocationCount: 1)]
[MemoryDiagnoser]
public class ProjectionAndBitmapBenchmarks
{
    private CameraTransform? _camera;
    private IReadOnlyList<SampleImageTile>? _tiles;
    private IReadOnlyList<SampleAnnotation>? _annotations;
    private ZeroCopyBitmapFactory? _bitmapFactory = null;

    [Params(true, false)]
    public bool IncludeSparseAnnotations { get; set; }

    [Params(true, false)]
    public bool ResidentPixels { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _camera = new CameraTransform();
        _camera.Zoom(0.25, new ScreenPoint(0, 0));
        _tiles = SampleImageGenerator.GenerateSet(
            imageCount: 1,
            pixelWidth: 8_192,
            pixelHeight: 4_096,
            objectsPerTile: 8,
            columns: 1,
            seed: 1729).ToArray();
        _annotations = _tiles!.SelectMany(tile => tile.Annotations).ToArray();
        _bitmapFactory = new ZeroCopyBitmapFactory(1_920, 1_080);
        if (ResidentPixels)
        {
            _ = _tiles![0].Pixels;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bitmapFactory?.Dispose();
    }

    [Benchmark]
    public object ComposeShippedTilePath()
    {
        return _bitmapFactory!.GenerateFrozenBitmap(
            _tiles!,
            IncludeSparseAnnotations ? _annotations! : Array.Empty<SampleAnnotation>(),
            _camera!.Capture(),
            showBackgroundImages: true,
            showSparseImageTiles: IncludeSparseAnnotations);
    }
}
#endif