namespace InfiniteCanvas.Core;

/// <summary>
/// Non-generic wrapper over the host's spatial index. The canvas consumes
/// visible items through this contract. It never touches a generic spatial
/// index service directly (ICW-312, ADR-0007).
/// </summary>
public interface ICanvasSpatialQuerySource
{
    IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport);
}
