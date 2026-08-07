# Wave O Handoff, Layer Visibility

Date: 2026-08-06
Status: Complete

## Critical review

The repository was clean and synchronized before Wave O.
HEAD and origin/main were both `5a0c138`.
Wave N source and tests matched its handoff claims.
The review found two ICW-093 gaps.

- `ShowSparseImageTiles` was persisted but the renderer received `_showImageTiles`.
- `ShowBoxes` was persisted but no UI control or composition branch consumed it.

## Changes

- Added separate controls for annotation boxes and sparse image tiles.
- Loaded both settings into the controls and private render state.
- Saved both settings through `CanvasUserSettings`.
- Passed `_showSparseImageTiles` to `GenerateFrozenBitmap`.
- Applied `ShowBoxes` to annotation rectangle stroke and fill.
- Kept labels independent from box visibility.
- Kept raster visibility true when any raster layer remains enabled.
- Added `LayerVisibilityWiringTests`.
- Updated ICW-093, the requirements registry, and both task trackers.

## Validation

- Focused visibility and settings tests pass 6/6.
- App Release build passes with the existing `_frameClaimantId` warning only.
- Full core suite passes 192/192.
- Full Windows suite passes 25/25.
- Task tracker validation passes 220 files, with 5 legacy markdown files skipped.

## Files

- `src/InfiniteCanvas.App/MainWindow.xaml`
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
- `tests/InfiniteCanvas.Tests/LayerVisibilityWiringTests.cs`
- `docs/tasks/tickets/ICW-093-layer-visibility-and-background-target-controls.md`
- `docs/requirements/functional-requirements-and-invariants.md`

## Next step

Wave O is ready to commit and push.
