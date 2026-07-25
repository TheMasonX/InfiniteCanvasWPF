---
id: ICW-082
key: ICW-082
title: Persist the background-image visibility toggle
status: Proposed
type: Bug
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

## Summary

The `ShowBackgroundImagesCheckBox` control is a runtime setting but is not represented in the persisted settings record or `SaveSettings` object. A user who changes this toggle loses the choice on the next launch.

## Scope

- Add a persisted `ShowBackgroundImages` setting with the existing default of `true`.
- Apply the setting in `MainWindow.ApplySettingsToUi`.
- Save the checkbox value in `MainWindow.SaveSettings`.
- Add settings round-trip coverage and preserve malformed/legacy-file fallback behavior.
- Keep the independent layer-visibility invariant aligned with ICW-073.

## Acceptance Criteria

- Toggling background-image visibility, saving, and loading preserves the value.
- Existing settings files without the new property load with `ShowBackgroundImages == true`.
- The UI uses the loaded value on startup and writes the current value on close.
- Focused settings tests and the Release app build pass.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter CanvasUserSettingsTests`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Current evidence: `CanvasUserSettings` has no `ShowBackgroundImages` property, `ApplySettingsToUi` does not set `ShowBackgroundImagesCheckBox` from persisted state, and `SaveSettings` omits the checkbox value. The visible control is therefore reset to its default on relaunch.

## Notes

The current UI has both background-image and sparse-image visibility controls. Do not conflate this task with sparse-image persistence, which is already present in the settings model.

## Related Tasks

- ICW-043: versioned settings persistence.
- ICW-073: independent show-settings surface, returned to In Review until this gap is resolved.
