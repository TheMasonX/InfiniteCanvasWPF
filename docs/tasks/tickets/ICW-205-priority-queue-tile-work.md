---
id: ICW-205-priority-queue-tile-work
author: Copilot
key: ICW-205
title: Replace FIFO tile queue with a true priority queue using center-distance and mip tie-breakers
status: Done
type: Improvement
priority: P1
tags:
  - rendering
  - tiles
  - scheduling
  - priority-queue
  - fast-pan
dependsOn:
  - ICW-143
  - ICW-144
related:
  - ICW-141
  - ICW-142
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
  - docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md
  - docs/tasks/tickets/ICW-144-fast-scroll-tile-queue-stress-validation.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-205 - Replace FIFO tile queue with a true priority queue

## Summary

The tile coordinator stores queued work in a FIFO `Queue<BackgroundTileCacheKey>`. It approximates two-level priority with a drain-time scan-and-promote loop. This does not match the ADR-0006 contract. The contract requires a true priority queue with center-distance and mip-suitability tie-breakers. Replace the FIFO queue with a heap-backed priority queue and deterministic comparators.

This task also enforces the hard no-flash constraint. The visible frame must NEVER flash, flicker, or blank. Per-frame claimant-token fire must not cancel in-flight generation for a tile still in the published interest set. The priority queue delivers visible tiles first so the placeholder window is minimal and deterministic.

## Background

- `TileWorkCoordinator.DrainQueueWithLivenessCheck` drains a FIFO queue. When the dequeued item is not visible, it scans the remaining queue for a visible item. Each scan is O(n) and re-enqueues the entire tail.
- `ViewportInterestSet` exposes `VisibleKeys` and `PrefetchKeys` only. It has no camera center. Center-distance ordering is impossible without adding that data.
- ADR-0006 requires visible requests first, then center distance, then mip suitability as deterministic tie-breakers.
- Per-frame claimant-token fire cancels in-flight work of a still-visible tile. Each frame restarts generation, so a tile that takes longer than one frame never completes. This produces permanent placeholder gray while scrolling. The no-flash constraint requires keeping visible in-flight work alive.
- ICW-143 lists "O(n) RemoveFromQueue" and "duplicate bounds iteration" as deferred performance items.

## Scope

Files to change:
- `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`
- `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs`
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
- `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs`
- `benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs`

Do not change:
- `BackgroundTileCacheKey` identity or semantics
- Reservation release contracts and exactly-once semantics
- `PublishInterestSet` and `RemoveClaimant` public signatures
- Resident-mip fallback in `SampleImageTile`

Hard constraint:
- The visible frame must never flash, flicker, or blank.
- A visible tile's in-flight generation must survive frame-boundary claimant-token fire.
- Stale work for tiles that left the viewport must still be canceled.

## Detailed Instructions

### Step 1: Add scheduling context to the interest snapshot

Extend `ViewportInterestSet` with optional scheduling context.

- Add `double? CenterX` and `double? CenterY` for the camera center in world coordinates.
- Add `int? SelectedMipLevel` for the mip-suitability tie-break target.
- Add `Func<BackgroundTileCacheKey, double>? SquaredDistanceFromCenter`. The caller derives it from tile bounds and the camera center. It returns squared distance so no `Math.Sqrt` runs.
- Keep the two-argument constructor. New fields default to null.
- Add a constructor overload that accepts the new fields.
- `Empty` keeps all new fields null.
- When the fields are null, the coordinator falls back to visible-first ordering with no distance or mip tie-break.
- Derive center and provider in `MainWindow.RenderFrameAsync` from `CameraSnapshot.GetViewportBounds` and the tile bounds.

### Step 2: Define the priority comparator

Add a deterministic priority tuple for `BackgroundTileCacheKey`.

Order:
1. Rank: visible keys above prefetch keys, which rank above stale keys.
2. Keys outside the interest set rank last. Cancel them at admission or publish time.
3. Within the same class, sort by squared distance from the key's tile center to the camera center, from `SquaredDistanceFromCenter`.
4. If distances are equal, prefer the mip level closest to `SelectedMipLevel`.
5. If still equal, use a monotonic FIFO sequence assigned at admission. This preserves insertion order and gives stable FIFO within equal priority.

The comparator must be a pure function of the key, the interest set, and the FIFO sequence. It must produce identical ordering for identical inputs. Add unit tests for each tie-breaker.

### Step 3: Replace the FIFO queue

Replace `Queue<BackgroundTileCacheKey>` with a heap-backed priority queue.

