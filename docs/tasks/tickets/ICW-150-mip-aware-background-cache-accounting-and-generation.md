---
id: ICW-150-mip-aware-background-cache-accounting-and-generation
author: Copilot
key: ICW-150
title: Mip-aware background generation and cache accounting
status: In Progress
type: Bug
priority: P0
tags:
  - rendering
  - mip
  - cache
  - memory
  - diagnostics
dependsOn:
  - ICW-P0-LEASE-RELEASE
  - ICW-P1-PIXELCOST-MIPS
related:
  - ICW-076
  - ICW-096
  - ICW-134
  - ICW-064
  - ADR-0005
  - ADR-0006
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-03
updated: 2026-08-03
---

# ICW-150-mip-aware-background-cache-accounting-and-generation

## Summary

The render path selects mip 6 at the reported zoom, but it also schedules mip 0 and reserves a mip-0 tile cost. The cache therefore reports native payload usage even when the viewport needs only coarse tiles.

For 108 tiles, one native `8192 x 4096` Gray8 payload costs 32 MiB. The current accounting reports `108 x 32 MiB = 3.375 GiB`, displayed as 3.38 GiB. This is consistent with the code and is not a measurement error.

## Scope

- Stop adding mip 0 to the visible interest set when the selected mip is greater than zero.
- Prevent `TryGetPixelsNonBlocking` from starting native generation when a coarse mip is the active request, unless the camera policy explicitly requires mip 0.
- Preserve resident-mip fallback without generating an unrequested native payload.
- Key reservations and tracked entries by complete `BackgroundTileCacheKey`, including source, tile, revision, and mip level.
- Charge each reservation the actual payload byte count, not `SampleImageTile.PixelCost` from mip 0.
- Release reservations exactly once on completion, failure, cancellation, eviction, reset, and frame supersession.
- Report resident bytes, mip variants, queued work, and evictions in cache diagnostics.
- Keep defect-image visibility independent from background payload accounting. Disabling sparse object images must not change background cache bytes.

## Acceptance Criteria

- At a camera state that selects mip 6, only mip 6 is requested for background generation.
- Mip 0 is requested only when the mip policy selects level 0 or an explicit inspection/readout contract requests it.
- A zoomed-out render does not allocate or reserve mip-0 bytes as a side effect of drawing a coarse mip.
- Cache usage equals the sum of resident Gray8 payload lengths for each resident source, revision, tile, and mip variant.
- A 108-tile scene reports approximately 3.38 GiB only when 108 native payloads are resident. It reports the much smaller mip-6 payload total when only mip 6 is resident.
- Reset, cancellation, rejection, eviction, and replacement do not leak budget bytes or release a reservation twice.
- A resident fallback remains renderable while a newly requested mip is generated.
- Tests cover selected mip scheduling, no-native-generation at coarse zoom, variant identity, exact byte accounting, reservation release, and sparse-image visibility independence.
- Windows raster tests verify that the source dimensions match the resident mip payload.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Focused validation passed for changed areas. Core focused tests: 57/57 passed (`SampleImageTileTests`, `TileWorkCoordinatorTests`, `SampleImageGeneratorTests`). Windows focused tests: 10/10 passed (`ZeroCopyBitmapFactoryTests`). App Release build succeeded with one pre-existing warning (`MainWindow._frameClaimantId` unused).

## Notes

- `BackgroundTileMipPolicy.SelectMipLevel` already returns level 6 for the reported zoom. The defect is downstream scheduling and accounting.
- `SampleImageTile._pixelCost` is fixed at native dimensions. `TileCacheBudget.TryReserve` tracks only `tile.Id` and charges that value.
- Existing ADR-0005 requires variant keys and actual payload byte costs. ICW-150 implements the missing materializer/cache portion of that decision.
- ICW-P0-LEASE-RELEASE and ICW-P1-PIXELCOST-MIPS are now being implemented as part of this bug slice to keep accounting and release semantics consistent.

## Related Tasks

- ICW-P0-LEASE-RELEASE
- ICW-P1-PIXELCOST-MIPS
- ICW-076
- ICW-096
- ICW-134
- ADR-0005
