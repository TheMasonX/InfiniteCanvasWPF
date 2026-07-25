namespace InfiniteCanvas.Core;

public static class TileGridIndexLookup
{
    public static bool TryGetTileIndex(
        double worldX,
        double worldY,
        SpatialBounds sceneBounds,
        double tileWidth,
        double tileHeight,
        int columns,
        int tileCount,
        out int tileIndex)
    {
        tileIndex = -1;

        if (!double.IsFinite(worldX)
            || !double.IsFinite(worldY)
            || tileWidth <= 0
            || tileHeight <= 0
            || columns <= 0
            || tileCount <= 0)
        {
            return false;
        }

        if (worldX < sceneBounds.X
            || worldX >= sceneBounds.Right
            || worldY < sceneBounds.Y
            || worldY >= sceneBounds.Bottom)
        {
            return false;
        }

        var column = (int)((worldX - sceneBounds.X) / tileWidth);
        if (column < 0 || column >= columns)
        {
            return false;
        }

        var row = (int)((worldY - sceneBounds.Y) / tileHeight);
        if (row < 0)
        {
            return false;
        }

        var index = (row * columns) + column;
        if (index < 0 || index >= tileCount)
        {
            return false;
        }

        tileIndex = index;
        return true;
    }
}