- Use `System.Collections.Generic.PriorityQueue<TElement, TPriority>` or a small custom binary heap.
- Store the priority tuple so re-sorting is cheap.
- Rebuild priorities when `PublishInterestSet` changes the interest set or center.
- Keep `_items` as the source of truth for work state.
- Keep `_activeCount` as the concurrency cap.
- Preserve `DrainQueueWithLivenessCheck` as a public method. Keep its liveness semantics.

### Step 4: Make queued removal O(log n) or lazy

Queued cancellation currently rebuilds the queue. This is O(n). Replace it:

- For per-key cancellation, mark the heap entry as removed with a tombstone set.
- Skip tombstoned entries during drain.
- Rebuild the heap once per `PublishInterestSet` from live queued items in `_items`.
- Clear tombstones on rebuild and on `CancelAll`.
- Do not let tombstones block live work.

### Step 5: Preserve liveness and the no-flash rule

- Keep the claimant-token auto-removal behavior.
- Keep the no-live-claimants check before promotion.
- Keep the rule that queued work with no live claimants and not in the interest set is canceled.
- Add the no-flash rule: when the last claimant is removed from a work item whose key is still in the published interest set, do NOT cancel the work. The next frame re-claims the same key through coalescing.
- Cancel work only when the key is not in the interest set.
- Keep the post-council fix: never remove claimants before `CancelWorkItem`.

### Step 6: Add tests

Add tests to `TileWorkCoordinatorTests.cs`:

- Visible key drains before prefetch key.
- Prefetch key drains before stale key.
- Two visible keys order by center distance.
- Equal distance orders by mip suitability.
- Equal distance and mip order by FIFO sequence.
- A queued item with no live claimants and not in the interest set is skipped.
- Cancellation by key does not block later work.
- `PublishInterestSet` with a new center reorders the queue.
- Interest set with null scheduling context preserves visible-first FIFO behavior.
- No-flash: a visible in-flight item survives its claimant-token fire and completes.
- A non-visible in-flight item is canceled when its claimant-token fires.
- Existing tests must still pass unchanged.

### Step 7: Extend benchmarks

Extend `TileWorkCoordinatorBenchmarks.cs`:

- Compare FIFO drain against priority drain with equal center distances.
- Add a scenario with 1000 visible keys ordered by distance.
- Add a rapid pan/zoom trace that changes the center each cycle.
- Measure queue depth, useful completion rate, and per-drain cost.

## Acceptance Criteria

- The coordinator uses a heap-backed priority queue. No drain-time scan-and-promote loop remains.
- Visible work drains before prefetch work, which drains before stale work.
- Center distance and mip suitability act as deterministic tie-breakers.
- Null scheduling context preserves visible-first FIFO ordering.
- A visible tile's in-flight generation survives frame-boundary claimant-token fire and completes.
- Stale in-flight work for a tile that left the viewport is canceled on token fire.
- The frame never flashes, flickers, or blanks during navigation.
- All existing `TileWorkCoordinatorTests` pass unchanged.
- New tie-breaker, ordering, and no-flash tests pass.
- Release build reports 0 errors.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter FullyQualifiedName~TileWorkCoordinatorTests`
- Result: Passed, 33/33 coordinator tests, including 8 new priority and no-flash tests.
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Passed, 0 errors.
- Command: `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release`
- Result: Passed, 0 errors (17 pre-existing warnings).
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Result: Passed, 12/12.
- Full-suite note: full core-suite runs are unstable because concurrent work (ICW-309 camera view-model decoupling, uncommitted `CameraTransform.cs` edits) breaks unrelated camera tests in the working tree. Coordinator tests are stable in isolation.

## Findings

- The old drain used a FIFO queue plus an O(n) scan-and-promote loop. The new drain pops a heap ordered by visibility class, squared center distance, mip suitability, and FIFO sequence. No scan-ahead remains.
- Per-frame claimant-token fire used to cancel in-flight work of still-visible tiles, restarting generation every frame. The no-flash rule now keeps in-flight work alive while its key is in the published interest set; stale work for departed tiles is still canceled.
- Queued cancellation uses tombstones keyed to the canceled item's FIFO sequence, so a re-admitted same-key item is never skipped.
- `ViewportInterestSet` now carries camera center, selected mip level, and a squared-distance provider derived from tile bounds.

## Notes

- Keep `BackgroundTileCacheKey` as a plain identity record. Do not add distance or priority fields to it.
- Store distances in the heap entry, not in the key.
- The drain must stay O(log n) amortized for admission and removal.
- Rebuild the interest set at most once per published frame. Do not re-sort the heap on every request.
- The FIFO sequence tie-break replaces ordinal key comparison. It gives stable, fair ordering and preserves insertion order across reprioritization.

## Related Tasks

- ICW-143: current two-level priority implementation
- ICW-144: stress benchmark evidence
- ICW-141: parent scheduling plan
- ADR-0006: scheduling contract
