---
id: ICW-338
author: Copilot
key: ICW-338
title: Enforce immutable frame and payload ownership
status: In Progress
type: Bug
priority: P1
tags:
  - contracts
  - immutability
  - rendering
  - cache
dependsOn: []
related:
  - ICW-337
  - ICW-076
  - ICW-316A
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs
created: 2026-08-07
updated: 2026-08-08
---

## Summary

Make published frame raster, item state, and resident tile payloads immutable from the caller's perspective.
The current working tree owns the item sequence, exposes read-only payload bytes, and rejects unfrozen raster input. It does not enforce element-level item stability or concurrent-read evidence.

## Scope

- `CanvasFrame.Items` construction and storage.
- `CanvasFrame.Raster` frozen ownership and surface lifetime.
- Item identity and bounds stability after frame publication.
- `BackgroundTilePayload.Pixels` construction and exposure.
- Materializer resident cache publication and lookup.
- Core regression tests for mutation and concurrent reads.

## Acceptance Criteria

- `CanvasFrame` owns a stable item sequence that cannot change when the constructor input changes.
- `CanvasFrame` rejects or owns non-frozen raster input before publication.
- Accepted item identity and bounds cannot change through a host-owned mutable item after publication.
- `BackgroundTilePayload` exposes read-only bytes or owns a defensive copy.
- Resident cache lookups cannot mutate cached payload data through the returned API.
- Tests prove source-list mutation and source-array mutation do not alter accepted state.
- Tests cover concurrent publication and resident reads without data races.
- Public APIs remain source-neutral and preserve zero-copy behavior where ownership permits it.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasFrame|FullyQualifiedName~BackgroundTileMaterializer"`.
- Result: The item sequence is copied, payload input bytes are defensively copied, payload bytes are exposed through a read-only collection, and unfrozen rasters are rejected. Focused materializer tests pass 6/6 and consumer-host tests pass 14/14. Element-level item stability, concurrent reads, and full validation remain pending.

## Notes

This task narrows the immutability claim from ICW-316A and the payload contract from ICW-076. The Wave AF review confirms list, byte-array, and raster ownership changes. It does not change the zero-copy surface lease policy.

## Latest Audit Findings

- [F-002, frame raster and item ownership remain unenforced](../../audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md)

## Related Tasks

- ICW-337
- ICW-076
- ICW-316A