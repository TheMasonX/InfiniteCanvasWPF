# ICW-312 Council Review Complete

## Status

Council complete. No implementation yet, per user instruction.

## Decision

The canvas data-source boundary uses non-generic contracts in `InfiniteCanvas.Core`: `ICanvasItem` (Id + Bounds), `ICanvasSceneSource`, and `ICanvasSpatialQuerySource`. `CanvasControl` hosts the sources as dependency properties. `MainWindow` implements them. Frame publication becomes a `CanvasFrame` value. Tile-material reuse waits for ICW-076. The render pipeline stays in the host.

## Seat Outcomes

- Viewport Architecture: PublishFrame(UIElement) is not a library boundary; pixelometer must be a non-blocking resident read.
- Spatial Indexing: canvas never queries the generic index; visible count per frame, total count a source property.
- MVVM/Settings: sources as dependency properties; view model stays passive and non-generic.
- Sequencing: ICW-312 to ICW-314 order valid; ICW-313 is scheduling; two follow-ups created (ICW-315, ICW-316).

## Artifacts

- Council report: docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
- Recovery workspace: D:/temp/icw-council/ (manifest + four seat directories with seat-report.md and notes.md)
- Tickets updated: ICW-312 (In Review), ICW-314 (depends on ICW-031), ICW-315 (new), ICW-316 (new)
- ADR-0007 updated with council refinement.

## Next Step

Approve the five evidence gates in ICW-312, then implement the first slice (contracts in Core + MainWindow adapter) with the gates as acceptance criteria. Confirm who owns ICW-315 before it starts.
