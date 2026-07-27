---
id: ICW-141
author: Copilot
key: ICW-141
title: Plan viewport-aware tile work scheduling and cancellation
status: Proposed
type: Epic
priority: P1
tags:
  - rendering
  - tiles
  - cancellation
  - scheduling
  - fast-pan
  - performance
dependsOn:
  - ICW-076
related:
  - ICW-096
  - ICW-132
  - ICW-133
links:
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Fast navigation can create tile generation demand faster than the current per-tile workers complete it. Establish a bounded, viewport-aware work pipeline that removes stale queued requests, cancels unclaimed in-flight work, prioritizes useful tiles, and preserves shared cache-fill correctness.

## Scope

- Define the request, claimant, cache-key, viewport-epoch, and cancellation ownership contracts.
- Coordinate child tasks ICW-142, ICW-143, and ICW-144.
- Preserve non-blocking rendering, resident mip fallback, cache reservations, and stale-frame protection.

## Acceptance Criteria

- The implementation has one documented owner for tile admission, queueing, cancellation, concurrency, and completion accounting.
- Rapid pan/zoom does not allow unbounded queued tile work or leave stale requests ahead of current visible work.
- Cancellation never publishes stale pixels, leaks cache reservations, or cancels a shared fill that still has a current claimant.
- Visible tiles remain eligible for generation and the renderer continues to show resident imagery/placeholders without waiting for the queue to drain.

## Validation

Command: `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

Outcome: Pending implementation; this planning record is being added before code changes.

## Notes

The existing `CoalescingAsyncAction` coalesces render requests only. `SampleImageTile` currently starts fire-and-forget `Task.Run` work and uses generation flags/epochs, which is insufficient for viewport culling. Do not introduce a second independent cache identity or bypass ADR-0005's source-neutral materializer boundary.

## Related Tasks

- ICW-142: bounded cancellable tile materialization ownership
- ICW-143: viewport culling and relevance-priority queue
- ICW-144: fast-scroll stress telemetry and benchmark evidence
