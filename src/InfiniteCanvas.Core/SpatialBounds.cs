namespace InfiniteCanvas.Core;

public readonly record struct SpatialBounds
{
    public SpatialBounds(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if (!double.IsFinite(width) || width < 0 || !double.IsFinite(x + width))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!double.IsFinite(height) || height < 0 || !double.IsFinite(y + height))
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Intersects(SpatialBounds other)
    {
        return X <= other.Right
            && Right >= other.X
            && Y <= other.Bottom
            && Bottom >= other.Y;
    }
}
