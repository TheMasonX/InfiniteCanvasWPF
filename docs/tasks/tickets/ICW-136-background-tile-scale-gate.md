---
id: ICW-136
author: Copilot
key: ICW-136
title: Keep background tiles renderable at extreme zoom-out
status: Done
type: Bug
priority: P1
tags:
  - rendering
  - settings
  - zoom
  - background-tiles
dependsOn:
  - ICW-P1-SETTINGS-VALIDATION
related:
  - ICW-074
links:
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
created: 2026-08-06
updated: 2026-08-06
---

# ICW-136, Keep background tiles renderable at extreme zoom-out

## Summary

The recent 96-pixel default suppresses background tile generation at the demo zoom scale.
Background tiles must remain renderable at that scale.

## Scope

- Define the demo threshold as `CanvasUserSettings.DefaultMinimumSparseTilePixelSize`.
- Set the named default to zero.
- Migrate the previous persisted value `CanvasUserSettings.LegacyDefaultMinimumSparseTilePixelSize` to zero on load.
- Keep positive thresholds available for explicit suppression tests and host policies.
- Keep the existing below-threshold renderer test.

## Acceptance criteria

- A new `CanvasUserSettings` instance uses the named zero-value default.
- A persisted previous default of `96` loads as the named zero-value default.
- The XAML control uses the same zero-value default.
- MainWindow uses the same zero-value default.
- A positive threshold still prevents generation for a projected tile below that threshold.
- Background rendering remains enabled at the demo zoom scale.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore`
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-restore`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore`
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- `git diff --check`

## Validation result

- Core tests: 196/196.
- Windows tests: 25/25.
- App Release build: succeeded with the existing `_frameClaimantId` warning.
- Focused settings migration tests: 7/7.
- Editor diagnostics: no errors in the changed code files.
- Task tracker script: blocked by the Windows PowerShell runtime because `System.IO.Path.GetRelativePath` is unavailable.
