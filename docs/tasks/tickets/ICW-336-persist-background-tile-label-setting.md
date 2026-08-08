---
id: ICW-336
author: Copilot
key: ICW-336
title: Persist background tile label setting
status: Done
type: Story
priority: P2
tags:
  - settings
  - background-tiles
  - persistence
  - ui
dependsOn: []
related:
  - ICW-130
  - ICW-043
links:
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - src/InfiniteCanvas.App/Controls/TileBackgroundSettingsView.xaml
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Persist a background tile label setting and expose it as a checkbox in the tile background settings.
The setting controls generation of the `Tile N` Gray8 label in every requested mip.

## Scope

- Add a Boolean property to `CanvasUserSettings`.
- Add the property to the background tile settings view model and JSON save and load flow.
- Add a checkbox to `TileBackgroundSettingsView`.
- Pass the value through `GeneratorOptions` and `SampleImageGenerator`.
- Apply the value during the next scene regeneration.
- Add persistence, wiring, and raster output tests.

## Acceptance Criteria

- The setting defaults to enabled.
- JSON save and load preserve both enabled and disabled values.
- The checkbox appears inside the tile background settings expander.
- Disabling the setting omits the tile label from mip 0 and lower-resolution mips.
- Enabling the setting preserves the existing tile label output.
- Tile identity and other background detail generation remain unchanged.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CanvasUserSettingsTests|FullyQualifiedName~SampleImageGeneratorTests|FullyQualifiedName~LayerVisibilityWiringTests"`, passed 35/35.
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~LayerVisibilityWiringTests"`, passed 5/5 after the rename.
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LayerVisibilityWiringTests"`, passed 1/1 after confirming deferred regeneration.
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore`, 196/197 passed. The existing `AnnotationTooltipWiringTests` failure belongs to ICW-314.
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore`, passed with the existing unused `_frameClaimantId` warning.
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`, passed.
- `git diff --check`, pending final validation.

## Notes

The setting changes generated tile payloads, so the checkbox affects the next scene regeneration.
The checkbox does not start regeneration or rendering work by itself.
The existing default remains enabled for compatibility with ICW-130.
The App build blocker belongs to the active ICW-314 tooltip ownership work.
The cleanup renames the code surface to `TileBackgroundSettings*` and the UI section to `BACKGROUND TILE SETTINGS`.

## Related Tasks

- ICW-130 defines the stable background tile label payload.
- ICW-043 defines display and generation settings persistence.