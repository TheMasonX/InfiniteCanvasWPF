using System.Runtime.CompilerServices;

// The host app composes the annotation and tile-grid overlays through
// CanvasControl.GetOverlayHost (ICW-319). The overlay host and its raw
// canvases stay internal so they never become library API; this attribute
// grants the app access without widening the public surface.
[assembly: InternalsVisibleTo("InfiniteCanvas.App")]
