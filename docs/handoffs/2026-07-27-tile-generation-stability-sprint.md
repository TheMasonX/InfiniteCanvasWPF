# 2026-07-27 Tile Generation Stability Sprint Handoff

## Summary

Fixed five critical bugs that prevented background tile generation from completing during fast scroll. The pipeline now self-recovers and the cache no longer deadlocks. Comprehensive logging added at every decision point.

## Bugs fixed (chronological)

| # | Bug | Root cause | Fix | Commit |
|---|-----|-----------|-----|--------|
| 1 | `Queue>0`, completions but 0 backgrounds | `OnCoordinatorPixelsGenerated` doesn't reset `_generationQueued` on epoch mismatch discard | Reset `_generationQueued=0` in else branch | `1e1e1b0` |
| 2 | `Queue=0`, `A0 Q0`, still 0 backgrounds | Factory completes after item canceled → `DispatchCompleted` skipped → callback never fires → flag stuck at 1 | Always dispatch completion callback even when canceled; dispatch failure for queued cancels | `982df7a` |
| 3 | Same as #2 | `OperationCanceledException` handler didn't notify tile | Added `DispatchFailed` in OCE catch | `982df7a` |
| 4 | Render pipeline permanently stops | When all coordinator completions are stale or all generation fails, no re-render is triggered | `OnCoordinatorPixelsGenerated` always fires `PixelsGenerated`; `OnTilePixelsGenerationFailed` dispatches `RequestRenderAsync` | `d03953d` |
| 5 | **Cache deadlock** — "EVICT REJECTED" + "CoordReq REJECTED" spam | Eviction predicate required `IsImageGenerated==true`. Cache fills with 128 un-generated tiles (4 GiB), none evictable, no new tiles admitted | Added fallback: evict un-generated tiles when no generated ones available | `8127200` |

## Cache deadlock detail (bug #5)

The most impactful bug. Logs showed:
```
Cache EVICT REJECTED: no evictable tiles. Tile=TILE-155 cost=33554432 used=4294967296 max=4294967296 pinned=12 tracked=128
```

128 tiles tracked at 32 MiB each = 4 GiB budget exhausted. 12 pinned (visible). The remaining 116 were **never generated** (user scrolled past before generation completed). Eviction required `IsImageGenerated==true`, but none were. Result: **complete generation halt** — no tile could ever get a cache reservation.

Fix: `TileCacheBudget.TryReserve` now has a two-pass eviction:
1. Prefer evicting a generated, unpinned tile
2. Fall back to evicting any unpinned tile (even un-generated)

## Additional improvements

- **Loading indicator**: `RenderBusyBar` shows when coordinator `PendingCount > 0`
- **Comprehensive Serilog logging** at every decision point:
  - `CoordReq START/QUEUE/COALESCE/REJECTED` — tile identity, mip, epoch, counts
  - `Coord COMPLETE/CANCEL/FAILED` — tile identity, state, reason
  - `Cache EVICT/EVICT REJECTED` — tile IDs, costs, budget state, pinned/tracked counts
  - `TileGen DISCARD` — expected vs current epoch when coordinator pixels are discarded
- **Periodic frame diagnostics** every ~120 frames logging coordinator counters, frame timing, tile fetch stats
- **Removed per-frame claimant advance** (`RemoveAllClaimants`) — was causing cancel thrashing where every frame canceled previous frame's in-flight work

## Key files modified

| File | Changes |
|------|---------|
| `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs` | Core coordinator with 19 tests; always dispatches callbacks; comprehensive logging |
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | Coordinator wiring; `_generationQueued` reset on discard; `_backgroundFetched` race fix; cache eviction fallback for un-generated tiles |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Coordinator create/assign/show/dispose; `RenderBusyBar` wiring; frame diagnostics; removed per-frame claimant advance; generation failure triggers re-render |
| `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs` | Unchanged — works correctly |

## Known remaining issues

1. **Cache thrashing under extreme scroll**: When the budget is at capacity and the user rapidly scrolls through many tiles, each new tile admission evicts another tile. The evicted tile may have in-flight coordinator work that gets wasted (epoch bump on `ResetImageCache` causes completion discard). This is bounded by coordinator concurrency (max 4) and doesn't deadlock, but may feel sluggish.
2. **No viewport culling**: Coordinator still generates tiles that have scrolled off-screen. They get evicted and wasted. ICW-143 (viewport interest snapshots) would cancel off-screen work proactively instead of waiting for eviction.
3. **Default concurrency (4) may be low**: For fast initial tile flood, consider raising `TileWorkCoordinator.DefaultMaxConcurrency` to `Environment.ProcessorCount` or 8.
4. **Default budget (4 GiB) fills with 128 tiles**: Each tile is 32 MiB (8192x4096 Grayscale). 128 tiles fill 4 GiB. Consider raising `TileCacheBudget.DefaultMaxBytes` to 8 GiB or 16 GiB on machines with sufficient RAM.

## Validation

```powershell
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Debug
# Expected: 86 passed, 0 failed
dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
# Expected: Build succeeded
```

## Recommended next steps

1. **ICW-143**: Viewport culling — publish viewport interest snapshots, cancel coordinator work for off-screen tiles, prioritize visible tiles. This is the next logical step to eliminate wasted generation.
2. **Tune defaults**: Consider raising `DefaultMaxConcurrency` and `DefaultMaxBytes` based on benchmark evidence.
3. **ICW-144**: Fast-scroll stress benchmarks — repeatable pan/zoom traces to measure queue depth, cancellation rate, useful completion rate.
4. **ICW-132**: Stage-level performance instrumentation — attribute frame time to native generation, normalization, rasterization, composition, cache, mip.

## Log format reference

Logs go to `%LOCALAPPDATA%\InfiniteCanvas\logs\` and the debug output pane. Key patterns to search for:

```
# Cache deadlock (should not appear after fix)
Cache EVICT REJECTED

# Normal coordinator activity
CoordReq START|QUEUE|COALESCE|COMPLETE

# Cancellation (wasted work)
Coord CANCEL|CANCELED

# Discarded pixels (epoch mismatch)
TileGen DISCARD

# Cache eviction
Cache EVICT
```
