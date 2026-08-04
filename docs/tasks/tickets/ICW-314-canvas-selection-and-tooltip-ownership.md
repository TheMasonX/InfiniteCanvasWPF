---
id: ICW-314-canvas-selection-and-tooltip-ownership
author: Copilot
key: ICW-314
title: Move selection and tooltip hover into the canvas control
status: Proposed
type: Story
priority: P2
tags:
  - canvas
  - selection
  - tooltip
  - library-extraction
dependsOn:
  - ICW-312
related:
  - ADR-0007
  - ICW-031
  - ICW-101
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-314-canvas-selection-and-tooltip-ownership

## Summary

User requirement: the canvas control must be responsible for object selection and tooltip hovers.

Today `MainWindow.BuildFrameVisual` builds the annotation layer against the concrete `SampleAnnotation` type, wires `OnAnnotationMouseLeftButtonDown`, and attaches `DeferredAnnotationToolTip`. The canvas cannot own selection or tooltips while they are built inside the app's frame.

## Scope

- Define an item contract such as `ICanvasItem` with world bounds, hit testing, tooltip payload, and a visual template.
- Move selection state and hit testing into the canvas control.
- Move tooltip display into the canvas control.
- Expose selection changes to the host through an event or an observable property.

## Acceptance Criteria

- Selection and tooltip logic do not reference `SampleAnnotation`.
- The host receives selection notifications and supplies item visuals.
- The app builds and the full core test suite passes.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

## Notes

- Depends on the data source abstraction (ICW-312), which defines how the canvas receives items.
- Coordinate with the council on the item contract.

## Related Tasks

- ICW-312 (data source abstraction)
- ADR-0007 (component boundary)
