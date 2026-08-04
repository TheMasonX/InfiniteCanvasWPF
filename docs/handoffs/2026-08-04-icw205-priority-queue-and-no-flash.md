# Handoff: ICW-205 Priority Queue and No-Flash Stability

- Date: 2026-08-04
- Status: Implementation complete, not yet committed
- Ticket: docs/tasks/tickets/ICW-205-priority-queue-tile-work.md

## Summary

ICW-205 replaces the FIFO tile queue with a heap-backed priority queue and enforces the hard no-flash constraint. A visible tile's in-flight generation now survives frame-boundary token fire, so scrolling no longer restarts generation every frame and permanently blanks tiles.

## Findings

- The old drain used a FIFO queue plus an O(n) scan-and-promote loop. The new drain pops a `PriorityQueue` ordered by visibility class, squared center distance, mip suitability, and FIFO sequence.
- Per-frame claimant-token fire used to cancel in-flight work of still-visible tiles. Each frame restarted generation, so a tile that took longer than one frame never completed. The no-flash rule keeps in-flight work alive while its key is in the published interest set.
- Stale work for tiles that left the viewport is still canceled on token fire.
- Queued cancellation uses tombstones keyed to the canceled item's FIFO sequence. A re-admitted same-key item is never skipped.
- `ViewportInterestSet` now carries camera center, selected mip level, and a squared-distance provider derived from tile bounds.

## Changes

- `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`: `TileWorkPriority`, `PriorityQueue`, sequence-scoped tombstones, no-flash interest-set guard, `RebuildQueue` on publish.
- `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs`: `ViewportInterestSet` scheduling context.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`: camera center, selected mip, and distance provider published per frame.
- `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs`: 8 new tests for ordering, tie-breakers, no-flash survival, and re-admission.
- `benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs`: distance-ordered drain and center-change stress benchmarks.
- Docs: ADR-0006 updated, no-flash hard constraint added to the requirements registry.

## Validation Evidence

- Coordinator tests: 33/33 pass.
- Windows tests: 12/12 pass.
- App Release build: 0 errors.
- Benchmarks Release build: 0 errors (17 pre-existing warnings).
- Full core-suite runs are unstable because concurrent work (ICW-309 camera view-model decoupling, uncommitted `CameraTransform.cs` edits) breaks unrelated camera tests in the working tree. Coordinator tests are stable in isolation.

## Recommended Next Step

1. Let the concurrent ICW-309 work commit and resolve the working tree.
2. Run the full core suite to confirm 0 failures.
3. Commit this ICW-205 batch with the docs and handoff note.
4. Run the new fast-scroll BenchmarkDotNet filters on target hardware for evidence.
