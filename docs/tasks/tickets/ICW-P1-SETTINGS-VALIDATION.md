---
id: ICW-P1-SETTINGS-VALIDATION
author: External Audit (Integration-1)
key: ICW-P1-SETTINGS-VALIDATION
title: Create single validation function per option field used by all entry paths
status: Done
type: Bug
priority: P1
tags:
  - settings
  - validation
  - startup
  - crash
dependsOn: []
related:
  - ICW-099
  - ICW-022
  - ICW-P1-SETTINGS-SCOPE
links:
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-08-06
---

# ICW-P1-SETTINGS-VALIDATION — Create single validation function per option field used by all entry paths

## Summary

**Pattern-level fix** for a recurring defect: settings validation is duplicated across code paths, some paths lack upper bounds, and one setting (`MinimumSparseTilePixelSize`) is validated and stored but never consumed. Two confirmed instances documented in the external audit.

**Instance 1 — `ObjectsPerTile` missing upper bound:**
- `CanvasUserSettings.IsValid` checks `ObjectsPerTile >= 0` only — no upper bound.
- `SampleImageGenerator.GenerateSet` throws `ArgumentOutOfRangeException` above `MaxObjectsPerTile = 256`.
- A hand-edited or corrupted `settings.json` with `ObjectsPerTile: 500` passes load validation (IsValid returns true) and crashes on the very first `RegenerateSceneAsync` call — a startup crash loop since the bad file persists.
- **Confidence: 95%** (exact validation code read).

**Instance 2 — `MinimumSparseTilePixelSize` validated but never consumed:**
- `CanvasUserSettings.IsValid` validates `MinimumSparseTilePixelSize` (`>= 0 and <= 4096`).
- The value is persisted and round-trips through `CanvasUserSettingsStore.Load`.
- Grep confirms it is referenced **only** inside `CanvasUserSettings.cs` itself — never read by `MainWindow`, `ZeroCopyBitmapFactory`, or anywhere in the render path.
- Worse: `ZeroCopyBitmapFactory.DrawTile` already has a same-named parameter (`minimumSparseTilePixelSize`, default `0`) that is **passed through from `GenerateFrozenBitmap` but never read inside `DrawTile`'s body** — meaning even if `MainWindow` is fixed to pass the setting through, `DrawTile` itself needs a real implementation.
- **Confidence: 95%** (grep-confirmed, method body read).

**Instance 3 — Duplicate validation in `TryReadGenerationOptions`:**
- `MainWindow.xaml.cs:1370-1411` re-implements the same `0..MaxObjectsPerTile` check independently — a third copy of validation logic that should use a shared function.
- **Confidence: 90%** (exact line range confirmed).

## Root Cause

There is no single shared validation function per setting field. Each consumer independently re-implements bounds checks with inconsistent coverage:
- `CanvasUserSettings.IsValid`: checks lower bounds but not upper bounds.
- `SampleImageGenerator.GenerateSet`/`GenerateSet(GeneratorOptions)`: checks both bounds but throws rather than reporting validation errors.
- `TryReadGenerationOptions`: checks both bounds but is a third independent copy.

## Scope

### Required Changes

1. **Add `ObjectsPerTile <= MaxObjectsPerTile` to `CanvasUserSettings.IsValid`:**
   - Either reference `SampleImageGenerator.MaxObjectsPerTile` (creates dependency from Core → Rendering) or duplicate the constant as `CanvasUserSettings.MaxObjectsPerTile = 256` with a comment cross-referencing `SampleImageGenerator.MaxObjectsPerTile` and a test asserting they stay equal.

2. **Implement the skip-below-threshold logic inside `DrawTile`:**
   - Use the existing (currently-ignored) `minimumSparseTilePixelSize` parameter.
   - Compare the tile's projected screen-space size against the threshold.
   - Reuse/share logic from `SampleImageTile.ShouldGenerateForPixelSize` (which already computes this).
   - When below threshold, render only the placeholder value, skipping the (currently unconditional) generation trigger.

