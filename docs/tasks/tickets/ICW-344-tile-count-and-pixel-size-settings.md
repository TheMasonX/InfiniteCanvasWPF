---
id: ICW-344-tile-count-and-pixel-size-settings
key: ICW-344
title: Configure generated tile count and pixel size
status: Done
type: Story
priority: P1
tags:
  - icw
  - tile-generation
  - settings
  - rendering
dependsOn: []
related:
  - ICW-030
  - ICW-049
links:
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - tests/InfiniteCanvas.Tests/CanvasUserSettingsTests.cs
  - tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-08
updated: 2026-08-08
---

# ICW-344 Configure Generated Tile Count And Pixel Size

## Summary

- Raise the maximum generated tile count from 2,000 to 24,288.
- Make tile pixel width and height configurable through persisted scene settings.
- Keep the default tile size at 8,192 by 4,096 pixels.

## Scope

- Add one canonical tile-count validation contract.
- Add canonical tile-size defaults and validation bounds.
- Add width and height controls to the scene material panel.
- Pass the configured dimensions to `SampleImageGenerator.GenerateSet`.
- Preserve settings through load, save, and scene regeneration.

## Acceptance Criteria

- The maximum valid generated tile count is exactly 24,288.
- A tile count of 24,289 is rejected by settings and runtime validation.
- New settings default to 8,192 by 4,096 pixels.
- Custom positive tile dimensions persist and reach generated `SampleImageTile` instances.
- Existing generation and cache behavior remains unchanged for default settings.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore`
- Result: Core 214/214, Windows 38/38, App Release build 0 warnings and 0 errors. Tracker validation passed with 234 task files. Whitespace validation passed.

## Findings

- `CanvasUserSettings.IsValid` and `MainWindow.TryReadGenerationOptions` currently reject tile grids above 2,000.
- `SampleImageGenerator` already accepts `pixelWidth` and `pixelHeight` with 8,192 by 4,096 defaults, but the app does not expose or persist them.
- The implementation now uses `CanvasUserSettings.MaxGeneratedTiles` and `ValidateTileCount` for the 24,288 ceiling.
- `TilePixelWidth` and `TilePixelHeight` persist in `CanvasUserSettings` and reach `SampleImageGenerator.GenerateSet`.

## Next Step

- Keep the count and dimension bounds aligned when future scene controls or generator entry points change.
