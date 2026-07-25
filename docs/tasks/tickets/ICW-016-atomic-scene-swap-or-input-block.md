---
id: ICW-016-atomic-scene-swap-or-input-block
key: ICW-016
title: Prevent torn scene frames during regeneration
status: Proposed
type: Task
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

Summary:
Make `RegenerateSceneAsync` scene updates atomic or block input during regeneration to avoid torn frames where tiles, annotations, or spatial index are inconsistently observed by the render path.

Scope:
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
- possible new `SceneState` record type under `src/InfiniteCanvas.App`

Acceptance criteria:
- Render path reads a single atomic `SceneState` instance representing tiles/annotations/spatial index.
- Manual scenario with slow regeneration demonstrates no torn frames when panning/zooming.

Validation commands:
- `dotnet build ./InfiniteCanvasWPF.slnx --configuration Release`

Estimated effort: Small-Medium
Risk: Low
Suggested owner: @app-team
