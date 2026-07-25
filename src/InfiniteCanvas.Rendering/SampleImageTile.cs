using InfiniteCanvas.Core;
using System.Diagnostics;
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
    private int _generationEpoch;
    private long _generationDurationTicks;
#if WINDOWS
    private readonly Func<Bitmap>? _backgroundBitmapFactory;
    private int _backgroundFetched;
    private long _bitmapGenerationDurationTicks;
    private long _bitmapConversionDurationTicks;
#endif
    public event EventHandler? PixelsGenerated;
    public event EventHandler? PixelsGenerationFailed;

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
            var bitmapGenerationStarted = Stopwatch.GetTimestamp();
            using var bitmap = _backgroundBitmapFactory();
            Interlocked.Exchange(ref _bitmapGenerationDurationTicks, Stopwatch.GetTimestamp() - bitmapGenerationStarted);
            var conversionStarted = Stopwatch.GetTimestamp();
            var pixels = ConvertBitmapToGray8(bitmap, pixelWidth, pixelHeight);
            Interlocked.Exchange(ref _bitmapConversionDurationTicks, Stopwatch.GetTimestamp() - conversionStarted);
            return pixels;
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

    public bool IsGenerationQueued => Volatile.Read(ref _generationQueued) == 1 && !IsImageGenerated;

    public TimeSpan? GenerationDuration => IsImageGenerated
        ? DurationFromStopwatchTicks(Volatile.Read(ref _generationDurationTicks))
        : null;

