---
id: ICW-143
author: Copilot
key: ICW-143
title: Add viewport culling and relevance-priority tile scheduling
status: Done
type: Improvement
priority: P1
tags:
  - rendering
  - tiles
  - viewport
  - scheduling
  - fast-pan
dependsOn:
  - ICW-142
  - ICW-078
  - ICW-P0-QUEUE-DRAIN
  - ICW-P1-CLAIMANT-TOKENS
  - ICW-P0-STALE-PUB
  - ICW-P0-SPATIAL-INDEX-SAFETY
  - ICW-100
related:
  - ICW-076
  - ICW-096
  - ICW-065
  - ICW-144
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
  - docs/handoffs/2026-07-30-sprint1-wave-d-complete.md
  - docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md
created: 2026-07-26
updated: 2026-07-30
---

## Summary

Viewport culling and relevance-priority tile scheduling delivered in Sprint 1 Wave D. All P0 dependencies cleared. ViewportInterestSet type, PublishInterestSet on TileWorkCoordinator, priority-aware DrainQueueWithLivenessCheck, and full render pipeline wiring in RenderFrameAsync. See council review for post-merge bug fix and supplementary handoff for remaining items.

## Deliverables

### Wave D-1: ViewportInterestSet record type
- Added `BackgroundTileContracts.cs`: `ViewportInterestSet` with `VisibleKeys`, `PrefetchKeys`, `Contains()`, `IsVisible()`, `Empty` static.
- Null-guard validation added during council review.

### Wave D-2: PublishInterestSet on TileWorkCoordinator
- Added `TileWorkCoordinator.cs`: `PublishInterestSet(ViewportInterestSet)` method.
- Cancels queued items whose keys are not in the interest set.
- Running items preserved for cache warming.
- Post-council bug fix: removed premature claimant removal that dropped failure callbacks.

### Wave D-3: Priority-aware drain
- `DrainQueueWithLivenessCheck` scans ahead for visible items when the dequeued item is not visible.
- Visible items promoted first; empty interest set preserves FIFO.

### Wave D-4: Render pipeline wiring
- `MainWindow.xaml.cs` `RenderFrameAsync`: computes interest set from camera snapshot and viewport bounds.
- Includes mip-0 and selected mip level in interest set keys.
- Interest set published before background tile work starts.

## Validation

Commands: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` and `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Outcome: Passed! 93/93 tests, Release build 0 errors, 1 pre-existing warning.

## Post-Review Corrections

1. **Bug fix (2026-07-30):** PublishInterestSet no longer removes claimants before CancelWorkItem — failure callbacks were silently dropped.
2. **Null guard (2026-07-30):** ViewportInterestSet constructor validates arguments.
3. **_disposed guard (2026-07-30):** Added to DrainQueueWithLivenessCheck.

## Deferred Items

- ICW-144: Fast-scroll stress benchmarks (no performance baseline exists)
- Performance: duplicate bounds iteration, GetClaimantIds() LINQ allocation, O(n) RemoveFromQueue
- ICW-081: Ticket deduplication

## Related Tasks

- ICW-141: parent scheduling plan
- ICW-142: cancellation ownership
- ICW-144: stress validation
- ICW-081: ticket deduplication
