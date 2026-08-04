namespace InfiniteCanvas.Core;

/// <summary>
/// Supplies the canvas with scene content. An implementation wraps the host's
/// concrete data pipeline (scene generation, spatial index, tile cache). The
/// canvas never references a generic spatial index or an application data
/// type through this contract (ICW-312, ADR-0007).
/// </summary>
public interface ICanvasSceneSource
{
    /// <summary>World bounds of the whole scene.</summary>
    SpatialBounds SceneBounds { get; }

    /// <summary>Total number of items in the scene.</summary>
    int TotalItemCount { get; }

    /// <summary>Items whose bounds intersect the given viewport.</summary>
    IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport);

    /// <summary>
    /// Reads the pixel at a world point from resident payloads only.
    /// The implementation must never start tile generation as a side effect.
    /// Returns false when no tile owns the point.
    /// </summary>
    bool TryReadResidentPixel(double worldX, double worldY, int mipLevel, out CanvasPixelSample sample);

    /// <summary>Raised when the scene content changes and the host must re-query.</summary>
    event EventHandler? SceneChanged;
}
