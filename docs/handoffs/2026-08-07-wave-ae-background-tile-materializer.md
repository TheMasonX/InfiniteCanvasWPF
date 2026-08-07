# Wave AE Handoff, Background Tile Materializer

Date: 2026-08-07
Status: Complete

## Review Result

Wave AD source changes are scoped to the three deleted rendering abstractions and the retained source boundary.
The Wave AD handoff had one malformed line. The line is corrected in this wave.
The worktree still contains unrelated ICW-336 settings changes. This wave does not modify them.

## Delivered

- Added `BackgroundTileMaterializer` in the rendering project.
- Reused `TileWorkCoordinator` for equal-key coalescing and claimant cancellation.
- Validated source payloads through `BackgroundTilePayload` before caching.
- Accounted actual bytes for each source, tile, revision, and mip variant.
- Added pinned variant admission and deterministic resident eviction.
- Suppressed completion from a replaced scene after `AdvanceScene`.
- Added three focused materializer tests.

## Evidence

- Focused materializer tests pass 3/3.
- The rendering project compiles through the focused test command.
- The next migration step remains outside this wave.

## Standards Review

- The materializer uses existing coordinator and source contracts.
- The materializer does not reference WPF or synthetic generator types.
- The cache key includes source identity, tile identity, content revision, and mip level.

## Spec Review

- This wave satisfies the first materializer slice in ICW-076.
- SampleImageTile and the Windows raster path still use existing adapters.
- Full ICW-076 acceptance remains open until those paths consume resident source-neutral payloads.

## Next Step

Connect SampleImageTile and the Windows raster path to `BackgroundTileMaterializer`.
Add a Windows zoom-transition regression after that migration.