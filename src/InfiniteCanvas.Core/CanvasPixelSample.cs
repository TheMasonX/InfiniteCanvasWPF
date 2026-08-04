namespace InfiniteCanvas.Core;

/// <summary>
/// Result of a non-blocking resident pixel read. The source fills this value
/// from already-resident payloads only. It never initiates tile acquisition
/// (ICW-P0-PIXELOMETER-READOUT).
/// </summary>
/// <param name="Background">Gray8 background value at the sampled point, or the tile placeholder value.</param>
/// <param name="Defect">Defect overlay value at the sampled point, or 0.</param>
/// <param name="TileId">Id of the background tile that owns the sampled point.</param>
/// <param name="TileInfo">Formatted readout line for the tile, or an empty string when no tile owns the point.</param>
public readonly record struct CanvasPixelSample(
    byte Background,
    byte Defect,
    string TileId,
    string TileInfo);
