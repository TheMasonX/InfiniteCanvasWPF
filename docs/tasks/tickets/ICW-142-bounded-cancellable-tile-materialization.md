---
id: ICW-142
author: Copilot
key: ICW-142
title: Add bounded cancellable tile materialization ownership
status: In Progress
type: Story
priority: P1
tags:
  - rendering
  - tiles
  - cancellation
  - cache
  - concurrency
dependsOn:
  - ICW-076
  - ICW-141
related:
  - ICW-096
  - ICW-064
  - ICW-029
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Replace per-tile fire-and-forget generation admission with a bounded coordinator/materializer that coalesces equal cache-key fills and tracks cancellation ownership separately from shared cache work.

## Scope

- Add a coordinator abstraction for queued, running, completed, failed, and canceled tile requests.
- Enforce configurable maximum active generation and deterministic disposal cancellation.
- Pass cancellation tokens through source/materializer work and require generation code to check them around expensive phases.
- Release cache reservations exactly once on cancellation, failure, or rejected admission.
- Keep generation epoch/revision checks so canceled or reset work cannot publish stale data.
- Expose structured counters for admitted, coalesced, running, completed, canceled, failed, and reservation-released work.

## Acceptance Criteria

- Queue depth and active generation are bounded by configuration.
- Duplicate requests for one `BackgroundTileCacheKey` share one underlying fill.
- A canceled waiter does not cancel a shared fill while another current waiter remains.
- When no claimant remains, queued work is removed and in-flight generation observes cancellation promptly.
- Reset, scene regeneration, and shutdown leave no orphaned workers, callbacks, or cache reservations.
- Focused tests cover cancellation before start, cancellation during generation, shared waiter survival, failure cleanup, and stale publication prevention.

## Validation

Commands: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` and `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`

Outcome: Pending implementation.

## Notes

Do not use `CancellationTokenSource.Dispose` from a viewport-culling loop while a worker may still observe the token. Coordinator ownership must define when token sources are disposed. Preserve the current resident-mip fallback while replacement work is pending.

## Implementation progress

### Done
- `TileWorkCoordinator` class created with:
  - Bounded concurrency (default 4, configurable)
  - Deduplication by `BackgroundTileCacheKey` (coalesces equal keys)
  - Shared-fill claimant tracking with `AddClaimant`/`RemoveClaimant`
  - Per-claimant token registration that auto-removes claimants when their token fires
  - Work-level `CancellationTokenSource` canceled when last claimant is removed
  - `CancelAll()` for shutdown/reset
  - Structured diagnostic counters: admitted, coalesced, completed, canceled, failed, reservation releases
  - `IDisposable` with proper cleanup
- 19 focused unit tests covering cancellation, shared-waiter survival, queue drain, callbacks
- Wired into `SampleImageTile`:
  - New `Coordinator` property
  - `EnsurePixelsGenerationStarted` routes through coordinator when available (with fallback to direct Task.Run)
  - `EnsureMipPixelsGenerationStarted` routes through coordinator when available (with fallback)
  - `ResetImageCache` notifies coordinator to cancel old-revision work
- Wired into `MainWindow`:
  - Coordinator instance created in constructor
  - `CancelAll()` called at start of scene regeneration
  - Coordinator assigned to all tiles after `GenerateSet`
  - Coordinator counters displayed in status bar
  - Coordinator disposed in `OnClosed`
- All 86 core tests pass; Debug and Release builds succeed

### Next steps
1. Proceed to **ICW-143**: viewport interest snapshots, culling stale requests, and priority ordering
2. Coordinate with **ICW-146**: wire coordinator active-count into a visible loading indicator (RenderBusyBar)

## Related Tasks

- ICW-141: parent scheduling plan
- ICW-143: viewport culling and priority ordering
- ICW-144: stress validation
