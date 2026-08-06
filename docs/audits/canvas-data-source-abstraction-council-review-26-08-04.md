# Council Review: Canvas Data Source Abstraction (ICW-312)

## Decision

Adopt a non-generic, injected data-source boundary for the canvas: `ICanvasItem` (Id + Bounds), `ICanvasSceneSource` (SceneBounds, TotalItemCount, QueryVisible, change event), and `ICanvasSpatialQuerySource` live in `InfiniteCanvas.Core`. `CanvasControl` hosts the sources as dependency properties. `MainWindow` implements them. Frame publication moves to a `CanvasFrame` value (frozen raster + items + viewport + counts). Tile-material reuse of `IBackgroundTileSource` is deferred until ICW-076. The render pipeline stays in the host. No implementation yet.

## Evidence Reviewed

- docs/tasks/tickets/ICW-312-canvas-data-source-abstraction.md
- docs/tasks/tickets/ICW-313-input-handler-abstraction.md
- docs/tasks/tickets/ICW-314-canvas-selection-and-tooltip-ownership.md
- docs/ADR/0007-canvas-reusable-component-boundary.md
- docs/ADR/0005-source-agnostic-background-tile-mips.md
- docs/ADR/0003-live-hybrid-spatial-indexing.md
- docs/requirements/functional-requirements-and-invariants.md
- docs/tasks/active-tasks.md
- DesignDoc.md
- src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
- src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
- src/InfiniteCanvas.Spatial/ISpatialIndexService.cs
- src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs
- src/InfiniteCanvas.ViewModels/InfiniteCanvas.ViewModels.csproj

