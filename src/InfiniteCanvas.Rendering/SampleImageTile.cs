using InfiniteCanvas.Core;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace InfiniteCanvas.Rendering;

public sealed class SampleImageTile
{
    private readonly Lazy<byte[]> _pixels;
#if WINDOWS
    private readonly Lazy<Bitmap>? _backgroundBitmap;
#endif

    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        Func<byte[]> pixelFactory,
        IReadOnlyList<SampleAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(pixelFactory);
        ArgumentNullException.ThrowIfNull(annotations);

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        Id = id;
        Bounds = bounds;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _pixels = new Lazy<byte[]>(
            () => ValidatePixels(pixelFactory(), pixelWidth, pixelHeight),
            LazyThreadSafetyMode.ExecutionAndPublication);
        Annotations = annotations;
    }

#if WINDOWS
    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        Func<Bitmap> backgroundBitmapFactory,
        IReadOnlyList<SampleAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(backgroundBitmapFactory);
        ArgumentNullException.ThrowIfNull(annotations);

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        Id = id;
        Bounds = bounds;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _backgroundBitmap = new Lazy<Bitmap>(
            () => backgroundBitmapFactory(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _pixels = new Lazy<byte[]>(
            () =>
            {
                var bitmap = _backgroundBitmap.Value;
                try
                {
                    return ConvertBitmapToGray8(bitmap, pixelWidth, pixelHeight);
                }
                finally
                {
                    bitmap.Dispose();
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
        Annotations = annotations;
    }
#endif

    public string Id { get; }

    public SpatialBounds Bounds { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public bool IsImageGenerated => _pixels.IsValueCreated;

#if WINDOWS
    public bool IsBackgroundFetched => _backgroundBitmap?.IsValueCreated ?? _pixels.IsValueCreated;
#else
    public bool IsBackgroundFetched => _pixels.IsValueCreated;
#endif

    public byte[] Pixels => _pixels.Value;

    public IReadOnlyList<SampleAnnotation> Annotations { get; }

    public bool TryGetPixelValue(double worldX, double worldY, out byte value)
    {
        value = default;
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        if (worldX < Bounds.X || worldX >= Bounds.Right || worldY < Bounds.Y || worldY >= Bounds.Bottom)
        {
            return false;
        }

        var sourceX = Math.Clamp(
            (int)((worldX - Bounds.X) * PixelWidth / Bounds.Width),
            0,
            PixelWidth - 1);
        var sourceY = Math.Clamp(
            (int)((worldY - Bounds.Y) * PixelHeight / Bounds.Height),
            0,
            PixelHeight - 1);

        value = Pixels[(sourceY * PixelWidth) + sourceX];
        return true;
    }

    private static byte[] ValidatePixels(byte[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height))
        {
            throw new InvalidOperationException("Generated pixel data length must match the image dimensions.");
        }

        return pixels;
    }

#if WINDOWS
    private static unsafe byte[] ConvertBitmapToGray8(Bitmap bitmap, int expectedWidth, int expectedHeight)
    {
        if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
        {
            throw new InvalidOperationException("Generated bitmap dimensions must match tile dimensions.");
        }

        var pixels = new byte[checked(expectedWidth * expectedHeight)];
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var source = (byte*)data.Scan0;
            for (var y = 0; y < expectedHeight; y++)
            {
                var row = source + (y * data.Stride);
                var rowOffset = y * expectedWidth;
                for (var x = 0; x < expectedWidth; x++)
                {
                    var channelOffset = x * 3;
                    var blue = row[channelOffset];
                    var green = row[channelOffset + 1];
                    var red = row[channelOffset + 2];
                    pixels[rowOffset + x] = (byte)((red + green + blue) / 3);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }
#endif
}

public sealed record SampleAnnotation(
    string Id,
    string TileId,
    string ObjectId,
    SpatialBounds Bounds,
    Bgra32Color Color,
    string Classification,
    IReadOnlyDictionary<string, double> Features,
    int DefectPixelWidth,
    int DefectPixelHeight,
    byte[] DefectPixels) : ISpatialEntity
{
    public bool TryGetDefectValue(double worldX, double worldY, out byte value)
    {
        value = default;
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        if (worldX < Bounds.X || worldX >= Bounds.Right || worldY < Bounds.Y || worldY >= Bounds.Bottom)
        {
            return false;
        }

        var sourceX = Math.Clamp(
            (int)((worldX - Bounds.X) * DefectPixelWidth / Bounds.Width),
            0,
            DefectPixelWidth - 1);
        var sourceY = Math.Clamp(
            (int)((worldY - Bounds.Y) * DefectPixelHeight / Bounds.Height),
            0,
            DefectPixelHeight - 1);

        value = DefectPixels[(sourceY * DefectPixelWidth) + sourceX];
        return true;
    }
}