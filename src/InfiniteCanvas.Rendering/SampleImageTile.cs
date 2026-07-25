using InfiniteCanvas.Core;
#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace InfiniteCanvas.Rendering;

public sealed class SampleImageTile
{
    private readonly object _cacheGate = new();
    private readonly Func<byte[]> _pixelFactory;
    private readonly byte _placeholderValue;
    private readonly int _pixelCost;
    private byte[]? _pixels;
    private int _generationQueued;
#if WINDOWS
    private readonly Func<Bitmap>? _backgroundBitmapFactory;
    private int _backgroundFetched;
#endif
    public event EventHandler? PixelsGenerated;

    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        Func<byte[]> pixelFactory,
        IReadOnlyList<SampleAnnotation> annotations,
        byte placeholderValue = 128)
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
        _placeholderValue = placeholderValue;
        _pixelFactory = () => ValidatePixels(pixelFactory(), pixelWidth, pixelHeight);
        _pixelCost = checked(pixelWidth * pixelHeight);
        Annotations = annotations;
    }

#if WINDOWS
    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        Func<Bitmap> backgroundBitmapFactory,
        IReadOnlyList<SampleAnnotation> annotations,
        byte placeholderValue = 128)
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
        _placeholderValue = placeholderValue;
        _backgroundBitmapFactory = backgroundBitmapFactory;
        _pixelCost = checked(pixelWidth * pixelHeight);
        _pixelFactory = () =>
        {
            using var bitmap = _backgroundBitmapFactory();
            return ConvertBitmapToGray8(bitmap, pixelWidth, pixelHeight);
        };
        Annotations = annotations;
    }
#endif

    public string Id { get; }

    public SpatialBounds Bounds { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public byte PlaceholderValue => _placeholderValue;

    public int PixelCost => _pixelCost;

    public bool IsImageGenerated => Volatile.Read(ref _pixels) is not null;

#if WINDOWS
    public bool IsBackgroundFetched => Volatile.Read(ref _backgroundFetched) == 1;
#else
    public bool IsBackgroundFetched => IsImageGenerated;
#endif

    public byte[] Pixels
    {
        get
        {
            var cached = Volatile.Read(ref _pixels);
            if (cached is not null)
            {
                return cached;
            }

            lock (_cacheGate)
            {
                if (_pixels is null)
                {
                    _pixels = _pixelFactory();
#if WINDOWS
                    Interlocked.Exchange(ref _backgroundFetched, 1);
#endif
                    Interlocked.Exchange(ref _generationQueued, 1);
                }

                return _pixels;
            }
        }
    }

    public bool TryGetPixelsNonBlocking(out byte[] pixels)
    {
        var cached = Volatile.Read(ref _pixels);
        if (cached is not null)
        {
            pixels = cached;
            return true;
        }

        EnsurePixelsGenerationStarted();
        pixels = Array.Empty<byte>();
        return false;
    }

    public void ResetImageCache()
    {
        lock (_cacheGate)
        {
            _pixels = null;
            Interlocked.Exchange(ref _generationQueued, 0);
#if WINDOWS
            Interlocked.Exchange(ref _backgroundFetched, 0);
#endif
        }
    }

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

        if (!TryGetPixelsNonBlocking(out var pixels))
        {
            value = _placeholderValue;
            return true;
        }

        value = pixels[(sourceY * PixelWidth) + sourceX];
        return true;
    }

    private void EnsurePixelsGenerationStarted()
    {
        if (Interlocked.CompareExchange(ref _generationQueued, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var generated = _pixelFactory();
                var shouldRaiseEvent = false;
                lock (_cacheGate)
                {
                    if (_pixels is null)
                    {
                        _pixels = generated;
                        shouldRaiseEvent = true;
                    }
                }

#if WINDOWS
                Interlocked.Exchange(ref _backgroundFetched, 1);
#endif

                if (shouldRaiseEvent)
                {
                    PixelsGenerated?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                Interlocked.Exchange(ref _generationQueued, 0);
            }
        });
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
#if WINDOWS
    public Bitmap? DefectBitmap { get; init; }
#endif

    public bool TryGetDefectValue(double worldX, double worldY, out byte value)
    {
        value = default;
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        var localX = worldX - Bounds.X;
        var localY = worldY - Bounds.Y;
        var imageLeft = (Bounds.Width - DefectPixelWidth) / 2.0;
        var imageTop = (Bounds.Height - DefectPixelHeight) / 2.0;
        var imageRight = imageLeft + DefectPixelWidth;
        var imageBottom = imageTop + DefectPixelHeight;
        if (localX < imageLeft || localX >= imageRight || localY < imageTop || localY >= imageBottom)
        {
            return false;
        }

        var sourceX = Math.Clamp((int)(localX - imageLeft), 0, DefectPixelWidth - 1);
        var sourceY = Math.Clamp((int)(localY - imageTop), 0, DefectPixelHeight - 1);

        value = DefectPixels[(sourceY * DefectPixelWidth) + sourceX];
        return true;
    }

    public IReadOnlyList<FeatureDisplayItem> GetFeatureDisplayItems()
    {
        return Features
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new FeatureDisplayItem(item.Key, FormatFeatureValue(item.Value)))
            .ToArray();
    }

    private static string FormatFeatureValue(double value)
    {
        return value <= 1.0 && value >= 0.0
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:P1}", value)
            : value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed record FeatureDisplayItem(string Name, string Value);

