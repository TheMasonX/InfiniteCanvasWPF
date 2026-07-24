namespace InfiniteCanvas.Core;

public readonly record struct SpatialBounds(double X, double Y, double Width, double Height)
{
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
