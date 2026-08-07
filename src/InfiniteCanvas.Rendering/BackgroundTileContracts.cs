using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

/// <summary>
/// Describes source-neutral tile identity and native dimensions for ICW-076.
/// </summary>
public sealed record BackgroundTileDescriptor
{
    public BackgroundTileDescriptor(
        string sourceId,
        string tileId,
        long contentRevision,
        SpatialBounds bounds,
        int nativePixelWidth,
        int nativePixelHeight,
        byte placeholderValue = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tileId);
        if (nativePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativePixelWidth));
        }

        if (nativePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativePixelHeight));
        }

        SourceId = sourceId;
        TileId = tileId;
        ContentRevision = contentRevision;
        Bounds = bounds;
        NativePixelWidth = nativePixelWidth;
        NativePixelHeight = nativePixelHeight;
        PlaceholderValue = placeholderValue;
    }

    public string SourceId { get; }

    public string TileId { get; }

    public long ContentRevision { get; }

    public SpatialBounds Bounds { get; }

    public int NativePixelWidth { get; }

    public int NativePixelHeight { get; }

    public byte PlaceholderValue { get; }
}

/// <summary>
/// Identifies one source revision and mip variant in the tile cache.
/// </summary>
public readonly record struct BackgroundTileCacheKey(
    string SourceId,
    string TileId,
    long ContentRevision,
    int MipLevel);

public interface ICacheReservation : IDisposable
{
}

/// <summary>
/// Requests one canonical mip payload for a source-neutral tile descriptor.
/// </summary>
public readonly record struct BackgroundTileRequest
{
    public BackgroundTileRequest(BackgroundTileDescriptor descriptor, int mipLevel)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (mipLevel is < 0 or > BackgroundTileMipPolicy.MaxMipLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        Descriptor = descriptor;
        MipLevel = mipLevel;
    }

    public BackgroundTileDescriptor Descriptor { get; }

    public int MipLevel { get; }

    public BackgroundTileCacheKey CacheKey => new(
        Descriptor.SourceId,
        Descriptor.TileId,
        Descriptor.ContentRevision,
        MipLevel);

    public (int Width, int Height) CanonicalDimensions =>
        BackgroundTileMipPolicy.GetDimensions(
            Descriptor.NativePixelWidth,
            Descriptor.NativePixelHeight,
            MipLevel);
}

public sealed record BackgroundTileReadoutInfo(string TileId, int MipLevel, int CanonicalWidth, int CanonicalHeight)
{
    public string Format()
    {
        var normalizedTileId = string.IsNullOrWhiteSpace(TileId) ? "--" : TileId;
        return $"TILE {normalizedTileId} mip {MipLevel} ({CanonicalWidth}x{CanonicalHeight})";
    }
}

/// <summary>
/// Carries validated Gray8 pixels for one source-neutral tile request.
/// </summary>
public sealed class BackgroundTilePayload
{
    public BackgroundTilePayload(BackgroundTileRequest request, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var (width, height) = request.CanonicalDimensions;
        var expectedLength = checked(width * height);
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException("Payload length does not match the requested mip dimensions.", nameof(pixels));
        }

        Request = request;
        Width = width;
        Height = height;
        Pixels = pixels;
        ByteCost = pixels.LongLength;
    }

    public BackgroundTileRequest Request { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public long ByteCost { get; }
}

/// <summary>
/// Resolves source-neutral background tile requests without WPF dependencies.
/// ICW-076 will connect this contract to the materializer and cache.
/// </summary>
public interface IBackgroundTileSource
{
    ValueTask<BackgroundTilePayload> ResolveAsync(
        BackgroundTileRequest request,
        CancellationToken cancellationToken = default);
}

public static class BackgroundTileMipPolicy
{
    public const int MaxMipLevel = 7;

    public static (int Width, int Height) GetDimensions(int nativeWidth, int nativeHeight, int mipLevel)
    {
        if (nativeWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeWidth));
        }

        if (nativeHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeHeight));
        }

        if (mipLevel is < 0 or > MaxMipLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        var divisor = 1 << mipLevel;
        return ((nativeWidth + divisor - 1) / divisor, (nativeHeight + divisor - 1) / divisor);
    }

    public static int SelectMipLevel(CameraSnapshot camera, int maxMipLevel = MaxMipLevel)
    {
        if (!double.IsFinite(camera.ScaleX)
            || !double.IsFinite(camera.ScaleY)
            || camera.ScaleX <= 0
            || camera.ScaleY <= 0)
        {
            return 0;
        }

        var clampedMax = Math.Clamp(maxMipLevel, 0, MaxMipLevel);
        // The larger scale is the binding axis: it has the highest texel
        // density and must not be under-resolved by a coarser mip.
        var bindingScale = Math.Max(camera.ScaleX, camera.ScaleY);
        var level = (int)Math.Floor(Math.Log2(1.0 / bindingScale));
        return Math.Clamp(level, 0, clampedMax);
    }
}

