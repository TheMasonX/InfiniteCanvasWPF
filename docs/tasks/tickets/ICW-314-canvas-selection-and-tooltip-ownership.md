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
  - ICW-031
related:
  - ADR-0007
  - ICW-101
  - ICW-315
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
created: 2026-08-04
updated: 2026-08-06
---

# ICW-314-canvas-selection-and-tooltip-ownership

## Summary

User requirement: the canvas control must be responsible for object selection and tooltip hovers.

Today `MainWindow.BuildFrameVisual` builds the annotation layer against the concrete `SampleAnnotation` type, wires `OnAnnotationMouseLeftButtonDown`, and attaches `DeferredAnnotationToolTip`. The canvas cannot own selection or tooltips while they are built inside the app's frame.

## Audit Synthesis Scope Note (2026-08-04)

- Item-query authority: ICW-314 consumes `QueryVisible`. The duplicate-authority decision (finding F-001, gated in ICW-316A) must precede hit-testing. Record as a soft dependency.
- `SceneChanged` is declared and raised but has no subscriber; decide event-vs-polling within this ticket's scope (finding C2-031).
- Item identity and instance lifetime across scene revisions are undefined and must be designed before selection migrates (finding C1-026).

## Audit Synthesis Scope Note (2026-08-06)

Verified at HEAD c552830 (Wave H landed). This ticket is the next functional slice on the ADR-0007 path to a reusable web-inspection viewport. State of the code:

- `ICanvasItem` exposes only `Id` + `Bounds` (`src/InfiniteCanvas.Core/ICanvasItem.cs`); its own doc comment says ICW-314 extends it with interaction members. The contract must grow (hit testing, tooltip payload, visual template) before selection/tooltip move honestly.
- `MainWindow` still owns selection state (`_selectedAnnotationId`, `MainWindow.xaml.cs:43`), tooltip creation (`DeferredAnnotationToolTip`, line 793), and selection writeback (line 896). `CanvasViewModel.VisibleItems` (`CanvasViewModel.cs:59`) is the required bridge for the control to hit-test; it is not the destination.
- Assembly extraction (ICW-316) and interaction ownership (this ticket) are orthogonal; extraction landed, this ticket remains the behavioral slice.
- Scope wording should read: "Move selection and tooltip hover into `CanvasControl`, extend `ICanvasItem` with the interaction payloads needed for hit testing and tooltip rendering, and keep `CanvasViewModel` only as the bridge until the control owns the full interaction path."
- Council 2026-08-04: tooltip half waits on ICW-031 (typed metrics).

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
- Council decision (2026-08-04): depends on ICW-031 (typed annotation metrics) for the tooltip payload. The tooltip half of this task waits for ICW-031.
- The item contract starts as `ICanvasItem` (Id + Bounds) in ICW-312 and is extended here with hit-test, tooltip payload, and visual template.
- The view model stores the visible item list as `IReadOnlyList<ICanvasItem>` so the control can hit-test.
- Full council report: docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md.

## Related Tasks

- ICW-312 (data source abstraction)
- ADR-0007 (component boundary)
