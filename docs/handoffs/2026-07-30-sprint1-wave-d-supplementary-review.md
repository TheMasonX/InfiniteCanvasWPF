# Supplementary Handoff: Sprint 1 Wave D Post-Council Corrections

**Date:** 2026-07-30
**Council report:** docs/audits/sprint1-wave-d-council-review-26-07-30.md
**Previous handoff:** 2026-07-30-sprint1-wave-d-complete.md

## Summary

A 4-seat council reviewed the Sprint 1 Wave D (ICW-143) implementation and found one confirmed bug, three defense-in-depth fixes, and several tracker inaccuracies. The bug and fixes were applied during the review. This handoff documents the changes made and the remaining work for the next agent.

## Changes Applied

### 1. BUG FIX: PublishInterestSet dropped failure callbacks (critical)

**File:** `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`
**Issue:** `PublishInterestSet` removed all claimants from queued items *before* calling `CancelWorkItem`. When `CancelWorkItem` called `DispatchFailed`, the claimant list was already empty, so no `onFailed` callback was invoked. The tile's `OnCoordinatorPixelsGenerationFailed` never fired, leaving `_generationQueued` stuck at 1. Subsequent calls to `EnsurePixelsGenerationStarted` via `TryGetPixelsNonBlocking` hit the `CompareExchange` guard and returned immediately without requesting new coordinator work. The tile was permanently blocked from async generation until `ResetImageCache` (scene regeneration).

**Fix:** Removed the claimant-removal loop. `PublishInterestSet` now collects non-interest keys into `toCancel` and calls `CancelWorkItem` directly. `CancelWorkItem` correctly dispatches `DispatchFailed` with the claimant list intact, so `onFailed` fires and `_generationQueued` resets to 0.

**Risk of regression:** None. The old code was strictly worse (lost callbacks). The new code uses the same `CancelWorkItem`/`DispatchFailed` path used by `RemoveClaimant` and `RemoveAllClaimants`, which are well-tested.

**Existing test gap:** `PublishInterestSet_CancelsNonVisibleQueuedItems` did not register `onFailed` callbacks, so the bug was invisible to tests. The test still passes because the functional behavior (item is canceled) is the same — only the callback delivery was broken.

### 2. NULL GUARD: ViewportInterestSet constructor

**File:** `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs`
**Issue:** The `readonly record struct` accepted `null` for `visibleKeys` or `prefetchKeys`, causing `NullReferenceException` at runtime on `Contains`/`IsVisible`.
**Fix:** Converted from primary-constructor syntax to explicit constructor with `ArgumentNullException.ThrowIfNull` guards.

### 3. DEFENSE-IN-DEPTH: Missing _disposed check

**File:** `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`
**Issue:** `DrainQueueWithLivenessCheck` was the only `_lock`-based method without a `_disposed` guard.
**Fix:** Added `if (_disposed) return;` at the top of the method body (after `lock`).

## Tracker Updates Needed

These items should be updated by the next agent:

1. **ICW-143 ticket file** (`docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md`): Change `status` from `To Do` to `Done`, update `updated` date, record validation outcome (93/93 tests, Release build 0 errors).

2. **JIRA.md** (`docs/tasks/JIRA.md`): Change ICW-143 status from `To Do` to `Done`.

3. **ICW-P0-ACTIVECOUNT** in `docs/tasks/active-tasks.md`: Change from `Proposed` to `Done (verified — code was already correct at Sprint 1 start)`. The Wave A handoff correctly noted this but the tracker was never updated.

4. **Requirements registry** (`docs/requirements/functional-requirements-and-invariants.md`): Add a "Sprint 1 Wave D additions" section with these invariants:
   - Interest set publication ordering (CTS replacement before interest set, the current ordering)
   - Non-visible queued item cancellation behavior
   - Visible promotion over prefetch during drain
   - Running items preserved for cache warming

5. **Wave D handoff** (`docs/handoffs/2026-07-30-sprint1-wave-d-complete.md`): The Wave D-4 section says "This runs before the per-frame CTS replacement" about interest set publication. The actual code does CTS replacement first (line 367), then interest set publication (line 400). The handoff claim is technically wrong. If the correct ordering matters, update the handoff text. If the ordering does not matter, add a clarifying note.

## Remaining Known Issues (Deferred)

| Priority | Issue | Status | Notes |
|---|---|---|---|
| P1 | Duplicate ticket IDs (ICW-081) | Proposed | ICW-100 appears 4 times, ICW-102/094/014/098/099 each appear twice in active-tasks.md |
| P2 | Viewport culling benchmarks (ICW-144) | Proposed | No benchmarks exercise PublishInterestSet or DrainQueueWithLivenessCheck. Zero coverage for culling path performance or allocations. |
| P2 | Performance: duplicate bounds iteration | Known | RenderFrameAsync iterates _tiles twice for bounds intersection (once for interest set, once for visibleTiles). |
| P2 | Performance: GetClaimantIds() LINQ allocation | Known | Each PublishInterestSet cancellation allocates object[] via Select().ToArray(). |
| P2 | Performance: O(n) RemoveFromQueue per cancellation | Known | Each CancelWorkItem for a queued item rebuilds the entire queue. |
| P2 | Performance: DrainQueueWithLivenessCheck O(n) scan under lock | Known | Queue scan-ahead holds _lock for the full queue depth. |
| P3 | ICW-141 epic restructuring | Proposed | Council advised Phase 0/1/2+ restructuring but it was never executed. |

## Validation

```
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
Passed! 93/93 tests
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
Build succeeded, 0 errors, 1 pre-existing warning
```

## Next Step Recommendations

1. Apply the tracker updates listed above (ICW-143 ticket, JIRA.md, ICW-P0-ACTIVECOUNT, requirements registry, handoff correction).
2. Execute ICW-081 (ticket deduplication) before creating any new P0/P1 ticket files.
3. Add regression tests that register `onFailed` callbacks for items cancelled by `PublishInterestSet`.
4. Replace `GetClaimantIds()` + per-claimant iteration in `PublishInterestSet` with a direct `CancelWorkItem` call (already done in this review).
5. Begin ICW-144 (fast-scroll stress benchmarks) to establish the performance baseline for future optimization.
