---
id: ICW-070-readonly-viewport-scroll-indicator
author: Copilot
key: ICW-070
title: Add a read-only camera scroll-position indicator
status: Proposed
type: Improvement
priority: P1
tags:
  - ui
  - viewport
  - camera
  - navigation
dependsOn: []
related:
  - ICW-065
links:
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
created: 2026-07-25
updated: 2026-07-25
---

## Summary

Show the current camera location within scene bounds as a non-interactive scrollbar-style indicator without allowing its extent or layout to alter render viewport sizing.

## Scope

- Draw a camera-native horizontal and/or vertical position indicator over the canvas.
- Derive thumb position and extent from the captured camera and scene bounds.
- Keep the indicator read-only for the first slice.

## Acceptance Criteria

- The indicator visibly communicates location while panning and zooming.
- It never participates in WPF layout measurements used for render size, zoom floor, or camera clamping.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- The previous native `ScrollViewer` integration regressed zoom-out by substituting scaled scene content dimensions for visible viewport dimensions.

## Related Tasks

- ICW-065