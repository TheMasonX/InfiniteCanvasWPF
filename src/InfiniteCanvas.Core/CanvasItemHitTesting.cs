namespace InfiniteCanvas.Core;

public static class CanvasItemHitTesting
{
    public static bool Contains(ICanvasItem item, double worldX, double worldY)
    {
        ArgumentNullException.ThrowIfNull(item);
        return double.IsFinite(worldX)
            && double.IsFinite(worldY)
            && worldX >= item.Bounds.X
            && worldX <= item.Bounds.Right
            && worldY >= item.Bounds.Y
            && worldY <= item.Bounds.Bottom;
    }
}
