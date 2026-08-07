# Wave N Handoff — Settings Validation and Sparse-Tile Gate

## Date

2026-08-06

## Status

Complete and pushed.

## Summary

- Implemented the shared settings validators in Core.
- Threaded the persisted sparse-tile threshold through the app render path.
- Gated missing tile generation below the threshold in the renderer.
- Added regression tests for both acceptance criteria.

## Commits

- `79d0cb2` Wave M (GDI+ cancellation storm).
- `b89aa55` CI pipeline (prior commit, now pushed).
- Wave N commit follows this note.

## Changes

1. Core: `CanvasUserSettings.MaxObjectsPerTile` constant.
2. Core: `ValidateObjectsPerTile` and `ValidateMinimumSparseTilePixelSize` functions.
3. Core: `IsValid` uses both functions.
4. Rendering: `SampleImageGenerator.MaxObjectsPerTile` references the Core constant.
5. Rendering: both `GenerateSet` overloads use `ValidateObjectsPerTile`.
6. Rendering: `DrawTile` gates generation with `ShouldGenerateForPixelSize` before any cache lookup.
7. App: new `MinimumSparseTilePixelSizeSliderTextBox` in the scene material panel.
8. App: load, save, UI apply, and frame generation all thread the threshold.
9. App: `TryReadGenerationOptions` uses the shared validators.
10. Tests: invalid-file fallback and non-finite threshold checks.
11. Tests: Windows renderer proves no generation below the threshold.

## Validation Evidence

- Core settings tests: 5/5.
- Full core suite: 191/191.
- Windows renderer tests: 13/13.
- Full Windows suite: 25/25.
- App Release build: succeeded (existing `_frameClaimantId` warning only).
- Task tracker validator: clean.
- `git diff --check`: clean.

## Next Step

Select the next open task. Candidates:

- ICW-093 (layer visibility and background target controls, In Progress).
- ICW-132 (rendering stage instrumentation).
- ICW-096 (scrollbar and mip fallback review).
