---
id: ICW-151-preserve-noise-settings-viewmodel-on-regeneration
author: Copilot
key: ICW-151
title: Preserve noise settings control state across regeneration
status: Proposed
type: Bug
priority: P1
tags:
  - settings
  - mvvm
  - regeneration
  - ui
dependsOn: []
related:
  - ICW-067
  - ICW-P1-SETTINGS-SCOPE
  - ICW-P1-SETTINGS-VALIDATION
  - ICW-043
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.ViewModels/MainViewModel.cs
  - src/InfiniteCanvas.App/Controls/TileBackgroundNoiseSettingsView.xaml
  - tests/InfiniteCanvas.Tests/MainViewModelTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-03
updated: 2026-08-03
---

# ICW-151-preserve-noise-settings-viewmodel-on-regeneration

## Summary

`RegenerateSceneAsync` captures the current noise values for tile generation, then replaces the window data context with a new `MainViewModel`. The generated scene uses the edited values, but the controls reset to the new view model defaults.

This is a partial fix, not a complete lifecycle fix. The current snapshot workaround preserves generation input but does not preserve the bound control state.

## Scope

- Separate spatial-state initialization from `MainViewModel` construction.
- Keep one settings view model for the window lifetime, or explicitly reapply the captured snapshot before publishing the new data context.
- Preserve target value, noise, circle count, scale, octaves, lacunarity, gain, and amplitude across regeneration.
- Preserve loaded settings during automatic startup regeneration.
- Centralize noise defaults and remove the current view-model default drift for octaves.
- Add a regression test that changes every noise field, regenerates, and verifies both the generated options and the visible view-model values.

## Acceptance Criteria

- Editing a noise control followed by Regenerate leaves every control at the edited value.
- Automatic startup regeneration does not replace values loaded from `CanvasUserSettings`.
- The tile generator receives the same snapshot that remains in the bound view model.
- `InitializeSpatialState` does not replace unrelated settings state.
- Tests fail if regeneration creates a fresh default `MainViewModel` without restoring values.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Research complete. Implementation and focused regression tests are pending.

## Notes

- The existing Wave A task marked the generation-input symptom Done. The remaining defect is the user-visible control reset after the snapshot is captured.
- Keep this task separate from ICW-067 because the reusable control work does not by itself fix data-context lifetime.

## Related Tasks

- ICW-067
- ICW-P1-SETTINGS-SCOPE
- ICW-P1-SETTINGS-VALIDATION
- ICW-043
