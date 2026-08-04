using System.Windows.Media;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.App.Controls;

/// <summary>
/// The frame boundary between the host render pipeline and the canvas
/// (ICW-315, ADR-0007). It carries the frozen raster plus the items,
/// viewport, and counts that describe one published frame.
/// The canvas displays the raster and never touches its backing memory
/// section, so the zero-copy buffer handoff stays intact
/// (ICW-P0-BUFFER-REUSE-SYNC, ICW-318).
/// </summary>
public sealed class CanvasFrame
{
    public CanvasFrame(
        ImageSource raster,
        IReadOnlyList<ICanvasItem> items,
        SpatialBounds viewport,
        int visibleItemCount,
        int totalItemCount,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(raster);
        ArgumentNullException.ThrowIfNull(items);
        if (visibleItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleItemCount));
        }

        if (totalItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalItemCount));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Raster = raster;
        Items = items;
        Viewport = viewport;
        VisibleItemCount = visibleItemCount;
        TotalItemCount = totalItemCount;
        Width = width;
        Height = height;
    }

    /// <summary>Frozen raster image displayed by the canvas.</summary>
    public ImageSource Raster { get; }

    /// <summary>Items visible in this frame.</summary>
    public IReadOnlyList<ICanvasItem> Items { get; }

    /// <summary>World viewport this frame was rendered for.</summary>
    public SpatialBounds Viewport { get; }

    public int VisibleItemCount { get; }

    public int TotalItemCount { get; }

    /// <summary>Raster width in pixels.</summary>
    public int Width { get; }

    /// <summary>Raster height in pixels.</summary>
    public int Height { get; }
}
