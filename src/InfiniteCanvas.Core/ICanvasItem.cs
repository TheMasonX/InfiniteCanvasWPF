namespace InfiniteCanvas.Core;

/// <summary>
/// An item the canvas can display, hit-test, and report. The contract is
/// deliberately minimal: stable identity plus world bounds. ICW-314 extends
/// it with interaction members (selection state, tooltip payload).
/// </summary>
public interface ICanvasItem
{
    string Id { get; }

    SpatialBounds Bounds { get; }
}
