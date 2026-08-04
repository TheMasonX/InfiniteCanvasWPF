---
id: ICW-311-move-canvas-interaction-logic-into-canvas-control
author: Copilot
key: ICW-311
title: Move canvas interaction logic from MainWindow into CanvasControl
status: Done
type: Task
priority: P2
tags:
  - canvas
  - decomposition
  - refactor
  - mvvm
dependsOn:
  - ICW-309
related:
  - ICW-022
  - ICW-098
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - tests/InfiniteCanvas.Tests/CanvasScrollbarWiringTests.cs
  - tests/InfiniteCanvas.Tests/CanvasViewModelTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-311-move-canvas-interaction-logic-into-canvas-control

## Summary

User requirement: move the panning and other canvas interaction logic out of `MainWindow` and into the `CanvasControl` component.

`MainWindow` still carries a full orphaned copy of the viewport interaction layer that `CanvasControl` already owns live: drag pan, anchor pan, scrollbar pan, and scrollbar geometry update. None of these handlers are wired in `MainWindow.xaml`. The live wheel-zoom handler also lives in `MainWindow` through the `PointerWheel` event.

This task deletes the orphaned duplicates, moves the anchor-pan exponential curve into the control's live `ApplyDeadZone`, and moves wheel zoom into the control.

## Scope

- Move `ComputeMinimumZoom` and the zoom floor into `CanvasViewModel` (`ComputeMinimumZoom`, `ApplyZoomFloor`).
- Move wheel zoom into `CanvasControl.OnViewportMouseWheel` using `ViewportZoomPolicy` and the canvas view model.
- Move the exponential anchor-pan curve (`_panExponent = 2.5`) into `CanvasControl.ApplyDeadZone`.
- Delete the orphaned viewport interaction methods, fields, and properties from `MainWindow`.
- Repurpose `MainWindow` wheel handling to update the pixelometer only.
- Add tests for the new `CanvasViewModel` zoom floor methods.

## Acceptance Criteria

- `MainWindow` has no drag-pan, anchor-pan, or scrollbar-pan handlers.
- `CanvasControl` handles drag pan, anchor pan, scrollbar pan, and wheel zoom.
- Anchor pan keeps the exponential dead-zone curve and stays finite.
- Wheel zoom produces the same camera result as before (policy, zoom floor, clamp, render request).
- Release build and full core test suite pass.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Passed. 0 errors. The pre-existing unused `_frameClaimantId` warning is being restored by a concurrent editor session and may reappear until that session stops editing `MainWindow.xaml.cs`.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Result: Passed. 154 passed, 0 failed.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasViewModel"`
- Result: Passed. Includes new `ComputeMinimumZoom` and `ApplyZoomFloor` tests.

## Notes

- The zoom presets, custom zoom UI, and fit-to-scene orchestration stay in `MainWindow`. Only the wheel interaction moves into the control.
- `ClampCameraToScene` stays in `MainWindow` and delegates the zoom floor to `CanvasSurface.ViewModel.ApplyZoomFloor`.
- The pixelometer stays in `MainWindow` because it reads scene tile data.
- `ComputeMinimumZoom` and the zoom floor moved to `CanvasViewModel` so the control and the window share one implementation.
- Orphan removal: deleted the orphaned drag-pan, anchor-pan, and scrollbar handlers and fields from `MainWindow`; removed the orphaned `ScrollbarHost`, `HorizontalTrack`, `HorizontalThumb`, `VerticalTrack`, `VerticalThumb`, and `AnchorVisual` properties from `CanvasControl`.
- The anchor-pan exponential curve (exponent 2.5) moved into the control's live `ApplyDeadZone`.
- Wheel zoom now runs inside `CanvasControl`; the window only observes the wheel for the pixelometer.

## Related Tasks

- ICW-309 (canvas view model decoupling)
- ICW-022 (MainWindow decomposition)
- ICW-098 (remove orphaned MainWindow scrollbar methods)
