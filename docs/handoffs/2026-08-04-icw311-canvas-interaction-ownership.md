# ICW-311: Canvas Interaction Ownership

## Status

Complete. `CanvasControl` now owns all viewport interaction.

## What Changed

- `CanvasControl` handles drag pan, anchor pan, scrollbar pan, and wheel zoom.
- The anchor-pan exponential curve (exponent 2.5) moved into the control's live `ApplyDeadZone`. It is sign-preserving, so negative deltas stay finite.
- `ComputeMinimumZoom` and the zoom floor moved into `CanvasViewModel`. Both the control and the window share one implementation.
- `MainWindow` keeps the zoom presets, custom zoom UI, fit-to-scene orchestration, pixelometer, and scene pipeline.
- All orphaned pan, scrollbar, and anchor code was deleted from `MainWindow`.
- Orphaned public properties were removed from `CanvasControl` (`ScrollbarHost`, `HorizontalTrack`, `HorizontalThumb`, `VerticalTrack`, `VerticalThumb`, `AnchorVisual`).

## Validation

- Release app build: 0 errors.
- Full core test suite: 154 passed, 0 failed.
- New `CanvasViewModel` tests cover `ComputeMinimumZoom` and `ApplyZoomFloor`.

## Architecture Direction

The user wants the canvas to become a reusable component that another app can pull in with its own data sources. The current gap:

- Selection and tooltips still live in `MainWindow` against `SampleAnnotation`.
- Data sources (generation, spatial index, frames) are not injected.
- Input handlers are not yet `IInputHandler` classes (explicitly future work).

Captured in ADR-0007 and tickets ICW-312, ICW-313, and ICW-314.

## Note

A concurrent editor session keeps restoring the unused `_frameClaimantId` field in `MainWindow.xaml.cs`. The removal did not persist. This is a pre-existing benign warning. It will disappear once that session stops editing the file.
