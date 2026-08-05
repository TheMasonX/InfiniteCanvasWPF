---
id: ICW-312-canvas-data-source-abstraction
author: Copilot
key: ICW-312
title: Abstract canvas data sources behind injected services
status: Done
type: Story
priority: P2
tags:
  - canvas
  - architecture
  - dependency-injection
  - library-extraction
dependsOn:
  - ICW-311
related:
  - ICW-313
  - ICW-314
  - ICW-315
  - ICW-316
  - ICW-076
  - ADR-0007
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - src/InfiniteCanvas.Core/ICanvasItem.cs
  - src/InfiniteCanvas.Core/ICanvasSceneSource.cs
  - src/InfiniteCanvas.Core/ICanvasSpatialQuerySource.cs
  - src/InfiniteCanvas.Core/CanvasPixelSample.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Tests/CanvasSceneSourceContractsTests.cs
  - tests/InfiniteCanvas.Tests/CanvasBoundaryZeroReferenceTests.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-312-canvas-data-source-abstraction

## Summary

User requirement: image generation and other data must be services or injected abstractions so the canvas can move to a separate library and another app can supply its own data sources.

Today `MainWindow` constructs the spatial index, generates tiles, renders frames, and pushes the frame into `CanvasControl`. The control cannot run without the app's concrete pipeline.

Council decision (2026-08-04): adopt a non-generic, injected data-source boundary. Implemented 2026-08-04; all five evidence gates pass. The render-pipeline and frame-boundary migration is tracked separately as ICW-315.

## Audit Synthesis Correction (2026-08-04)

Audit synthesis finding F-001: `QueryVisible` exists on both `ICanvasSceneSource` and `ICanvasSpatialQuerySource`, and neither is consumed by the control. This is a duplicate item-query authority that must be resolved to one contract before ICW-314 consumes it and before the ICW-316 move. The resolution is gated in ICW-316A. `CanvasBoundaryZeroReferenceTests` asserts both source dependency properties and must change atomically with the consolidation.

## Scope (council-rescoped)

- Define `ICanvasItem` (string Id, SpatialBounds Bounds) in `InfiniteCanvas.Core`. No interaction members; ICW-314 extends it.
- Define `ICanvasSceneSource` (SceneBounds, TotalItemCount, `IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds)`, change event) in `InfiniteCanvas.Core`. Never expose `ISpatialIndexService<T>` here.
- Define `ICanvasSpatialQuerySource` as a non-generic wrapper over the app's live hybrid index. The canvas must never query the generic index itself.
- Replace `PublishFrame(UIElement)` with a `CanvasFrame` value: frozen raster `ImageSource` + items + viewport + counts. The canvas never touches the memory section, preserving the zero-copy handoff and ICW-P0-BUFFER-REUSE-SYNC.
- Inject the sources as dependency properties on `CanvasControl`, keeping the parameterless constructor for XAML and designer support.
- `CanvasViewModel` stays a passive, non-generic state holder. The control hosts and queries the sources; the host keeps the render pipeline.
- Add a non-blocking resident pixel-read contract behind the scene source. It must never initiate tile generation (closes the live ICW-P0-PIXELOMETER-READOUT violation).
- Tile-material reuse of `IBackgroundTileSource` is deferred to ICW-076 (the ADR-0005 materializer does not exist yet; zero implementers today).
- Make `MainWindow` an implementation of the sources over the existing pipeline.

## Acceptance Criteria (council evidence gates)

1. Zero-reference gate: `CanvasControl.xaml.cs` and `CanvasViewModel.cs` are free of `SampleAnnotation`, `SampleImageTile`, `LiveSpatialIndexService`, `InfiniteCanvas.Spatial`, and `InfiniteCanvas.Rendering`. Enforced by a scan test and a zero-error Release build.
2. Adapter gate: `MainWindow` implements the source interfaces. Full core suite and Release build pass with unchanged behavior.
3. Consumer-host gate: a new test drives `CanvasViewModel` from fake sources, referencing no app type.
4. Render-pipeline invariance gate: the slice diff touches only contracts, adapters, and injection. Core and Windows suites pass.
5. Tracker gate: `Validate-TaskTracker.ps1` is clean. Tile-scope decision and new follow-up tickets are recorded.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Command: `scripts/Validate-TaskTracker.ps1`

## Implementation Evidence (2026-08-04)

- New Core contracts: `ICanvasItem`, `ICanvasSceneSource`, `ICanvasSpatialQuerySource`, `CanvasPixelSample`.
- `SampleAnnotation` now implements `ICanvasItem`. `IReadOnlyList<out T>` covariance lets the spatial query result flow into the canvas contract without a mapping.
- `CanvasControl` exposes `SceneSource` and `SpatialQuerySource` dependency properties. Parameterless constructor is preserved for XAML and designer support.
- `CanvasViewModel` stays passive and non-generic. `ApplyFrame` gained an optional `IReadOnlyList<ICanvasItem>` so the consumer-host test can drive it from fake sources; `VisibleItems` is retained for ICW-314.
- `MainWindow` implements `ICanvasSceneSource` and `ICanvasSpatialQuerySource`, wires the dependency properties, and raises `SceneChanged` after regeneration.
- Non-blocking pixel read: `SampleImageTile.TryGetResidentPixels` reads only resident payloads and never starts generation. `MainWindow.UpdatePixelometer` now reads through `CanvasSurface.SceneSource`. This closes the live ICW-P0-PIXELOMETER-READOUT violation (hover no longer initiates tile acquisition). The legacy `TryReadPixelValue` generating path was deleted. `SampleImageTile.TryGetPixelsNonBlocking` fallback logic was factored into a shared `TryGetBestResidentMip` helper (no behavior change).
- Removed the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj` (council cleanup).
- Render pipeline untouched: `RenderFrameAsync`, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, and interest-set computation remain in the host.

## Gate Results

1. Zero-reference gate: `CanvasBoundaryZeroReferenceTests` scans both boundary files; no forbidden tokens. Release build 0 errors.
2. Adapter gate: `MainWindow` implements both source interfaces; full core suite and Release build pass with unchanged behavior.
3. Consumer-host gate: `CanvasSceneSourceContractsTests` drives `CanvasViewModel` from fake sources with no app types.
4. Render-pipeline invariance gate: the diff touches contracts, adapters, and injection only (plus the required non-blocking tile read). Core 167/167, Windows 18/18, Release App build 0 errors.
5. Tracker gate: `Validate-TaskTracker.ps1` clean. Follow-ups ICW-315 and ICW-316 recorded.

## Notes

- Sequence the change so the app keeps working at every step (strangler-fig).
- Keep `CanvasViewModel` in a non-WPF net10.0 project so tests are not retargeted.
- Remove the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj` inside the zero-reference gate.
- Keep the render pipeline, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, and interest-set computation in the host.
- The `SampleImageTile.TryGetResidentPixels` addition lives in `InfiniteCanvas.Rendering` because the existing `TryGetPixelsNonBlocking` starts generation, which the council explicitly ruled out for the pixelometer. It is supporting infrastructure for the adapter, not a render-pipeline change.
- Full council report: docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md.

## Related Tasks

- ADR-0007 (component boundary)
- ICW-313 (input handler abstraction)
- ICW-314 (selection and tooltip ownership)
- ICW-315 (render-pipeline and frame-boundary migration)
- ICW-316 (assembly extraction)
- ICW-076 (ADR-0005 tile materializer, tile-source dependency)
