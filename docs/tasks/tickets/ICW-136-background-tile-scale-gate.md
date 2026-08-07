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
- Keep positive thresholds available for explicit suppression tests and host policies.
- Keep the existing below-threshold renderer test.

## Acceptance criteria

- A new `CanvasUserSettings` instance uses the named zero-value default.
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

- Focused settings tests: 6/6.
- Focused Windows renderer tests: 13/13.
- Editor diagnostics: no errors in the changed code files.
