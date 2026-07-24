using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

public sealed class SampleImageTile
{
    public SampleImageTile(
        string id,
        SpatialBounds bounds,
        int pixelWidth,
        int pixelHeight,
        byte[] pixels,
        IReadOnlyList<SampleAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(annotations);

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        if (pixels.Length != checked(pixelWidth * pixelHeight))
        {
            throw new ArgumentException("Pixel data length must match the image dimensions.", nameof(pixels));
        }

        Id = id;
        Bounds = bounds;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Pixels = pixels;
        Annotations = annotations;
    }

    public string Id { get; }

    public SpatialBounds Bounds { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public byte[] Pixels { get; }

    public IReadOnlyList<SampleAnnotation> Annotations { get; }
}

public sealed record SampleAnnotation(
    string Id,
    string TileId,
    string ObjectId,
    SpatialBounds Bounds,
    Bgra32Color Color,
    string Classification,
    IReadOnlyDictionary<string, double> Features) : ISpatialEntity;