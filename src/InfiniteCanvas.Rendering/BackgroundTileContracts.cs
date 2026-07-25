using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

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

public readonly record struct BackgroundTileCacheKey(
    string SourceId,
    string TileId,
    long ContentRevision,
    int MipLevel);

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
        var minimumScale = Math.Min(camera.ScaleX, camera.ScaleY);
        var level = (int)Math.Floor(Math.Log2(1.0 / minimumScale));
        return Math.Clamp(level, 0, clampedMax);
    }
}