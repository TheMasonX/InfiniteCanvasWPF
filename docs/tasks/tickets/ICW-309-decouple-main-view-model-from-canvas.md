---
id: ICW-309-decouple-main-view-model-from-canvas
author: Copilot
key: ICW-309
title: Decouple MainViewModel from canvas state
status: Done
type: Task
priority: P1
tags:
  - mvvm
  - canvas
  - architecture
  - decomposition
dependsOn: []
related:
  - ICW-022
  - ICW-017
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - src/InfiniteCanvas.ViewModels/MainViewModel.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
  - tests/InfiniteCanvas.Tests/CanvasViewModelTests.cs
  - tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-309-decouple-main-view-model-from-canvas

## Summary

User requirement: `MainViewModel` must not control the canvas. The canvas must be a separate, self-contained component because it will move to a separate library.

Today `MainViewModel` owns canvas viewport state (`VisibleItemCount`, `TotalItemCount`, `ApplyViewportState`) that `MainWindow.RenderFrameAsync` updates every frame. The canvas component (`CanvasControl` + `CanvasViewModel`) owns the camera but not the per-frame counts, while a dead generic `CanvasViewportViewModel<T>` tracks the same counts with no UI consumer.

This task consolidates all per-frame canvas state into `CanvasViewModel`, deletes the dead `CanvasViewportViewModel<T>`, and removes all canvas state from `MainViewModel`.

## Scope

- Add `VisibleItemCount`, `TotalItemCount`, and `ApplyFrame` to `CanvasViewModel`.
- Delete `CanvasViewportViewModel<T>` and its tests.
- Remove `VisibleItemCount`, `TotalItemCount`, and `ApplyViewportState` from `MainViewModel`.
- Bind the window header counts to `CanvasSurface.ViewModel`.
- Update `MainWindow` to call `CanvasSurface.ViewModel.ApplyFrame` once per frame.
- Port `ApplyFrame` tests to `CanvasViewModelTests`.

## Acceptance Criteria

- `MainViewModel` contains no canvas or viewport state.
- `CanvasViewModel` is the single owner of per-frame canvas state.
- `CanvasViewportViewModel<T>` no longer exists in the solution.
- The header count bindings still display visible and total items.
- Focused tests pass.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Passed. 0 errors. Pre-existing `CS0169` warning on unused `_frameClaimantId` remains.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasViewModel|FullyQualifiedName~MainViewModel|FullyQualifiedName~CanvasScrollbarWiring"`
- Result: Passed. 8 passed, 0 failed.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-build`
- Result: Passed. 149 passed, 0 failed.

## Notes

- `LastSnapshotPublishedAtUtc` is deleted with `CanvasViewportViewModel<T>`. It has zero consumers in the app (repo-wide grep). The `LiveSpatialIndexService<T>` downcast that fed it is gone too.
- The canvas component keeps its view model non-generic and free of spatial-index coupling, which is the shape needed for extraction into a separate library.
- `MainWindow.RenderFrameAsync` now calls `CanvasSurface.ViewModel.ApplyFrame(viewport, visibleCount, totalCount)` once per frame. The header count bindings read `CanvasSurface.ViewModel` via `ElementName=CanvasSurface`.
- The pre-existing uncommitted `TileWorkCoordinatorTests.cs` change (ICW-205 priority queue work) is unrelated and was left untouched.

## Related Tasks

- ICW-022 (MainWindow decomposition and tests)
- ICW-017 (RefreshCommand dead path removal / ApplyFrame canonicalization)
