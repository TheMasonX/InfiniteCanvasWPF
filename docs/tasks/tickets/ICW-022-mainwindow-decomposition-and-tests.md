---
id: ICW-022-mainwindow-decomposition-and-tests
author: External Audit (Integration-1)
key: ICW-022
title: Extract testable logic from MainWindow code-behind and backfill unit tests
status: In Progress
type: Task
priority: P2
tags:
  - refactoring
  - mainwindow
  - testing
  - decomposition
dependsOn:
  - ICW-101
  - ICW-031
related:
  - ICW-080
  - ICW-037
  - ICW-P1-SETTINGS-SCOPE
  - ICW-P1-SETTINGS-VALIDATION
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.ViewModels
  - src/InfiniteCanvas.Core
  - tests/InfiniteCanvas.Tests
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-25
updated: 2026-08-04
---

# ICW-022 — Extract testable logic from MainWindow code-behind and backfill unit tests

## Summary

**Audit finding:** MainWindow combines viewport composition, render presentation, settings editing, and lifecycle logic in one shell; the XAML repeats visual patterns that would benefit from styles and subcontrols. The file is 1600+ lines. Multiple audit items depend on this decomposition.

## Scope

### Current slice — temporary Canvas control duplication

1. Add `Controls/CanvasControl.xaml` and `Controls/CanvasControl.xaml.cs` with a duplicated viewport surface, overlays, scrollbar chrome, pixelometer, and pointer interaction hooks.
2. Add `CanvasViewModel` for camera and viewport state owned by the new control.
3. Keep MainWindow unchanged. Do not replace the live viewport until the duplicate builds and receives focused tests.

### Phase 1 — Compatibility & Settings Hardening (acceptance criteria from external audit)

These items must be completed before structural decomposition begins:

1. **(a) Settings not silently reset on `RegenerateSceneAsync`** — Add regression test asserting `MainViewModel`/noise settings survive scene regeneration. **Status: DONE** (Sprint 1 Wave A — background noise settings snapshot). Add cross-reference in tracker.

2. **(b) `CanvasUserSettings.IsValid` checks all enforced bounds** — Audit every field, fix any gaps. Covered by ICW-P1-SETTINGS-VALIDATION.

3. **(c) Every persisted setting is verifiably consumed** in the render/generation call graph — Add consumption test that would have caught `MinimumSparseTilePixelSize` not reaching `GenerateFrozenBitmap`. Covered by ICW-P1-SETTINGS-VALIDATION.

### Phase 2 — Structural Decomposition

1. **Extract subcontrols:**
   - Viewport host (viewport `Canvas` + scrollbar overlays + grid overlay)
   - Settings sidebar (display panel, debug panel, noise settings, etc.)
   - Feature inspector (selected-annotation DataGrid)
   - Footer/status area (status text, loading indicator, pixelometer readout)

2. **Move pure logic to presenter/controller classes:**
   - Zoom math → `ViewportZoomCalculator` (see ICW-052) **Status: DONE** — verify extraction is complete.
   - Pixelometer view-state → `PixelometerController`
   - Generation input validation → `GenerationOptionsValidator` (see ICW-052) **Status: DONE** — verify.
   - Selection/tooltip formatting → `AnnotationFeaturePresenter` (see ICW-101) **Status: PARTIAL** — tooltip still uses raw indexers.

3. **Consolidate repeated XAML patterns** into shared styles/templates:
   - Section headers, button groups, panel spacing, slider labels.

### Acceptance Criteria

- MainWindow becomes a thin shell composing focused subcontrols.
- The duplicate Canvas control builds independently while MainWindow still owns the live viewport.
- Canvas camera and viewport state lives on its dedicated view model.
- Viewport and settings interactions backed by presenter/controller classes rather than code-behind.
- Repeated XAML patterns use shared styles/templates.
- Pure logic is unit-testable without instantiating the full window.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml` | Extract subcontrols into separate files, consolidate styles |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Delegate to presenter/controller classes |
| `src/InfiniteCanvas.App/Controls/CanvasControl.xaml` (new) | Temporary duplicated viewport composition control |
| `src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs` (new) | Canvas input and overlay interaction boundary |
| `src/InfiniteCanvas.ViewModels/CanvasViewModel.cs` (new) | Canvas camera and viewport state |
| `src/InfiniteCanvas.App/Controls/SettingsSidebarControl.xaml` (new) | Settings panel subcontrol |
| `tests/InfiniteCanvas.Tests` | Add tests for extracted logic |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release
dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
```

Current slice evidence:

- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release` passes. The existing `_frameClaimantId` warning remains.
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~CanvasViewModelTests` passes 2 tests.
- A normal test restore is blocked by the pre-existing Rendering project target mismatch. The focused test passes with existing restore assets.

## Related Tasks

- ICW-080: annotation feature presentation model
- ICW-037: accessibility baseline (depends on subcontrol extraction)
- ICW-P1-SETTINGS-SCOPE: Phase 1 acceptance criteria
- ICW-P1-SETTINGS-VALIDATION: settings validation (Phase 1 prerequisite)
- ICW-101: tooltip presenter restore (Phase 1 prerequisite)
