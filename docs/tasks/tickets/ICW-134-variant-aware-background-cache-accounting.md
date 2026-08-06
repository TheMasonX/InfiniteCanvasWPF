---
id: ICW-134
author: Copilot
key: ICW-134
title: Add variant-aware background cache accounting and reuse
status: Done
type: Improvement
priority: P1
tags:
  - rendering
  - cache
  - mipmaps
  - performance
dependsOn: []
related:
  - ICW-064
  - ICW-076
  - ICW-096
  - ICW-131
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
created: 2026-07-26
updated: 2026-08-06
---

## Summary

Prevent avoidable native noise regeneration across mip transitions and make cache cost match the payloads actually resident. The current tile cache reuses native tile payloads, but its reservation is keyed by tile ID and charges `PixelCost`, while a tile can hold multiple mip byte arrays with different dimensions.

## Scope

- Define cache identity and ownership for source/tile/content-revision/mip variants.
- Reserve and release actual Gray8 payload bytes per resident variant, including failed generation and reset paths.
- Preserve the best resident mip during asynchronous replacement and avoid duplicate requests for an already resident or queued variant.
- Expose hit, miss, queued, generated, rejected, evicted, and resident-byte counts by mip level.

## Acceptance Criteria

- Repeated requests for the same tile revision and mip do not invoke the native generator again while the payload remains resident.
- Cache accounting equals the sum of resident payload byte lengths and remains correct after eviction, reset, failure, and regeneration.
- A requested mip transition continues rendering the nearest valid resident payload until replacement completes.
- Tests cover concurrent duplicate requests, reset during generation, variant eviction, and source/revision invalidation.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Run the mip-transition and cache benchmark matrix from ICW-133.

## Completion Evidence

Reservations use complete `BackgroundTileCacheKey` identity and dispose through `ICacheReservation`. Resident byte accounting covers native and mip payloads. Tile and complete-key pinning coexist. Regression tests cover multi-mip accounting, duplicate disposal, and diagnostics snapshots.

## Notes

A generic output cache was suggested by the profiler report, but `SampleImageTile` already memoizes its native payload and mip payloads. The missing performance contract is variant-aware accounting and diagnostics, not an unbounded second cache of float noise grids.

## Related Tasks

- ICW-064: byte-budgeted tile-cache admission and diagnostics
- ICW-076: source-agnostic background tile mip levels
- ICW-096: resident imagery during mip transitions
- ICW-131: FastNoise and Gray8 performance review
