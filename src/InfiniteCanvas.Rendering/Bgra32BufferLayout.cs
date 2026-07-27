namespace InfiniteCanvas.Rendering;

/// <summary>
/// Represents a tightly packed BGRA32 buffer layout for a 2D pixel surface.
/// <para>
/// Note: arithmetic is checked for overflow. Very large dimensions can cause
/// <see cref="OverflowException"/> during construction. Callers should validate
/// dimensions against <see cref="MaxWidth"/> and <see cref="GetMaxHeightForWidth(int)"/> if
/// they may approach large values.
/// </para>
/// </summary>
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

    /// <summary>
    /// Maximum width that can be safely multiplied by 4 without overflowing a 32-bit signed integer.
    /// </summary>
    public static int MaxWidth => int.MaxValue / 4;

    /// <summary>
    /// Returns the maximum height allowed for the provided width such that
    /// <c>width * 4 * height</c> does not overflow a 32-bit signed integer.
    /// </summary>
    /// <param name="width">The pixel width to compute the maximum height for.</param>
    /// <returns>The maximum safe height for the given width.</returns>
    public static int GetMaxHeightForWidth(int width)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var stride = (long)width * 4L;
        return (int)(int.MaxValue / stride);
    }

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
