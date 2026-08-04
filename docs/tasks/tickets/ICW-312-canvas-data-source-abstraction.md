---
id: ICW-312-canvas-data-source-abstraction
author: Copilot
key: ICW-312
title: Abstract canvas data sources behind injected services
status: In Review
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
  - src/InfiniteCanvas.Core/SpatialBounds.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-312-canvas-data-source-abstraction

## Summary

User requirement: image generation and other data must be services or injected abstractions so the canvas can move to a separate library and another app can supply its own data sources.

Today `MainWindow` constructs the spatial index, generates tiles, renders frames, and pushes the frame into `CanvasControl`. The control cannot run without the app's concrete pipeline.

Council decision (2026-08-04): adopt a non-generic, injected data-source boundary. No implementation yet.

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

## Notes

- Sequence the change so the app keeps working at every step (strangler-fig).
- Keep `CanvasViewModel` in a non-WPF net10.0 project so tests are not retargeted.
- Remove the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj` inside the zero-reference gate.
- Keep the render pipeline, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, and interest-set computation in the host.
- Full council report: docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md.

## Related Tasks

- ADR-0007 (component boundary)
- ICW-313 (input handler abstraction)
- ICW-314 (selection and tooltip ownership)
- ICW-315 (render-pipeline and frame-boundary migration)
- ICW-316 (assembly extraction)
- ICW-076 (ADR-0005 tile materializer, tile-source dependency)
