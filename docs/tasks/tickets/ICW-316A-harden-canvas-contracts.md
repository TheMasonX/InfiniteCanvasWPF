---
id: ICW-316A-harden-canvas-contracts
author: InfiniteCanvas Agent
key: ICW-316A
title: Harden reusable canvas contracts and lifecycle before extraction
status: Proposed
type: Story
priority: P1
tags:
  - canvas
  - boundary
  - library-extraction
  - hardening
dependsOn:
  - ICW-312
  - ICW-315
related:
  - ICW-316
  - ICW-319
  - ICW-314
  - ADR-0007
links:
  - src/InfiniteCanvas.App/Controls/CanvasFrame.cs
  - src/InfiniteCanvas.ViewModels/CanvasViewModel.cs
  - src/InfiniteCanvas.Core/ICanvasSceneSource.cs
  - src/InfiniteCanvas.Core/ICanvasSpatialQuerySource.cs
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-316A — Harden reusable canvas contracts and lifecycle in place

## Summary

Audit synthesis finding F-001..F-004 and F-013. Before the physical assembly move (ICW-316), the canvas boundary must be hardened in place. A mechanical move would publish the duplicate query authority, the mutable frame, the un-validated view-model state, and the raw-element surface as stable library API.

## Scope

- Resolve the duplicate item-query authority: `QueryVisible` exists on both `ICanvasSceneSource` and `ICanvasSpatialQuerySource`. The control must consume exactly one item-query contract. Update `CanvasBoundaryZeroReferenceTests` atomically.
- Harden `CanvasFrame`: immutable by contract, count-consistency validation, raster-dimension validation against `ImageSource` metadata, revision identity.
- Harden `CanvasViewModel`: no public setter permits `VisibleItemCount > TotalItemCount`; `ApplyFrame` requires a non-null visible-items list; frame state publishes as one notification batch; `HasScene` cannot be bypassed.
- Add `Loaded`/`Unloaded` lifecycle handling on `CanvasControl` to stop the anchor-pan timer, release capture, and clear `Mouse.OverrideCursor`.
- Replace the magic `0.01 x 0.01` probe and `_tiles[0]`/`_tileColumns` layout assumptions in the host pixel read with a named point-query contract.

## Acceptance Criteria

- One item-query authority per published frame.
- `CanvasFrame` construction validates item counts and raster dimensions.
- `CanvasViewModel` state invariants hold by construction; `CanvasSceneSourceContractsTests` optional-items fallback test is updated.
- `CanvasControl` releases timer, capture, and cursor on unload.
- Host pixel read uses a named point-query contract, not a magic probe.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "CanvasBoundaryZeroReferenceTests|CanvasSceneSourceContractsTests|CanvasFrame|CanvasViewModel"`
- Command: source scan asserting the control consumes exactly one source contract after consolidation
- Command: `dotnet build src/InfiniteCanvas.App --configuration Release`

## Notes

- Gate order: item-query authority first, then frame, then view-model state.
- Must land before ICW-316B (physical move) and before ICW-314 consumes `QueryVisible`.

## Related Tasks

- ICW-312 (data source abstraction, Done)
- ICW-315 (frame boundary migration, Done)
- ICW-316 (physical move, rescoped)
- ICW-319 (method-based CanvasControl API)
- ICW-323 (epoch-wiring test, batches here)
