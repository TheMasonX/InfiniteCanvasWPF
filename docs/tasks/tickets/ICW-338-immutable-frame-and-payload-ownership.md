---
id: ICW-338
author: Copilot
key: ICW-338
title: Enforce immutable frame and payload ownership
status: Proposed
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
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Make published frame item collections and resident tile payloads immutable from the caller's perspective.
The current read-only interfaces do not own the underlying list or byte array.

## Scope

- `CanvasFrame.Items` construction and storage.
- `BackgroundTilePayload.Pixels` construction and exposure.
- Materializer resident cache publication and lookup.
- Core regression tests for mutation and concurrent reads.

## Acceptance Criteria

- `CanvasFrame` owns a stable item sequence that cannot change when the constructor input changes.
- `BackgroundTilePayload` exposes read-only bytes or owns a defensive copy.
- Resident cache lookups cannot mutate cached payload data through the returned API.
- Tests prove source-list mutation and source-array mutation do not alter accepted state.
- Tests cover concurrent publication and resident reads without data races.
- Public APIs remain source-neutral and preserve zero-copy behavior where ownership permits it.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasFrame|FullyQualifiedName~BackgroundTileMaterializer"`.
- Result: Pending implementation. Current source review confirms the ownership gap.

## Notes

This task narrows the immutability claim from ICW-316A and the payload contract from ICW-076. It does not change the zero-copy surface lease policy.

## Related Tasks

- ICW-337
- ICW-076
- ICW-316A