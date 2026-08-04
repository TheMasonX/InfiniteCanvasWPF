---
id: ICW-313-input-handler-abstraction
author: Copilot
key: ICW-313
title: Abstract canvas input handlers into IInputHandler classes
status: Proposed
type: Story
priority: P3
tags:
  - canvas
  - input
  - architecture
  - library-extraction
dependsOn:
  - ICW-312
related:
  - ADR-0007
links:
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-313-input-handler-abstraction

## Summary

User requirement (future task): the input handlers for panning, zooming, and related interactions must be abstracted into a set of `IInputHandler` classes.

Today `CanvasControl` implements pan, zoom, anchor pan, and scrollbar logic directly in its code-behind. This is a single component's responsibility, but the user asked for the handlers to become `IInputHandler` implementations.

## Scope

- Define `IInputHandler` with attach and detach lifecycle over the viewport input.
- Extract drag-pan, anchor-pan, scrollbar-pan, and wheel-zoom handlers.
- Register handlers on `CanvasControl` in a deterministic order.
- Keep `ViewportScrollbarPolicy`, `ViewportZoomPolicy`, and `ViewportScrollbarAxis` as the shared policy layer.

## Acceptance Criteria

- Each input behavior is a separate `IInputHandler` class.
- The control behavior is unchanged.
- Handlers are unit-testable without WPF input simulation.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

## Notes

- Explicitly deferred by the user as future work. Do not start until the data source abstraction (ICW-312) lands.

## Related Tasks

- ICW-312 (data source abstraction)
- ADR-0007 (component boundary)