#if WINDOWS
    public TimeSpan? BitmapGenerationDuration => IsImageGenerated
        ? DurationFromStopwatchTicks(Volatile.Read(ref _bitmapGenerationDurationTicks))
        : null;

    public TimeSpan? BitmapConversionDuration => IsImageGenerated
        ? DurationFromStopwatchTicks(Volatile.Read(ref _bitmapConversionDurationTicks))
        : null;

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

    public bool TryGetPixelsNonBlocking(out byte[] pixels, Func<bool>? tryReserveCacheEntry = null)
    {
        var cached = Volatile.Read(ref _pixels);
        if (cached is not null)
        {
            pixels = cached;
            return true;
        }

        EnsurePixelsGenerationStarted(tryReserveCacheEntry);
        pixels = Array.Empty<byte>();
        return false;
    }

    public void ResetImageCache()
    {
        lock (_cacheGate)
        {
            _pixels = null;
            Interlocked.Increment(ref _generationEpoch);
            Interlocked.Exchange(ref _generationQueued, 0);
#if WINDOWS
            Interlocked.Exchange(ref _backgroundFetched, 0);
#endif
        }
    }

    public IReadOnlyList<SampleAnnotation> Annotations { get; }

    public bool ShouldGenerateForPixelSize(CameraSnapshot camera, double minimumPixelSize)
    {
        if (!double.IsFinite(minimumPixelSize) || minimumPixelSize <= 0)
        {
            return true;
        }

        if (IsImageGenerated)
        {
            return true;
        }

        if (!double.IsFinite(camera.ScaleX)
            || !double.IsFinite(camera.ScaleY)
            || camera.ScaleX <= 0
            || camera.ScaleY <= 0)
        {
            return false;
        }

        var topLeft = camera.WorldToScreen(Bounds.X, Bounds.Y);
        var bottomRight = camera.WorldToScreen(Bounds.Right, Bounds.Bottom);
        var projectedWidth = Math.Max(0, bottomRight.X - topLeft.X);
        var projectedHeight = Math.Max(0, bottomRight.Y - topLeft.Y);
        return Math.Min(projectedWidth, projectedHeight) >= minimumPixelSize;
    }

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

    private void EnsurePixelsGenerationStarted(Func<bool>? tryReserveCacheEntry)
    {
        if (Interlocked.CompareExchange(ref _generationQueued, 1, 0) != 0)
        {
            return;
        }

        if (tryReserveCacheEntry is not null && !tryReserveCacheEntry())
        {
            Interlocked.Exchange(ref _generationQueued, 0);
            return;
        }

        _ = Task.Run(() =>
        {
            var generationStarted = Stopwatch.GetTimestamp();
            var generationEpoch = Volatile.Read(ref _generationEpoch);
            try
            {
                var generated = _pixelFactory();
                var shouldRaiseEvent = false;
                lock (_cacheGate)
                {
                    if (_pixels is null && generationEpoch == Volatile.Read(ref _generationEpoch))
                    {
                        _pixels = generated;
                        Interlocked.Exchange(ref _generationDurationTicks, Stopwatch.GetTimestamp() - generationStarted);
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
                PixelsGenerationFailed?.Invoke(this, EventArgs.Empty);
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

    private static TimeSpan DurationFromStopwatchTicks(long ticks) =>
        TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency);

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
        return AnnotationFeaturePresenter.BuildRows(this);
    }
}

public sealed record FeatureDisplayItem(string Name, string Value);

public sealed class TileCacheBudget
{
    public const long DefaultMaxBytes = 4L * 1024 * 1024 * 1024;

    private readonly long _maxBytes;
    private readonly Dictionary<string, SampleImageTile> _trackedTiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pinnedTileIds = new(StringComparer.OrdinalIgnoreCase);
    private long _usedBytes;
    private int _evictionCount;

    public TileCacheBudget(long maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        _maxBytes = maxBytes;
    }

    public long MaxBytes => _maxBytes;

    public long UsedBytes => Volatile.Read(ref _usedBytes);

    public int EvictionCount => Volatile.Read(ref _evictionCount);

    public int ResidentTileCount
    {
        get
        {
            lock (_trackedTiles)
            {
                return _trackedTiles.Count;
            }
        }
    }

    public void SetPinnedTiles(IEnumerable<SampleImageTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        lock (_trackedTiles)
        {
            _pinnedTileIds.Clear();
            foreach (var tile in tiles)
            {
                _pinnedTileIds.Add(tile.Id);
            }
        }
    }

    public bool TryReserve(SampleImageTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        lock (_trackedTiles)
        {
            if (_trackedTiles.ContainsKey(tile.Id))
            {
                return true;
            }

            _trackedTiles[tile.Id] = tile;
            Interlocked.Add(ref _usedBytes, tile.PixelCost);

            while (UsedBytes > _maxBytes)
            {
                var evictedTile = _trackedTiles.Values.FirstOrDefault(candidate =>
                    !string.Equals(candidate.Id, tile.Id, StringComparison.OrdinalIgnoreCase)
                    && !_pinnedTileIds.Contains(candidate.Id)
                    && candidate.IsImageGenerated);
                if (evictedTile is null)
                {
                    _trackedTiles.Remove(tile.Id);
                    Interlocked.Add(ref _usedBytes, -tile.PixelCost);
                    return false;
                }

                _trackedTiles.Remove(evictedTile.Id);
                Interlocked.Add(ref _usedBytes, -evictedTile.PixelCost);
                evictedTile.ResetImageCache();
                Interlocked.Increment(ref _evictionCount);
            }

            return true;
        }
    }

    public void Release(SampleImageTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        lock (_trackedTiles)
        {
            if (!_trackedTiles.Remove(tile.Id))
            {
                return;
            }

            Interlocked.Add(ref _usedBytes, -tile.PixelCost);
        }
    }

    public void Clear()
    {
        lock (_trackedTiles)
        {
            _trackedTiles.Clear();
            _pinnedTileIds.Clear();
            Interlocked.Exchange(ref _usedBytes, 0);
            Interlocked.Exchange(ref _evictionCount, 0);
        }
    }

    public string DescribeStatus()
    {
        return $"Cache {FormatBytes(UsedBytes)}/{FormatBytes(_maxBytes)}  |  {ResidentTileCount:N0} tiles  |  {EvictionCount:N0} evictions";
    }

    private static string FormatBytes(long bytes)
    {
        const long gibibyte = 1024L * 1024 * 1024;
        return $"{bytes / (double)gibibyte:F2} GiB";
    }
}