public sealed class TileCacheBudget
{
    public const int DefaultRetainedTileCount = 4;
    public const long DefaultMaxPixels = DefaultRetainedTileCount
        * (long)SampleImageGenerator.DefaultPixelWidth
        * SampleImageGenerator.DefaultPixelHeight;

    private readonly long _maxPixels;
    private readonly Dictionary<string, SampleImageTile> _trackedTiles = new(StringComparer.OrdinalIgnoreCase);
    private long _usedPixels;

    public TileCacheBudget(long maxPixels)
    {
        if (maxPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPixels));
        }

        _maxPixels = maxPixels;
    }

    public long MaxPixels => _maxPixels;

    public long UsedPixels => Volatile.Read(ref _usedPixels);

    public bool CanAccept(int pixelCost) => UsedPixels + pixelCost <= _maxPixels;

    public void Add(int pixelCost)
    {
        if (pixelCost <= 0)
        {
            return;
        }

        Interlocked.Add(ref _usedPixels, pixelCost);
    }

    public void Remove(int pixelCost)
    {
        if (pixelCost <= 0)
        {
            return;
        }

        Interlocked.Add(ref _usedPixels, -pixelCost);
    }

    public void TrackTile(SampleImageTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (!tile.IsImageGenerated)
        {
            return;
        }

        lock (_trackedTiles)
        {
            if (_trackedTiles.ContainsKey(tile.Id))
            {
                return;
            }

            _trackedTiles[tile.Id] = tile;
            Add(tile.PixelCost);

            while (UsedPixels > _maxPixels && _trackedTiles.Count > 0)
            {
                var evictedTile = _trackedTiles.Values.FirstOrDefault();
                if (evictedTile is null)
                {
                    break;
                }

                _trackedTiles.Remove(evictedTile.Id);
                Remove(evictedTile.PixelCost);
                evictedTile.ResetImageCache();
            }
        }
    }

    public void Clear()
    {
        lock (_trackedTiles)
        {
            _trackedTiles.Clear();
            Interlocked.Exchange(ref _usedPixels, 0);
        }
    }

    public string DescribeStatus(IReadOnlyList<SampleImageTile> tiles)
    {
        var generated = tiles.Count(tile => tile.IsImageGenerated);
        var used = UsedPixels;
        return $"Budget {used:N0}/{_maxPixels:N0} pixels  |  {generated}/{tiles.Count} cached";
    }
}