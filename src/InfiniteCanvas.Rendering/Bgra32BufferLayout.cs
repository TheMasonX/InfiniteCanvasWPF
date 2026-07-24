namespace InfiniteCanvas.Rendering;

public readonly record struct Bgra32BufferLayout
{
    public Bgra32BufferLayout(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Stride = checked(width * 4);
        ByteCount = checked(Stride * height);
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public int ByteCount { get; }

    public bool Contains(int x, int y)
    {
        return (uint)x < (uint)Width && (uint)y < (uint)Height;
    }

    public int GetPixelOffset(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates must be within the buffer.");
        }

        return checked((y * Stride) + (x * 4));
    }
}
