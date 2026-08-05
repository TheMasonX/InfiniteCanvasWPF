using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        BitmapSource raster,
        IReadOnlyList<ICanvasItem> items,
        SpatialBounds viewport,
        int visibleItemCount,
        int totalItemCount,
        int width,
        int height,
        int revision = 0)
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

        // Count-consistency validation (ICW-316A): the visible count cannot
        // exceed the total, and the items list is exactly the visible set.
        if (visibleItemCount > totalItemCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibleItemCount),
                "VisibleItemCount cannot exceed TotalItemCount.");
        }

        if (items.Count != visibleItemCount)
        {
            throw new ArgumentException(
                "The items list count must equal visibleItemCount.",
                nameof(items));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        // Raster-dimension validation against ImageSource metadata (ICW-316A).
        // The raster must match the frame's declared pixel size so the shell
        // and overlay math stay aligned with the frozen image.
        if (raster.PixelWidth != width || raster.PixelHeight != height)
        {
            throw new ArgumentException(
                $"Raster dimensions {raster.PixelWidth}x{raster.PixelHeight} do not match frame dimensions {width}x{height}.",
                nameof(raster));
        }

        Raster = raster;
        Items = items;
        Viewport = viewport;
        VisibleItemCount = visibleItemCount;
        TotalItemCount = totalItemCount;
        Width = width;
        Height = height;
        Revision = revision;
    }

    /// <summary>Frozen raster image displayed by the canvas.</summary>
    public BitmapSource Raster { get; }

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

    /// <summary>Stale-frame revision identity (ICW-316A).</summary>
    public int Revision { get; }
}
