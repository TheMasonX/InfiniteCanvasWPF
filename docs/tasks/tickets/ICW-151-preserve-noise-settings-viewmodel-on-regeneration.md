---
id: ICW-151-preserve-noise-settings-viewmodel-on-regeneration
author: Copilot
key: ICW-151
title: Preserve noise settings control state across regeneration
status: Done
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
  - src/InfiniteCanvas.App/Controls/TileBackgroundSettingsView.xaml
  - tests/InfiniteCanvas.Tests/MainViewModelTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-03
updated: 2026-08-03
---

# ICW-151-preserve-noise-settings-viewmodel-on-regeneration

## Summary

`MainWindow` now keeps one `MainViewModel` for the window lifetime, so regeneration no longer resets bound noise controls to defaults.

`RegenerateSceneAsync` now reads generator input from the same bound settings snapshot that remains visible in the UI.

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

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~MainViewModelTests"`
- Result: Passed. 4 passed, 0 failed.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Result: Passed. 132 passed, 0 failed.
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Passed. Build succeeded. Existing warning remains: `CS0169` on unused `_frameClaimantId`.

## Notes

- `InitializeSpatialState` now initializes spatial services only. It does not replace `DataContext`.
- `MainViewModel` now supports explicit `ApplyBackgroundNoiseSnapshot` restore and uses `CanvasUserSettings` as the default source for all noise settings.
- `SampleImageGenerator` noise defaults now align with settings defaults for octaves.

## Related Tasks

- ICW-067
- ICW-P1-SETTINGS-SCOPE
- ICW-P1-SETTINGS-VALIDATION
- ICW-043
