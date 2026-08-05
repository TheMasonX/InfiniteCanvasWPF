---
id: ICW-315-render-pipeline-frame-boundary-migration
author: Copilot
key: ICW-315
title: Migrate render pipeline to the CanvasFrame boundary
status: Done
type: Story
priority: P1
tags:
  - canvas
  - rendering
  - frame-boundary
  - library-extraction
dependsOn:
  - ICW-312
related:
  - ICW-316
  - ICW-021
  - ICW-P0-BUFFER-REUSE-SYNC
  - ADR-0007
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasFrame.cs
  - tests/InfiniteCanvas.Tests/FrameShellWiringTests.cs
  - docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-315-render-pipeline-frame-boundary-migration

## Summary

Council finding: `PublishFrame(UIElement)` is not a valid library boundary. It receives a frame tree built against `SampleAnnotation` in `MainWindow.BuildFrameVisual`. Replace it with a `CanvasFrame` value so the canvas owns a stable frame contract and the host keeps the deterministic render pipeline.

The render-pipeline migration is the largest canvas chunk and is currently unticketed. This task owns it.

## Scope

- Define `CanvasFrame`: frozen raster `ImageSource` plus items, viewport, and counts.
- Replace `CanvasSurface.PublishFrame(UIElement)` with the `CanvasFrame` boundary.
- Keep the zero-copy buffer handoff intact. The canvas must never touch the backing memory section (ICW-P0-BUFFER-REUSE-SYNC, ICW-021).
- Keep `RenderFrameAsync`, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, and interest-set computation in the host.
- The canvas raises `ViewportChanged`; the host queries sources and pushes `CanvasFrame` results.

## Acceptance Criteria

- The canvas receives a `CanvasFrame`, never a concrete `UIElement` tree built from `SampleAnnotation`.
- Behavior is unchanged: no-flash invariants (ICW-205), stale-frame epoch guard (ICW-100), and Wave-D ordering hold.
- Release build and full core and Windows test suites pass.
- No new allocation or copy of the raster memory section on the boundary.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Audit Synthesis Correction (2026-08-04)

Audit synthesis finding F-003: `CanvasFrame` claims snapshot semantics but borrows mutable lists, has no `IsFrozen` check, no revision identity, and does not validate raster dimensions against `ImageSource` metadata. `Stretch.Fill` is the only display path, so a dimension mismatch would stretch silently. Frame immutability, count-consistency validation, raster-dimension validation, and revision identity are gated in ICW-316A before the type moves.

## Implementation Evidence (2026-08-04)

- `CanvasFrame` defined in `src/InfiniteCanvas.App/Controls/CanvasFrame.cs`: frozen raster `ImageSource` plus items, viewport, visible/total counts, and pixel dimensions. The control never touches the backing memory section.
- `CanvasControl.PublishFrame(CanvasFrame)` replaces `PublishFrame(UIElement)`. The control owns the persistent frame shell (`EnsureFrameShell` / `DetachFrameShell`), swaps only `Image.Source` per frame, applies the frame to `CanvasViewModel`, and raises `FramePublished`.
- `MainWindow` builds a `CanvasFrame` per frame and publishes through the control. It keeps the render pipeline: `RenderFrameAsync`, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, interest-set computation, and the `FrameBufferPool` handoff.
- Overlay composition stays host-composed (ICW-314 decision pending): `MainWindow.OnCanvasFramePublished` repopulates the control-owned tile-grid and annotation canvases with the published camera snapshot.
- `RasterVisible` on the control is driven by the layer-visibility settings and toggles.
- `FrameShellWiringTests` now guards the control-owned shell: shell attach/detach exactly twice, `PublishFrame(CanvasFrame)` (no UIElement), no raster memory-section references, and MainWindow publishing a `CanvasFrame` with no direct Viewbox child assignment.

## Gate Results

- Canvas receives a `CanvasFrame`; the `PublishFrame(UIElement)` overload is gone (scan test enforced).
- Behavior unchanged: no-flash shell invariant (attach/detach exactly twice), stale-frame epoch guard stays in the host, Wave-D ordering untouched.
- Core 170/170, Windows 18/18, Release App build 0 errors.
- No allocation or copy of the raster memory section on the boundary; the frozen `ImageSource` is handed to the control by reference.
- Flake repair: `SampleImageGeneratorTests.TryGetPixelValue_ReturnsTileSampleForWorldCoordinate` was a pre-existing async race (fails 15/15 on clean HEAD). It now uses a blocked factory so the placeholder assertion is deterministic.

## Notes

- This is the highest-risk slice. Sequence it alone with its own gates.
- Decide with the council whether the canvas later owns composition (ICW-314) or stays host-composed.

## Related Tasks

- ICW-312 (data source abstraction)
- ICW-316 (assembly extraction)
- ADR-0007 (component boundary)