Seat artifacts: D:/temp/icw-council/*/seat-report.md (recovery root preserved).

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---|---|
| Viewport Architecture | Replace `PublishFrame(UIElement)` with `ApplyFrame(CanvasFrame)`; keep interest computation and render pipeline in the host; add a non-blocking resident pixel-read contract; reuse `IBackgroundTileSource` for async tile material paired with a synchronous resident read | 0.80-0.90 | PublishFrame(UIElement) is not a valid library boundary; pixelometer hover currently initiates tile generation (live ICW-P0-PIXELOMETER-READOUT violation) |
| Spatial Indexing | Canvas consumes visible items from an injected `ICanvasSceneSource`, never queries `ISpatialIndexService<T>`; item contract is Id + Bounds only; visible count derives per frame, total count is a source property | 0.85-0.92 | Canvas-side index querying would break the non-generic/spatial-free invariant and the library goal |
| MVVM/Settings | Sources are dependency properties on `CanvasControl` (XAML-instantiable, parameterless ctor preserved); `CanvasViewModel` stays a passive, non-generic state holder; versioned scene-swap handshake that never touches settings; busy state moves to a control-level observable | 0.85-0.98 | Frame-publish coupling is not removed by scene/spatial/tile sources alone; `IBackgroundTileSource` has zero implementers |
| Sequencing | ICW-312 to ICW-314 order is correct; ICW-313 is a scheduling guard, not a hard dependency; rescope ICW-312 tile reuse off; define five evidence gates; two follow-ups needed (render-pipeline migration, assembly move) | 0.80-1.00 | Render-pipeline migration is unticketed; the tile-source reuse claim is not implementable (zero implementers) |

## Synthesis

### What changes now (design records only, no implementation)

1. **Rescope ICW-312** to scene and spatial sources only. Tile-material reuse of `IBackgroundTileSource` becomes a dependency on ICW-076 (the ADR-0005 materializer does not exist yet).
2. **Define the contracts in `InfiniteCanvas.Core`**: `ICanvasItem` (string Id, SpatialBounds Bounds), `ICanvasSceneSource` (SceneBounds, TotalItemCount, `IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds)`, change event), and `ICanvasSpatialQuerySource`. No generic index through the view model.
3. **Decide the frame boundary**: replace `PublishFrame(UIElement)` with a `CanvasFrame` value carrying a frozen `ImageSource` raster plus items, viewport, and counts. The canvas never touches the memory section, so the zero-copy handoff and ICW-P0-BUFFER-REUSE-SYNC stay intact.
4. **Injection via dependency properties** on `CanvasControl`, keeping the parameterless constructor for XAML and designer support.
5. **Keep the render pipeline in the host**: `RenderFrameAsync`, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, and interest-set computation stay in `MainWindow`. The canvas exposes the viewport; the host queries and pushes results.
6. **Add a non-blocking pixel-read contract** behind the scene source. It must never initiate tile generation. This closes a live ICW-P0-PIXELOMETER-READOUT violation.
7. **Approved evidence gates** (from the sequencing seat) become ICW-312 acceptance criteria.

### What is deferred

- **ICW-313 (IInputHandler)**: stays deferred by user instruction. It does not depend on ICW-312 technically; it is a scheduling guard. Land it after ICW-314 so click-to-select has a home.
- **Tile-material source reuse**: waits for ICW-076 (ADR-0005 materializer).
- **Selection and tooltips**: ICW-314, after ICW-312. Add ICW-031 (typed annotation metrics) as a dependency for the tooltip payload.
- **Assembly move**: new ICW-316. Keep `CanvasViewModel` in a non-WPF net10.0 project so tests are not retargeted.
- **Render-pipeline migration**: new ICW-315. This is the largest slice and must be sequenced with its own gates.
- **Pixelometer mip policy** (camera-selected vs mip-0): defer; preserve current behavior in the non-blocking read contract.

### Dissent

- **Frame contract**: the MVVM seat asked whether to retain `PublishFrame(UIElement)` or add `ICanvasFrameComposer`. The synthesis chooses a `CanvasFrame` value as the middle path: it removes the concrete-UIElement boundary without moving rendering into the canvas. This is a judgement call; revisit if the canvas must own composition (ICW-314).
- **Contract location**: Core vs a new `InfiniteCanvas.Contracts` assembly. The synthesis keeps contracts in Core for ICW-312 and revisits before the assembly move (ICW-316), because Core already holds `SpatialBounds`, `CameraTransform`, and `ISpatialEntity`.

## Acceptance Criteria (evidence gates for ICW-312)

1. **Zero-reference gate**: `CanvasControl.xaml.cs` and `CanvasViewModel.cs` are free of `SampleAnnotation`, `SampleImageTile`, `LiveSpatialIndexService`, `InfiniteCanvas.Spatial`, and `InfiniteCanvas.Rendering`. Enforced by a scan test and a zero-error Release build.
2. **Adapter gate**: `MainWindow` implements the source interfaces over the existing pipeline. Full core suite and Release build pass with unchanged behavior.
3. **Consumer-host gate**: a new test drives `CanvasViewModel` from fake sources, referencing no app type.
4. **Render-pipeline invariance gate**: the slice diff touches only contracts, adapters, and injection. Core and Windows suites pass.
5. **Tracker gate**: `Validate-TaskTracker.ps1` is clean. Tile-scope decision and new follow-up tickets are recorded.

## Open Questions

1. Does the view model or the control store the visible item list for ICW-314 hit testing? (Recommended: the view model, as `IReadOnlyList<ICanvasItem>` in `ApplyFrame`.)
2. Does the scene-source change event drive re-render through the host, or does the host poll?
3. Is task-tracker.md canonical or legacy for epic tracking? (Recommendation: treat active-tasks.md as live; sync task-tracker.md only if it is still a source of truth.)
4. Who owns the render-pipeline migration (ICW-315), and does it precede ICW-314?

## Follow-up Tickets

- ICW-315: render-pipeline and frame-boundary migration to `CanvasFrame`.
- ICW-316: physical assembly extraction of the canvas component (ADR-0007 decision 5).
- Cleanup noted: remove the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj` inside the zero-reference gate.

