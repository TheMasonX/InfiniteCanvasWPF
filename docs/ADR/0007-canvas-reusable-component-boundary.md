# ADR-0007: Canvas as a Reusable Component with Injected Data Sources

## Status

Proposed

## Context

The canvas must become a reusable component. Another application must be able to pull it in and supply its own data sources. The canvas must own viewport interaction: pan, zoom, scrollbars, object selection, and tooltip hover.

Today `CanvasControl` owns pan, zoom, and scrollbar interaction. It does not own selection, tooltips, or data. `MainWindow` still owns:

- Scene generation (`SampleImageGenerator.GenerateSet`).
- The spatial index (`LiveSpatialIndexService<SampleAnnotation>`).
- Frame rendering and publication (`PublishFrame`).
- Selection state (`_selectedAnnotationId`) and annotation visuals built against `SampleAnnotation`.
- Tooltip creation (`DeferredAnnotationToolTip`).

`CanvasControl` references `InfiniteCanvas.ViewModels` and `InfiniteCanvas.Core`. It depends on concrete application types such as `SampleAnnotation` through the frame it receives.

## Decision

Define the canvas as a component with a stable, app-agnostic boundary. The canvas must not reference application data types.

1. **Data sources are injected.** The canvas receives content through interfaces. Candidates: `ICanvasSceneSource` for scene bounds and items, the existing `IBackgroundTileSource` (ADR-0005) for tile material, and a spatial query source for visible items. `MainWindow` implements these and supplies them through the control constructor or the view model.
2. **Items are abstract.** The canvas interacts with an item contract such as `ICanvasItem` that exposes world bounds, hit testing, tooltip payload, and a visual template. `SampleAnnotation` becomes one application implementation.
3. **Selection and tooltip hover move into the canvas.** The control owns hit testing, selection state, and tooltip display against the item contract.
4. **Input handlers are abstracted.** Pan, zoom, anchor pan, scrollbar, and wheel handlers become `IInputHandler` implementations registered on the control. This is future work.
5. **The component ships as a separate assembly.** `CanvasControl` and `CanvasViewModel` move into a WPF control library that depends only on `InfiniteCanvas.Core` (or a new contracts assembly) and WPF.

The existing interaction ownership from ICW-311 stays: the control owns pan, zoom, and scrollbars.

## Council Refinement (2026-08-04)

A four-seat council reviewed this boundary. No implementation yet. Decisions:

1. Contracts live in `InfiniteCanvas.Core`: `ICanvasItem` (Id + Bounds), `ICanvasSceneSource`, and `ICanvasSpatialQuerySource`. Revisit the location before the assembly move.
2. Sources are dependency properties on `CanvasControl`. The parameterless constructor stays for XAML and designer support.
3. The frame boundary becomes `CanvasFrame` (frozen raster + items + viewport + counts). The canvas never touches the backing memory section.
4. The render pipeline, tile coordinator, cache budget, epoch guard, and interest-set computation stay in the host.
5. Tile-material reuse of `IBackgroundTileSource` waits for ICW-076.
6. ICW-313 (IInputHandler) is a scheduling guard, not a hard dependency on ICW-312.

Full report: docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md.

## Consequences

- `MainWindow` shrinks to orchestration: generate a scene, wrap it in the injected sources, and let the canvas consume it.
- Another application can host the canvas and supply its own item, tile, and spatial sources.
- Selection and tooltip behavior become deterministic and testable inside the control.
- The change is large. It must be sequenced so the app keeps working at each step. ICW-312, ICW-315, ICW-314, and ICW-316 define the sequence.

## Related

- ICW-309, ICW-311 (completed component boundary work)
- ICW-312 (data source abstraction)
- ICW-313 (input handler abstraction)
- ICW-314 (selection and tooltip ownership)
- ICW-315 (frame boundary migration)
- ICW-316 (assembly extraction)
- ADR-0005 (source-agnostic tile boundary)