/// <summary>
/// Describes the set of tile cache keys that are currently of interest
/// to the viewport. Used by ICW-143 to cull non-visible tile work and
/// prioritize visible generation, and by ICW-205 to order queued work by
/// visibility class, center distance, and mip suitability.
/// </summary>
/// <param name="VisibleKeys">Tile cache keys that intersect the current viewport.
/// These have highest priority for generation.</param>
/// <param name="PrefetchKeys">Tile cache keys in a configurable margin around
/// the viewport. These have lower priority than visible keys.</param>
public readonly record struct ViewportInterestSet
{
    /// <summary>
    /// Creates an interest set with no scheduling context.
    /// The coordinator falls back to visible-first ordering.
    /// </summary>
    public ViewportInterestSet(
        IReadOnlySet<BackgroundTileCacheKey> visibleKeys,
        IReadOnlySet<BackgroundTileCacheKey> prefetchKeys)
        : this(visibleKeys, prefetchKeys, null, null, null, null)
    {
    }

    /// <summary>
    /// Creates an interest set with optional scheduling context.
    /// Neither set may be null — if no tiles are of interest, use <see cref="Empty"/>.
    /// </summary>
    /// <param name="visibleKeys">Keys that intersect the current viewport.</param>
    /// <param name="prefetchKeys">Keys in a margin around the viewport.</param>
    /// <param name="centerX">Camera center X in world coordinates, or null.</param>
    /// <param name="centerY">Camera center Y in world coordinates, or null.</param>
    /// <param name="selectedMipLevel">Target mip level for the mip-suitability tie-break, or null.</param>
    /// <param name="squaredDistanceFromCenter">
    /// Maps a cache key to its squared distance from the camera center.
    /// The caller derives this from tile bounds and the camera center.
    /// Null disables the distance tie-break.
    /// </param>
    public ViewportInterestSet(
        IReadOnlySet<BackgroundTileCacheKey> visibleKeys,
        IReadOnlySet<BackgroundTileCacheKey> prefetchKeys,
        double? centerX,
        double? centerY,
        int? selectedMipLevel,
        Func<BackgroundTileCacheKey, double>? squaredDistanceFromCenter)
    {
        ArgumentNullException.ThrowIfNull(visibleKeys);
        ArgumentNullException.ThrowIfNull(prefetchKeys);
        if (selectedMipLevel is < 0 or > BackgroundTileMipPolicy.MaxMipLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedMipLevel));
        }

        VisibleKeys = visibleKeys;
        PrefetchKeys = prefetchKeys;
        CenterX = centerX;
        CenterY = centerY;
        SelectedMipLevel = selectedMipLevel;
        SquaredDistanceFromCenter = squaredDistanceFromCenter;
    }

    public IReadOnlySet<BackgroundTileCacheKey> VisibleKeys { get; }

    public IReadOnlySet<BackgroundTileCacheKey> PrefetchKeys { get; }

    /// <summary>
    /// Camera center X in world coordinates, or null when no center is published.
    /// </summary>
    public double? CenterX { get; }

    /// <summary>
    /// Camera center Y in world coordinates, or null when no center is published.
    /// </summary>
    public double? CenterY { get; }

    /// <summary>
    /// Target mip level for the mip-suitability tie-break, or null.
    /// </summary>
    public int? SelectedMipLevel { get; }

    /// <summary>
    /// Maps a cache key to its squared distance from the camera center.
    /// Null disables the distance tie-break.
    /// </summary>
    public Func<BackgroundTileCacheKey, double>? SquaredDistanceFromCenter { get; }

    /// <summary>
    /// True if this interest set carries enough context for deterministic
    /// center-distance and mip-suitability ordering.
    /// </summary>
    public bool HasSchedulingContext => SquaredDistanceFromCenter is not null;

    /// <summary>
    /// True if the given key is in the visible or prefetch interest set.
    /// </summary>
    public bool Contains(BackgroundTileCacheKey key) =>
        VisibleKeys.Contains(key) || PrefetchKeys.Contains(key);

    /// <summary>
    /// True if the given key is in the visible (highest priority) set.
    /// </summary>
    public bool IsVisible(BackgroundTileCacheKey key) => VisibleKeys.Contains(key);

    /// <summary>
    /// Returns an empty interest set (no tiles are of interest).
    /// </summary>
    public static ViewportInterestSet Empty { get; } = new(
        new HashSet<BackgroundTileCacheKey>(),
        new HashSet<BackgroundTileCacheKey>());
}