# Handoff: Sprint 1 Wave D Complete — Viewport Culling (ICW-143)

**Date:** 2026-07-30
**HEAD:** d7fe07f
**Previous handoff:** 2026-07-30-sprint1-wave-c-complete.md

## Summary

Sprint 1 Wave D delivered ICW-143 (viewport culling and relevance-priority tile scheduling), the main feature that all previous safety/correctness work was paving the way for. The coordinator now receives per-frame viewport interest snapshots, cancels queued work for non-visible tiles, and prioritizes visible tiles over prefetch. Each item was validated with `dotnet test` (93/93 passing) and a Release build.

## Deliverables

### Wave D-1: ICW-143 — Viewport Interest Set (`ViewportInterestSet`)

**Changes to `BackgroundTileContracts.cs`:**
- Added `ViewportInterestSet` record type with `VisibleKeys` and `PrefetchKeys` sets
- `Contains(key)` checks both sets; `IsVisible(key)` checks only the visible set
- `Empty` static property for no-interest state

### Wave D-2: ICW-143 — `PublishInterestSet` on Coordinator

**Changes to `TileWorkCoordinator.cs`:**
- Added `_interestSet` field and `PublishInterestSet(ViewportInterestSet)` method
- On publish: only queued items (not running) are checked against the interest set
- Queued items whose keys are not in the interest set have their claimants removed and are cancelled
- Running items are allowed to complete (their pixels may still be useful for cache warming)
- Added `GetClaimantIds()` on `TileWorkItem` to support claimant enumeration

### Wave D-3: ICW-143 — Priority-Aware Drain

**Changes to `TileWorkCoordinator.cs`:**
- `DrainQueueWithLivenessCheck` now scans ahead for visible items when the dequeued item is not visible
- When an interest set with visible keys is published, non-visible items are re-enqueued while the queue is scanned for a visible candidate
- Visible items are promoted first; if no visible item exists in the queue, the original prefetch item is started
- The empty-interest-set case (no viewport active) preserves the original FIFO behavior

### Wave D-4: ICW-143 — Render Pipeline Wiring

**Changes to `MainWindow.xaml.cs`:**
- Before each render frame, the visible tile set is computed from the camera snapshot and viewport bounds
- Both mip-0 and the selected mip level are included in the interest set
- The interest set is published to the coordinator via `PublishInterestSet`
- This runs before the per-frame CTS replacement and the background render work

**Changes to `SampleImageTile.cs`:**
- Added `CurrentGenerationEpoch` public property for interest set key construction

## Validation

```
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
Passed! 93/93 tests (2 new)
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
Build succeeded, 0 errors, 1 pre-existing warning
```

## New Tests

- `PublishInterestSet_CancelsNonVisibleQueuedItems` — verifies queued items not in the interest set are cancelled
- `DrainQueueWithLivenessCheck_PromotesVisibleOverNonVisible` — verifies visible items are promoted over non-visible items when interest set is published

## Sprint 1 Completion Summary

| Wave | Tasks | Tests |
|---|---|---|
| Wave A | ICW-100 (RenderRequestTracker), ICW-P0-QUEUE-DRAIN Phase 0, noise settings fix | 88 |
| Wave B | ICW-P1-CLAIMANT-TOKENS, ICW-P0-QUEUE-DRAIN Phase 1, ICW-P0-PIXELOMETER-READOUT | 88 |
| Wave C | ICW-P0-STALE-PUB, ICW-P0-SPATIAL-INDEX-SAFETY | 91 |
| Wave D | ICW-143 (viewport culling) | 93 |

## Remaining Work (Non-Blocking)

| Priority | Task | Status | Notes |
|---|---|---|---|
| P1 | ICW-P0-TRANSACTIONAL-REGEN | Open | Atomic regenerate with fallback |
| P1 | ICW-P0-BUFFER-REUSE-SYNC | Open | Compositor handoff race |
| P1 | ICW-P0-LEASE-RELEASE | Open | IDisposable lease pattern |
| P1 | ICW-P1-COOPERATIVE-CANCEL | Open | In-factory cancellation checks |
| P1 | ICW-P1-GDI-CONCURRENCY | Open | GDI+ bounding |
| P1 | ICW-P1-SETTINGS-VALIDATION | Open | Unified validation |
| P1 | ICW-P1-PIXELCOST-MIPS | Open | Mip-aware cost accounting |
| P2 | ICW-144 | Proposed | Fast-scroll stress benchmarks |
| P2 | ICW-132/133 | To Do | Stage instrumentation + benchmark matrix |
| — | ICW-081 | Proposed | Ticket deduplication |
