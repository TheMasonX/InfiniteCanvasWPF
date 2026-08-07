---
id: ICW-334
author: Copilot
key: ICW-334
title: Move point selection into CanvasControl through the item contract
status: In Progress
type: Story
priority: P1
tags:
  - canvas
  - selection
  - library-extraction
  - viewport
  - mvp
dependsOn:
  - ICW-312
related:
  - ICW-314
  - ADR-0007
links:
  - src/InfiniteCanvas.Core/ICanvasItem.cs
  - src/InfiniteCanvas.Core/ICanvasSceneSource.cs
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs
created: 2026-08-07
updated: 2026-08-07
---

# ICW-334-canvas-selection-contract-and-control-ownership

## Summary

The reusable canvas must own point selection without referencing application annotation types.
The current host owns selection state and visual click handlers.

## Scope

- Add a host-neutral point hit-test member to `ICanvasItem`.
- Make `CanvasControl` query the injected scene source on an un-dragged left click.
- Expose the selected item and a selection-change event from the control.
- Forward selection changes from `MainWindow` to the existing inspection state.
- Keep tooltip ownership deferred until typed annotation metrics land under ICW-031.

## Acceptance Criteria

- A consumer host can select an item by clicking its world position.
- Dragging the viewport does not change selection.
- Clicking empty space clears selection.
- Selection events expose only `ICanvasItem` and nullable selection state.
- The control and its tests do not reference `SampleAnnotation`.
- Tooltip ownership remains explicitly tracked as the next dependent slice.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter CanvasControlConsumerHostTests`
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore`
- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

## Notes

The existing `ICanvasSceneSource.QueryPoint` is the single point-query authority.
The control uses the current camera snapshot to convert viewport coordinates to world coordinates.
The control selects the first returned item that accepts the point through `HitTest`.

ICW-314 remains open for tooltip payload and tooltip lifecycle ownership.
Those concerns require the typed metrics decision tracked by ICW-031.

## Related Tasks

- ICW-314, selection and tooltip ownership epic.
- ICW-031, typed annotation metrics.
- ADR-0007, reusable canvas boundary.
