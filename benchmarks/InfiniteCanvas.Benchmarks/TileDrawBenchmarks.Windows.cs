#if WINDOWS
using BenchmarkDotNet.Attributes;
using InfiniteCanvas.Core;
using InfiniteCanvas.Rendering;

namespace InfiniteCanvas.Benchmarks;

[MemoryDiagnoser]
public class TileDrawBenchmarks
{
    private const int ViewportWidth = 1_920;
    private const int ViewportHeight = 1_080;
    private const int NativeTileWidth = 8_192;
    private const int NativeTileHeight = 4_096;

    private ZeroCopyBitmapFactory? _bitmapFactory;
    private SampleImageTile? _tile;
    private CameraTransform? _camera;

    [Params(1.0, 0.5, 0.03125, 0.001)]
    public double CameraScale { get; set; }

    [Params(true, false)]
    public bool ResidentPixels { get; set; }

    public int RequestedMipLevel => BackgroundTileMipPolicy.SelectMipLevel(_camera!.Capture());

    [GlobalSetup]
    public void Setup()
    {
        _bitmapFactory = new ZeroCopyBitmapFactory(ViewportWidth, ViewportHeight);
        _camera = new CameraTransform();
        _camera.Zoom(CameraScale, new ScreenPoint(0, 0));
        _tile = CreateTile();
    }

    [IterationSetup]
    public void Reset()
    {
        _tile!.ResetImageCache();
        if (ResidentPixels)
        {
            EnsureRequestedMipIsResident();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bitmapFactory?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public object DrawTiles()
    {
        return _bitmapFactory!.GenerateFrozenBitmap(
            new[] { _tile! },
            Array.Empty<SampleAnnotation>(),
            _camera!.Capture());
    }

    private SampleImageTile CreateTile()
    {
        return new SampleImageTile(
            "benchmark-tile",
            new SpatialBounds(
                0,
                0,
                ViewportWidth / CameraScale,
                ViewportHeight / CameraScale),
            NativeTileWidth,
            NativeTileHeight,
            () => CreatePixels(NativeTileWidth, NativeTileHeight, 73),
            Array.Empty<SampleAnnotation>(),
            placeholderValue: 17,
            mipPixelFactory: mipLevel =>
            {
                var dimensions = BackgroundTileMipPolicy.GetDimensions(
                    NativeTileWidth,
                    NativeTileHeight,
                    mipLevel);
                return CreatePixels(dimensions.Width, dimensions.Height, (byte)(73 + mipLevel));
            });
    }

    private void EnsureRequestedMipIsResident()
    {
        var requestedMip = RequestedMipLevel;
        if (requestedMip == 0)
        {
            _ = _tile!.Pixels;
            return;
        }

        _tile!.TryGetPixelsNonBlocking(requestedMip, out _, out _);
        SpinWait.SpinUntil(() => _tile!.IsMipGenerated(requestedMip));
    }

    private static byte[] CreatePixels(int width, int height, byte value)
    {
        var pixels = new byte[checked(width * height)];
        pixels.AsSpan().Fill(value);
        return pixels;
    }
}
#endif
