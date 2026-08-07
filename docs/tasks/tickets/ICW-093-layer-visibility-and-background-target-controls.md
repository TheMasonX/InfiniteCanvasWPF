---
id: ICW-093
author: InfiniteCanvas Agent
key: ICW-093
title: Fix independent layer visibility and expose background target controls
status: Done
type: Bug
priority: P1
tags:
  - rendering
  - visibility
  - settings
  - ui
dependsOn: []
related:
  - ICW-073
  - ICW-082
  - ICW-P1-SETTINGS-VALIDATION
links:
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - tests/InfiniteCanvas.Tests/LayerVisibilityWiringTests.cs
created: 2026-08-06
updated: 2026-08-06
---

# ICW-093 - Fix independent layer visibility and expose background target controls

## Summary

The settings model stores independent layer visibility values, but the UI and renderer do not consume every value.
The sparse-image flag currently follows the general image flag. The box flag has no render control.

## Scope

1. Add separate controls for sparse image tiles and annotation boxes.
2. Load and save both settings through the existing settings path.
3. Use the sparse-image setting for the sparse renderer argument.
4. Skip annotation rectangles when the box setting is disabled, while preserving labels and selection behavior.
5. Preserve separate raster visibility for background and sparse image layers.

## Acceptance Criteria

- `ShowImageTiles` and `ShowSparseImageTiles` can differ after load and at runtime.
- `GenerateFrozenBitmap` receives the sparse-image setting, not the general image setting.
- `ShowBoxes = false` removes annotation rectangles without hiding labels.
- All four layer settings persist through `CanvasUserSettingsStore`.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LayerVisibilityWiringTests|FullyQualifiedName~CanvasUserSettingsTests"`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore`
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

## Findings

Wave N review found that `ShowSparseImageTiles` and `ShowBoxes` were persisted but not independently consumed.

## Next Step

Run the full core and Windows suites, validate the tracker, and record the result in the Wave O handoff.

## Validation Result

- Focused visibility and settings tests: 6/6.
- App Release build: succeeded with the existing `_frameClaimantId` warning only.
