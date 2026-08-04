---
id: ICW-312-canvas-data-source-abstraction
author: Copilot
key: ICW-312
title: Abstract canvas data sources behind injected services
status: Proposed
type: Story
priority: P2
tags:
  - canvas
  - architecture
  - dependency-injection
  - library-extraction
dependsOn:
  - ICW-311
related:
  - ICW-313
  - ICW-314
  - ADR-0007
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-312-canvas-data-source-abstraction

## Summary

User requirement: image generation and other data must be services or injected abstractions so the canvas can move to a separate library and another app can supply its own data sources.

Today `MainWindow` constructs the spatial index, generates tiles, renders frames, and pushes the frame into `CanvasControl`. The control cannot run without the app's concrete pipeline.

## Scope

- Define `ICanvasSceneSource` (scene bounds + items) and a spatial query source.
- Reuse `IBackgroundTileSource` (ADR-0005) as the tile material boundary.
- Inject the sources into `CanvasControl` or `CanvasViewModel`.
- Make `MainWindow` an implementation of the sources over the existing pipeline.
- Remove the direct frame-publish coupling once the sources replace it.

## Acceptance Criteria

- `CanvasControl` and `CanvasViewModel` reference no application data types.
- A consumer can host the canvas and provide its own scene, tile, and spatial sources.
- The app builds and the full core test suite passes after each step.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

## Notes

- Sequence the change so the app keeps working at every step (strangler-fig).
- Do not start until ICW-311 is committed.
- Coordinate with the council before defining the item contract.

## Related Tasks

- ADR-0007 (component boundary)
- ICW-313 (input handler abstraction)
- ICW-314 (selection and tooltip ownership)
