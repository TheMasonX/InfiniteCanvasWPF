---
id: ICW-340
author: Copilot
key: ICW-340
title: Publish an atomic material layer plan
status: In Progress
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
  - ICW-343
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md
  - docs/audits/external-material-source-annotation-readiness-audit-26-08-08.md
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-08
---

## Summary

Publish raster content and interactive material layers as one accepted frame plan.
The current control publishes raster and view-model state before the host composes tile-grid and annotation layers, and the active raster input loses complete tile identity.

## Scope

- Define an immutable ordered layer plan with visibility and revision data.
- Carry complete source, tile, layer, content revision, and mip identity in each material payload input.
- Include raster, background material, defect imagery, tile grid, annotations, labels, selection, and pixelometer provenance as applicable.
- Include camera-column identity and explicit left or right precedence for horizontal overlap.
- Validate no vertical overlap within one camera column.
- Include neutral annotation kind, order, style, label policy, and optional presentation data.
- Move frame acceptance and layer publication behind one control boundary.
- Preserve the persistent shell and composition-fenced buffer lifecycle.
- Add rejected-frame and layer-order regression tests.

## Acceptance Criteria

- One captured camera and source snapshot produces the raster and every layer input in the plan.
- The control accepts or rejects the complete plan as one unit.
- A rejected plan updates neither raster nor overlay state.
- Layer order and visibility are deterministic and independently testable.
- The plan carries the semantic identity from ICW-339.
- The plan preserves complete tile identity through raster composition. Equal tile IDs with different source, revision, or mip values remain distinct.
- Existing zero-copy buffer fencing remains valid through the new publication path.
- Regression tests prove that colliding tile IDs do not replace each other's payloads.
- Regression tests prove left and right overlap preferences independently of input order.
- Regression tests reject vertical overlap within one camera column.
- Regression tests publish defect, marker, and region data through one neutral adapter.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasControlConsumerHost|FullyQualifiedName~FrameShell"`.
- Result: `CanvasFrame` carries an ordered layer plan. `CanvasControl` invokes `FrameLayersPublishing` after stale checks and before raster and view-model mutation. The consumer-host suite passes 14/14, including rejected semantic frames and layer-order checks. Layer content rollback after a host callback failure remains pending.

## Notes

The external layer list and dirty-layer rules require product confirmation. Keep the contract source-neutral and avoid application-specific visual classes. The current callback boundary does not provide rollback for partial host visual mutation.

## Latest Audit Findings

- [F-001, full tile identity is lost before raster composition](../../audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md)
- [F-005, scanner overlap has no deterministic policy](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)
- [F-006, external heterogeneous annotations lack an adapter boundary](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)

## Related Tasks

- ICW-337
- ICW-339
- ICW-316
- ICW-328
- ICW-343