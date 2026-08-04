# ICW-309: Canvas View Model Decoupling

## Status

Complete. The canvas is now a self-contained component. `MainViewModel` does not control the canvas.

## What Changed

- `CanvasViewModel` now owns all per-frame canvas state. It exposes `VisibleItemCount`, `TotalItemCount`, and `ApplyFrame(viewport, visibleCount, totalCount)`.
- `MainWindow.RenderFrameAsync` publishes the frame result once through `CanvasSurface.ViewModel.ApplyFrame`.
- The window header count bindings read `CanvasSurface.ViewModel` via `ElementName=CanvasSurface`.
- `MainViewModel` no longer owns `VisibleItemCount`, `TotalItemCount`, or `ApplyViewportState`. It holds only app settings.
- The dead generic `CanvasViewportViewModel<T>` is deleted. It had no UI consumer. Its `LastSnapshotPublishedAtUtc` property and the `LiveSpatialIndexService<T>` downcast are gone.

## Why

The user requires the canvas to be a separate component for future extraction into its own library. The old design split canvas state across `MainViewModel` and the unbound `CanvasViewportViewModel<T>`, which audits 10 and 11 flagged as duplication.

## Validation

- Release app build: 0 errors. Pre-existing `CS0169` warning on `_frameClaimantId` remains.
- Focused tests (CanvasViewModel, MainViewModel, CanvasScrollbarWiring): 8 passed.
- Full core suite: 149 passed, 0 failed.

## Next Step

Extract `CanvasControl` and `CanvasViewModel` into a separate assembly when the app surface is stable. The view model is non-generic and free of spatial-index coupling, so the move is ready.

## Note

The pre-existing uncommitted change in `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs` belongs to in-flight ICW-205 work. It is unrelated to this task and was left untouched.
