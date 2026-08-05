---
id: ICW-316-canvas-assembly-extraction
author: Copilot
key: ICW-316
title: Extract the canvas component into its own assembly (physical move)
status: Proposed
type: Story
priority: P3
tags:
  - canvas
  - library-extraction
  - assembly
dependsOn:
  - ICW-315
  - ICW-316A
  - ICW-319
related:
  - ICW-312
  - ICW-314
  - ADR-0007
links:
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.App/Controls/CanvasFrame.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-316-canvas-assembly-extraction

## Summary

Council finding: the physical assembly move for the canvas component (ADR-0007 decision 5) is unticketed. Move `CanvasControl`, `CanvasViewModel`, and `CanvasFrame` into their own library so another application can reference it.

Audit synthesis (2026-08-04) rescoped this ticket to the physical-move phase. The hardening phase is a separate ticket (ICW-316A). The move must not publish the duplicate query authority, the mutable frame, the un-validated view-model state, or the raw-element surface as library API.

## Scope

- Create the canvas control library (WPF) and move `CanvasControl`, `CanvasViewModel`, and `CanvasFrame`.
- Keep `CanvasViewModel` in a non-WPF net10.0 project so existing tests are not retargeted.
- Decide the contracts location (Core vs a new contracts assembly) before the move; default is Core per ADR-0007 refinement 1.
- Update `CanvasScrollbarWiringTests` and `FrameShellWiringTests` path assertions atomically with the move.
- Update the app project reference and the solution file.
- The library must expose no raw WPF element surface (see ICW-319).

## Acceptance Criteria

- The canvas library builds with no references to app, rendering, or spatial projects.
- `CanvasFrame` and its dependencies move with `CanvasControl` and `CanvasViewModel`; no App dependency remains.
- Another host can reference the library and implement the source interfaces; a consumer-host test references only the library.
- Release build and full test suites pass.
- No behavior change.

## Validation

- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Command: consumer-host reference test against the library only

## Notes

- Do not start until ICW-316A (harden contracts) and ICW-319 (method-based boundary API) land.
- Add a consumer-host sample or test that references only the library.

## Related Tasks

- ICW-315 (frame boundary migration)
- ICW-316A (harden canvas contracts before extraction)
- ICW-319 (method-based CanvasControl boundary API)
- ICW-312 (data source abstraction)
- ADR-0007 (component boundary)
