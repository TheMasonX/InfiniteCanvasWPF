# ICW-049: Tile Cache Budget Capacity Regression

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Fix the tile-cache default budget so it can retain generated default-size tiles. The existing 4,000,000-pixel budget is smaller than one 8192x4096 tile (33,554,432 pixels), causing every completed tile to be immediately evicted and regenerated.

## Scope

- Set the default cache budget to retain a bounded number of default-size tiles.
- Preserve pixel-cost-based cache accounting and eviction.
- Add focused regression coverage proving the configured default capacity can retain at least one default tile.
- Update the cache invariant and task trackers with validation evidence.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Results:

- Focused `SampleImageGeneratorTests`: 7 passed, 0 failed.
- Release application build: succeeded.

## Findings

- Runtime diagnostics repeatedly reported `Budget 0/4,000,000 pixels | 0/256 cached | generated 0/256` after the view remained still.
- A default tile costs 33,554,432 pixels, so `TileCacheBudget.TrackTile` adds it, observes budget overflow, evicts the same tile, and calls `ResetImageCache`.

## Outcome

- The default budget is now 134,217,728 pixels, retaining four default-size tiles before pixel-budget eviction begins.
- Default tile dimensions are named constants shared by generation, cache sizing, and regression coverage.