3. **Wire `MainWindow.RenderFrameAsync`'s call to `GenerateFrozenBitmap`** to pass `_mainViewModel`'s persisted `MinimumSparseTilePixelSize`.
   - Needs a UI control too (slider or numeric up/down in settings panel), or can ship headless first with just the persisted value.

4. **Consolidate validation into a single shared function:**
   - `CanvasUserSettings.ValidateObjectsPerTile(int value)` called from:
     - `CanvasUserSettings.IsValid`
     - `TryReadGenerationOptions`
     - `SampleImageGenerator.GenerateSet` (at least the validation part, not the throw — use a Try-pattern or return validation errors)

5. **Background noise settings fix** (already done in Sprint 1 Wave A, but needs cross-reference):
   - `RegenerateSceneAsync` now snapshots `_mainViewModel` background-noise settings before `InitializeSpatialState()` overwrites it.
   - Cross-reference this in ICW-022's and ICW-P1-SETTINGS-SCOPE's tracker entries.

### Test Requirements

6. **Round-trip test:** Create a settings file with `ObjectsPerTile = 500`, assert `IsValid == false` and `Load` falls back to defaults instead of crashing on first generate.

7. **Consumption test:** Assert every persisted setting in `CanvasUserSettings` is verifiably consumed in the render/generation call graph. This would have caught `MinimumSparseTilePixelSize` not reaching `GenerateFrozenBitmap`.

8. **Shared-validation test:** Assert `ValidateObjectsPerTile` is called from all three entry paths and returns consistent results.

### Acceptance Criteria

- `ObjectsPerTile = 500` in settings file no longer causes startup crash loop — `IsValid` returns false, `Load` falls back to defaults.
- `MinimumSparseTilePixelSize` is passed from persisted settings through `GenerateFrozenBitmap` to `DrawTile` — and `DrawTile` uses it to skip generation for tiles below the threshold.
- A single shared validation function per field replaces the three independent copies.
- Validation tests cover all three entry paths.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Core/CanvasUserSettings.cs` | Add upper bound check to `IsValid`, add shared validation functions, add `MaxObjectsPerTile` constant |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Wire `MinimumSparseTilePixelSize` into render path, replace `TryReadGenerationOptions` validation with shared function |
| `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs` | Implement skip-below-threshold logic in `DrawTile` using `minimumSparseTilePixelSize` |
| `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs` | Optionally use shared validation function (or keep throw-and-document pattern) |
| `src/InfiniteCanvas.App/MainWindow.xaml` | Add `MinimumSparseTilePixelSize` UI control (slider or numeric input) |
| `tests/InfiniteCanvas.Tests/CanvasUserSettingsTests.cs` | Add upper-bound and round-trip tests, add consumption test |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "SettingsValidation|ObjectsPerTile|MinimumSparseTilePixelSize"
```

Current wave validation also includes the Windows renderer tests, the Release app build, and the task tracker validator.

## Notes

- Wave M is complete and pushed at `79d0cb2`.
- The current branch includes the CI commit `b89aa55`, now pushed to `origin/main`.
- Wave N is complete and pushed (shared validators + sparse-tile gate).

## Validation Result (Wave N, 2026-08-06)

- Core settings tests: 5/5.
- Full core suite: 191/191.
- Windows renderer tests: 13/13.
- Full Windows suite: 25/25.
- App Release build: succeeded (existing `_frameClaimantId` warning only).
- Task tracker validator: clean.
- `git diff --check`: clean.

## Related Tasks

- ICW-099: MinimumSparseTilePixelSize threading (this ticket subsumes it)
- ICW-022: MainWindow decomposition Phase 1 acceptance criteria (this ticket satisfies parts a-c)
- ICW-P1-SETTINGS-SCOPE: Phase 1 compatibility plan
- ICW-043: settings persistence (established the round-trip this ticket hardens)
