---
id: ICW-340
author: Copilot
key: ICW-340
title: Publish an atomic material layer plan
status: Proposed
type: Story
priority: P0
tags:
  - external-viewport
  - layers
  - frame-publication
  - rendering
dependsOn:
  - ICW-339
related:
  - ICW-337
  - ICW-316
  - ICW-328
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Publish raster content and interactive material layers as one accepted frame plan.
The current control publishes raster and view-model state before the host composes tile-grid and annotation layers.

## Scope

- Define an immutable ordered layer plan with visibility and revision data.
- Include raster, background material, defect imagery, tile grid, annotations, labels, selection, and pixelometer provenance as applicable.
- Move frame acceptance and layer publication behind one control boundary.
- Preserve the persistent shell and composition-fenced buffer lifecycle.
- Add rejected-frame and layer-order regression tests.

## Acceptance Criteria

- One captured camera and source snapshot produces the raster and every layer input in the plan.
- The control accepts or rejects the complete plan as one unit.
- A rejected plan updates neither raster nor overlay state.
- Layer order and visibility are deterministic and independently testable.
- The plan carries the semantic identity from ICW-339.
- Existing zero-copy buffer fencing remains valid through the new publication path.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasControlConsumerHost|FullyQualifiedName~FrameShell"`.
- Result: Pending implementation. Current source review confirms separate `PublishFrame` and `FramePublished` composition steps.

## Notes

The external layer list and dirty-layer rules require product confirmation before implementation. Keep the first contract source-neutral and avoid application-specific visual classes.

## Related Tasks

- ICW-337
- ICW-339
- ICW-316
- ICW-328