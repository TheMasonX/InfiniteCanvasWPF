namespace InfiniteCanvas.Rendering;

public readonly record struct Bgra32Color(byte Blue, byte Green, byte Red, byte Alpha)
{
    public static Bgra32Color OpaqueBlue { get; } = new(255, 0, 0, 255);
}
