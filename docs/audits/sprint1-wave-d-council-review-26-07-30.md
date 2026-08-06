# Council Review: Sprint 1 Wave D — ICW-143 Viewport Culling

**Date:** 2026-07-30
**Council seats:** 4 (Viewport Architecture, Coordinator/Concurrency, Implementation Sequencing, Rendering Performance)

## Decision

Sprint 1 Wave D (ICW-143) is functionally correct with one confirmed bug that must be fixed before the wave can be considered complete. The fix was applied during this review.

## Evidence Reviewed

- docs/handoffs/2026-07-30-sprint1-wave-d-complete.md
- docs/handoffs/2026-07-30-sprint1-wave-a-complete.md
- docs/handoffs/2026-07-30-sprint1-wave-b-complete.md
- docs/handoffs/2026-07-30-sprint1-wave-c-complete.md
- docs/ADR/0006-viewport-aware-tile-work-scheduling.md
- docs/requirements/functional-requirements-and-invariants.md
- docs/tasks/active-tasks.md
- docs/tasks/task-tracker.md
- src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
- src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
- docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---|---|
| **Viewport Architecture** | Fix PublishInterestSet bug: claimant removal loop drops failure callbacks, stalling _generationQueued permanently. Add null guards to ViewportInterestSet constructor. | 0.92 | **Yes** — bug causes tiles to become permanently stuck unable to generate via coordinator |
| **Coordinator/Concurrency** | No hard concurrency bugs. Add _disposed guard to DrainQueueWithLivenessCheck. Fix theoretical _activeCount leak in success path disposed case. Fix double ReleaseReservation during cancellation storms. | 0.92 | No |
| **Implementation Sequencing** | Update ICW-143 ticket status from To Do to Done. Resolve duplicate IDs (ICW-100 x4, ICW-102/094/014/098/099 x2). Add Wave D invariants to requirements registry. Correct handoff ordering claim. Update task-tracker.md. | 0.88 | Yes — tracker inaccuracies create confusion about completion state |
| **Rendering Performance** | Add benchmarks for viewport culling path. Eliminate duplicate bounds iteration per frame. Reduce allocations: GetClaimantIds() LINQ, empty prefetch HashSet, O(n) RemoveFromQueue during cancellation bursts. | 0.85 | No — performance concerns are future-sprint work |

## Synthesis

### What changed during review

1. **BUG FIX** — `PublishInterestSet` in `TileWorkCoordinator.cs`: Removed the claimant-removal loop that called `RemoveClaimant` on each claimant before `CancelWorkItem`. The old code emptied the claimant list before `DispatchFailed` could snapshot it, so `onFailed` callbacks were never delivered. This caused `_generationQueued` to remain set at 1, permanently blocking the tile from future coordinator-based generation. Fixed by collecting keys directly into `toCancel` and calling `CancelWorkItem` without pre-removing claimants.

2. **NULL GUARD** — `ViewportInterestSet` constructor in `BackgroundTileContracts.cs`: Added `ArgumentNullException.ThrowIfNull` guards for both `visibleKeys` and `prefetchKeys`. Converted from primary-constructor syntax to explicit constructor to support validation.

3. **DEFENSE-IN-DEPTH** — Added `if (_disposed) return;` guard in `DrainQueueWithLivenessCheck` for consistency with other methods.

### What changes now (tracker updates)

1. Update ICW-143 ticket file: status To Do → Done
2. Update task-tracker.md: ICW-143 To Do → Done
3. Add Wave D invariants to functional-requirements-and-invariants.md
4. Correct Wave D handoff ordering claim: CTS replacement runs BEFORE interest set publication, not after
5. Mark ICW-P0-ACTIVECOUNT as Done (verified — code was already correct)

### What is deferred

| Item | Deferral rationale | Trigger condition |
|---|---|---|
| Duplicate ID resolution (ICW-081) | Requires full audit of all ticket files; non-blocking for correctness | Before creating any new ICW-P0/P1 ticket files |
| Viewport culling benchmarks (ICW-144) | P2 priority; correctness landed first | Before production load or before further performance optimization |
| Performance optimizations (RemoveFromQueue batching, priority queue, GetClaimantIds allocation) | Correctness proven; allocation pressure is acceptable at current tile counts | After ICW-144 benchmarks quantify the overhead |
| Requirements registry Wave B/C/D additions | Non-blocking; correctness invariants already enforced in code | Next time requirements registry is touched |
| ICW-141 epic restructuring | Design-level task; does not affect current implementation | When epic is revisited for sprint planning |

## Dissent

No material disagreement between seats. All seats confirmed:
- The viewport culling logic is architecturally sound and consistent with ADR-0006.
- The PublishInterestSet/DrainQueue integration correctly cancels non-visible queued work and promotes visible items.
- The CTS replacement pattern with deferred disposal is correct.
- All 93 tests pass and the Release build succeeds.

The Performance seat expressed concern about the absence of benchmarks, but this is tracked as ICW-144 and does not block correctness validation.

## Acceptance Criteria

1. [x] PublishInterestSet cancels only queued (not running) items whose keys are not in the interest set
2. [x] Running items are preserved for cache warming
3. [x] DrainQueueWithLivenessCheck promotes visible items over prefetch when interest set is published
4. [x] Empty interest set preserves FIFO drain behavior
5. [x] Per-frame CTS replacement with deferred two-frame disposal works correctly
6. [x] Interest set published before background tile work starts each frame
7. [x] Both mip-0 and selected mip level included in interest set keys
8. [x] CurrentGenerationEpoch is correctly used for cache key construction
9. [x] Failure callbacks are correctly delivered when PublishInterestSet cancels a queued item
10. [x] ViewportInterestSet constructor rejects null arguments
11. [x] All 93 tests pass, Release build succeeds with 0 errors

## Open Questions

1. **What is the typical queue depth under fast-scroll stress?** This determines whether the O(n) scan in DrainQueueWithLivenessCheck and RemoveFromQueue is a real problem. Tracked by ICW-144.

2. **Does PublishInterestSet cause visible lock contention under load?** Coordinator shared lock serializes interest publication with worker completions. Not yet measured.

3. **Should the empty prefetch set be a static singleton?** The per-frame `new HashSet<BackgroundTileCacheKey>()` allocation (~80 bytes) is minor but trivially eliminated.

4. **Is the one-frame CTS disposal gap sufficient for all callback patterns?** Current implementation assumes auto-removal callbacks complete within one frame. If callbacks grow longer, this needs revisiting.

