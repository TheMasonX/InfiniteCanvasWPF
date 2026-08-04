# Handoff: ICW-312 and ICW-315 Data-Source Boundary

Date: 2026-08-04

## Status

ICW-312 (canvas data-source abstraction) is Done.
ICW-315 (CanvasFrame render-pipeline boundary) is Done.
ICW-316 (assembly extraction) is the next slice. It is Proposed.

## What Landed

### ICW-312: injected data-source boundary

- New Core contracts: `ICanvasItem`, `ICanvasSceneSource`, `ICanvasSpatialQuerySource`, `CanvasPixelSample`.
- `SampleAnnotation` implements `ICanvasItem`. `IReadOnlyList<out T>` covariance carries the spatial query result through the canvas contract without a mapping.
- `CanvasControl` exposes `SceneSource` and `SpatialQuerySource` dependency properties. The parameterless constructor stays for XAML and designer support.
- `CanvasViewModel` stays passive and non-generic. `ApplyFrame` gained an optional `IReadOnlyList<ICanvasItem>`; `VisibleItems` is retained for ICW-314.
- `MainWindow` implements both source interfaces, wires the dependency properties, and raises `SceneChanged` after regeneration.
- Non-blocking pixel read: `SampleImageTile.TryGetResidentPixels` reads resident payloads only. `MainWindow.UpdatePixelometer` reads through `CanvasSurface.SceneSource`. This closes the live ICW-P0-PIXELOMETER-READOUT violation: hover no longer initiates tile generation.
- Removed the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj`.

### ICW-315: CanvasFrame frame boundary

- `CanvasFrame` value carries the frozen raster `ImageSource` plus items, viewport, visible/total counts, and pixel dimensions.
- `CanvasControl.PublishFrame(CanvasFrame)` replaces `PublishFrame(UIElement)`. The control owns the persistent frame shell and swaps only `Image.Source` per frame.
- The control applies the frame to `CanvasViewModel` and raises `FramePublished`. The host composes the tile-grid and annotation overlays against the published camera snapshot.
- The render pipeline stays in the host: `RenderFrameAsync`, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, interest-set computation, and `FrameBufferPool` handoff.
- `RasterVisible` on the control is driven by the layer-visibility settings and toggles.
- `FrameShellWiringTests` now guard the control-owned shell and the `CanvasFrame` boundary.

## Validation Evidence

- Core suite: 170/170 pass.
- Windows suite: 18/18 pass.
- App Release build: 0 errors.
- `Validate-TaskTracker.ps1`: clean.

## Findings

- The pixelometer path was a live invariant violation before this batch. `TryGetPixelsNonBlocking` starts generation; hover triggered it. The new `TryGetResidentPixels` read path never starts generation.
- A pre-existing flaky test was repaired: `SampleImageGeneratorTests.TryGetPixelValue_ReturnsTileSampleForWorldCoordinate` failed 15/15 on clean HEAD because async generation raced the placeholder assertion. It now uses a blocked factory.
- The frame shell moved from `MainWindow` into `CanvasControl`. The no-flash invariant is preserved: the Viewbox child is assigned exactly twice (shell attach + detach).

## Recommended Next Step

Implement ICW-316 (physical assembly extraction): move `CanvasControl`, `CanvasViewModel`, and `CanvasFrame` into a WPF control library that depends only on `InfiniteCanvas.Core` and WPF. Keep `CanvasViewModel` in a non-WPF net10.0 project so tests are not retargeted. Update `CanvasScrollbarWiringTests` and `FrameShellWiringTests` paths atomically.

Then sequence ICW-314 (selection and tooltip ownership) after ICW-031 (typed metrics) lands, so the tooltip payload is typed before it moves into the control.
