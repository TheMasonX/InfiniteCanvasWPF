---
id: ICW-316-canvas-assembly-extraction
author: Copilot
key: ICW-316
title: Extract the canvas component into its own assembly
status: Proposed
type: Story
priority: P3
tags:
  - canvas
  - library-extraction
  - assembly
dependsOn:
  - ICW-315
related:
  - ICW-312
  - ICW-314
  - ADR-0007
links:
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-316-canvas-assembly-extraction

## Summary

Council finding: the physical assembly move for the canvas component (ADR-0007 decision 5) is unticketed. Move `CanvasControl` and `CanvasViewModel` into their own library so another application can reference it.

## Scope

- Create the canvas control library (WPF) and move `CanvasControl`.
- Keep `CanvasViewModel` in a non-WPF net10.0 project so existing tests are not retargeted.
- Decide the contracts location (Core vs a new contracts assembly) before the move.
- Update `CanvasScrollbarWiringTests` path assertions atomically with the move.
- Update the app project reference and the solution file.

## Acceptance Criteria

- The canvas library builds with no references to app, rendering, or spatial projects.
- Another host can reference the library and implement the source interfaces.
- Release build and full test suites pass.
- No behavior change.

## Validation

- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Notes

- Do not start until ICW-315 lands and the frame boundary is stable.
- Add a consumer-host sample or test that references only the library.

## Related Tasks

- ICW-315 (frame boundary migration)
- ICW-312 (data source abstraction)
- ADR-0007 (component boundary)
