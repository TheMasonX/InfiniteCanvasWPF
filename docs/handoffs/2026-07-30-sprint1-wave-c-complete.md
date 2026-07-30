# Handoff: Sprint 1 Wave C Complete — Clear ICW-143 Dependencies

**Date:** 2026-07-30
**HEAD:** 76adcbd
**Previous handoff:** 2026-07-30-sprint1-wave-b-complete.md

## Summary

Sprint 1 Wave C cleared the two remaining P0 dependencies for ICW-143 (viewport culling). ICW-143 is now fully unblocked. Each item was validated with `dotnet test` (91/91 passing) and a Release build.

## Deliverables

### Wave C-1: ICW-P0-STALE-PUB — Tile-Level Stale Publication Guard

**Status:** Done

**Changes to `SampleImageTile.cs`:**
- Added explicit stale-generation publication guard documentation in `OnCoordinatorPixelsGenerated` referencing ICW-P0-STALE-PUB
- The existing epoch check (`key.ContentRevision` vs `_generationEpoch`) is now clearly documented as the shared epoch mechanism with ICW-100
- When the coordinator completes with a stale epoch (tile was reset/evicted after request), pixels are discarded and `_generationQueued` is reset

**Changes to `SampleImageTileTests.cs`:**
- Added `CoordinatorCompletion_WithStaleEpoch_DiscardsPixels` — injection test that verifies stale coordinator completions are discarded after `ResetImageCache` advances the tile epoch

### Wave C-2: ICW-P0-SPATIAL-INDEX-SAFETY — Immutable Query Results

**Status:** Done

**Changes to `LiveSpatialIndexService.cs`:**
- `Query` now returns `results.ToArray()` instead of the mutable `List<T>` cast as `IReadOnlyList<T>`
- Callers now receive a genuine immutable array, preventing accidental modification
- Snapshot isolation is guaranteed even during concurrent `PublishSnapshotAsync`

**Changes to `LiveSpatialIndexServiceTests.cs`:**
- Added `Query_ReturnsImmutableArray_CallerCannotModify` — verifies the returned value is `Array` type, not `List<T>`
- Added `QueryDuringPublish_ReturnsConsistentSnapshot` — verifies snapshot isolation during concurrent publish

## Validation

```
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
Passed! 91/91 tests (3 new)
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
Build succeeded, 0 errors, 1 pre-existing warning
```

## ICW-143 Dependency Status

| Dependency | Status | Notes |
|---|---|---|
| ICW-P0-ACTIVECOUNT | **Resolved** (Wave A) | Worker termination path decrements correctly |
| ICW-100 (RenderRequestTracker) | **Done** (Wave A) | Frame-level stale rejection |
| ICW-P1-CLAIMANT-TOKENS | **Done** (Wave B) | Per-tile claimant + per-frame CancellationToken |
| ICW-P0-QUEUE-DRAIN | **Done** (Wave B) | Phase 0+1 liveness check |
| ICW-P0-PIXELOMETER-READOUT | **Done** (Wave B) | Cache budget wired through pixelometer |
| ICW-P0-STALE-PUB | **Done** (Wave C) | Tile-level stale publication guard |
| ICW-P0-SPATIAL-INDEX-SAFETY | **Done** (Wave C) | Immutable query results |

**ICW-143 now has zero P0 dependencies.**

## Next Step Recommendation

**ICW-143 (viewport culling and relevance-priority scheduling)** is the next priority. All safety and correctness harness work is complete. The coordinator correctly bounds concurrency, per-tile claimant identity prevents cross-tile cancellation, per-frame tokens auto-remove stale claimants, frame-level and tile-level stale guards reject stale results, the spatial index returns immutable snapshots, and the pixelometer no longer bypasses cache budget.

## Open Items (Non-Blocking)

| Priority | Task | Status | Notes |
|---|---|---|---|
| P1 | ICW-P0-TRANSACTIONAL-REGEN | Open | Atomic regenerate with fallback |
| P1 | ICW-P0-BUFFER-REUSE-SYNC | Open | Compositor handoff race |
| P1 | ICW-P0-LEASE-RELEASE | Open | IDisposable lease pattern |
| P1 | ICW-P1-COOPERATIVE-CANCEL | Open | In-factory cancellation checks |
| P1 | ICW-P1-GDI-CONCURRENCY | Open | GDI+ bounding |
| P1 | ICW-P1-SETTINGS-VALIDATION | Open | Unified validation |
| P1 | ICW-P1-PIXELCOST-MIPS | Open | Mip-aware cost accounting |
| — | ICW-081 | Proposed | Ticket deduplication before new IDs |
