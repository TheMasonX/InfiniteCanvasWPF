---
id: ICW-076-background-tile-mip-levels
key: ICW-076
title: Add background tile mip-level fetching for zoomed-out views
status: In Progress
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
  - src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs
  - tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs
created: 2026-07-25
updated: 2026-08-07
---

# ICW-076 - Add background tile mip-level fetching for zoomed-out views

## Summary

Add zoom-dependent mip selection to the background tile path so zoomed-out views load lower-resolution tile variants instead of always fetching the full-resolution source. The canvas and renderer must consume source-neutral descriptors, requests, and Gray8 payloads so a future external API can replace the synthetic generator without changing canvas inputs. This should reduce memory pressure and CPU cost while keeping coarse tile appearance stable across mip levels.

## Scope

- Define `BackgroundTileDescriptor`, `BackgroundTileCacheKey`, `BackgroundTileRequest`, `BackgroundTilePayload`, and `IBackgroundTileSource` in the rendering boundary; descriptors must carry a source-scoped stable identity and immutable content revision, not synthetic delegates, `System.Drawing.Bitmap`, external API types, or annotation ownership.
- Add a source-neutral asynchronous materializer/cache between the render coordinator and `IBackgroundTileSource`. It selects the mip from the captured camera, coalesces equal requests, owns reservations and completion, and gives the synchronous rasterizer only a resident payload or placeholder.
- Support at least eight reduction levels, where each level reduces the source image resolution by half in each dimension (4x fewer pixels).
- Require canonical mip dimensions of `max(1, ceil(nativeDimension / 2^level))`; reject payloads that do not match the requested level and dimensions rather than silently caching an API-selected substitute.
- Key cache entries, reservations, and visible-frame pins by source identity, tile identity, content revision, and mip level, using the payload's actual byte cost rather than the native tile cost.
- Keep the selection path compatible with both synthetic tile generation and real-world external tile sources.
- Use deterministic low-pass reduction for synthetic mips; point-sampled floor-coordinate decimation is not acceptable because it aliases sparse content.
- Keep pixelometer sampling source-neutral through an explicit mip-zero materializer request; it must not synchronously generate or decode native payloads on hover.

## Acceptance Criteria

- The background tile system automatically selects a lower-resolution mip variant when the viewport is zoomed out far enough.
- The implementation supports at least eight reduction levels in the mip chain.
- Each mip step halves the image resolution in each dimension, reducing the pixel count by approximately 4x.
- Lower-resolution tiles are generated or fetched from a stable sampling path that remains visually coherent across mip transitions.
- Canvas and renderer inputs depend only on the source-neutral tile request/payload contracts, and a synthetic provider remains a replaceable implementation.
- The rasterizer performs no asynchronous source call, cache admission, or mip-policy decision; it samples only an available validated payload using that payload's dimensions.
- Cache keys, reservations, and active-frame pins include source identity, tile identity, content revision, and mip level; byte accounting uses each validated payload's actual byte cost.
- The materializer coalesces concurrent equal requests. Cancellation or scene replacement cannot leave a reservation behind, cancel another frame's shared fill, or publish a completion into a replacement scene.
- Synthetic mip generation is deterministic and low-pass filtered; regression coverage distinguishes it from floor-coordinate point sampling.
- Pixelometer sampling remains mip-zero and non-blocking until a separately designed point-sampling source capability exists.
- The behavior is documented in the tracker and linked from the requirements registry.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~BackgroundTile"`; `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~ZeroCopyBitmapFactory"`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`; `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Result: Contracts, canonical mip policy, deterministic low-pass synthetic generation, per-mip tile storage, and camera-selected Windows raster sampling are implemented. Focused core tests 25/25 and Windows raster tests 5/5 passed. Full materializer/cache migration remains pending. Task validation remains blocked by the pre-existing invalid `Reverted` status in ICW-072.

## Notes

- ADR-0005 records the chosen boundary and migration sequence.
- The pure mip policy chooses the coarsest canonical level that still supplies at least one texel per output pixel on both axes, clamped to levels zero through seven. It is evaluated before rasterization from the captured `CameraSnapshot`.
- External providers must honor canonical dimensions for a requested mip. A future server-side negotiation API is a separate contract, not an implicit exception to payload validation.
- Pixelometer reads retain an explicit non-blocking mip-zero inspection path unless a dedicated source-neutral point-sampling capability replaces it.

## Findings

- Current code stores a single full-resolution `Func<byte[]>`/Windows bitmap factory on `SampleImageTile`, so canvas inputs are coupled to the synthetic implementation.
- `ZeroCopyBitmapFactory.DrawTile` calls the tile's non-blocking generation path and samples `tile.PixelWidth` and `tile.PixelHeight` directly. It must become a payload-only consumer; an async external source cannot be introduced inside this synchronous raster loop.
- `TileCacheBudget` keys and pins by tile ID and budgets one full-resolution tile cost, which would undercount and incorrectly protect multiple mip payloads.
- The prior floor-coordinate sampling proposal aliases high-frequency source details. Canonical low-pass reduction is required for stable mip transitions.

## Wave AE Update, 2026-08-07

Added `BackgroundTileMaterializer` as the first source-neutral materializer slice. The materializer uses `TileWorkCoordinator` for equal-key coalescing and claimant cancellation. It validates returned payloads through `BackgroundTilePayload`, accounts actual variant bytes, supports variant pinning, and rejects stale scene completions. Focused tests pass 3/3.

The existing `SampleImageTile` and Windows raster path still use their established synthetic adapters. This wave does not claim the full ICW-076 migration.

## Next Step

Connect `SampleImageTile` to the materializer and migrate the Windows raster path to resident payload dimensions. Preserve non-blocking placeholders and explicit mip-zero pixelometer reads.

## Related Tasks

- ICW-047
- ICW-066
- ICW-074
