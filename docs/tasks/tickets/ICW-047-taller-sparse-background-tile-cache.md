# ICW-047: Taller Sparse Background Tiles and Cache Diagnostics

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Make default background tiles twice as tall and pivot from pre-existing dense backgrounds to noisy image tiles generated on demand near the viewport. Keep generated image tiles in the same cache model as background tiles, use pixel count for cache capacity, and expose image visibility plus per-cache status for debugging.

## Scope

- Double the default tile height.
- Generate noisy sparse image tiles asynchronously as the camera approaches them.
- Apply a shared pixel-budget cache heuristic so smaller image tiles retain more entries for the same configured capacity.
- Add Show Image Tiles and per-cache UI status.

## Validation

- Add rendering/cache unit coverage for pixel-budget eviction and visibility toggle.
- Run core and Windows tests plus the Release app build.

## Findings

- Existing tile generation is non-blocking but has only one tile pixel cache and a debug dump/reset command; it has no cache-budget abstraction or live status display.

## Next Step

- Define cache ownership, pixel-cost accounting, and tile/image generation contracts before changing the renderer.