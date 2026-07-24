using InfiniteCanvas.Core;

namespace InfiniteCanvas.Rendering;

public readonly record struct ViewportRenderRequest(SpatialBounds Viewport, double ZoomLevel);
