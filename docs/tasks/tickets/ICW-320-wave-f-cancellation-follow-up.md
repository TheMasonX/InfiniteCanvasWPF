---
id: ICW-320-wave-f-cancellation-follow-up
author: InfiniteCanvas Agent
key: ICW-320
title: Harden coordinator cancel-and-re-request window (Wave F follow-up)
status: Proposed
type: Bug
priority: P2
tags:
  - coordinator
  - cancellation
  - concurrency
  - tile-generation
dependsOn:
  - ICW-WAVE-F-VIEWPORT-CANCELLATION
related:
  - ICW-P0-ACTIVECOUNT-residuals
  - ICW-205
  - ICW-144
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-320 — Harden coordinator cancel-and-re-request window (Wave F follow-up)

## Summary

Audit synthesis findings F-006, F-007, F-014. During scroll-away-and-back, `Request` coalesces a fresh claimant onto an already-canceled still-running item, swallowing one regeneration round trip. `HandleWorkStopped` removes `_items[key]` by key without reference equality, which can clobber a newer item once the coalesce fix lands. `AddClaimant` registers the token callback before adding the claimant, leaving a ghost for a pre-canceled token.

## Scope

One atomic change in `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`:

- `Request` (lines 176-178): do not coalesce when the existing item is in a terminal state. Treat a canceled running item as not present.
- `HandleWorkStopped` (line 510): remove only when `ReferenceEquals(current, item)`.
- `AddClaimant` (lines 783-786): add the claimant before registering the token callback, or skip the add for an already-canceled token.

## Acceptance Criteria

- `RunningWorkCanceled_ReRequest_AdmitsFreshItem` passes (fails on HEAD): a scroll-away-and-back re-request during the cancel window admits a fresh work item.
- `LateWorkerStop_DoesNotRemoveNewerItem` passes: a late old-worker stop never removes or invalidates the newer item for the same key.
- `PreCanceledToken_DoesNotLeaveGhostClaimant` passes: a pre-canceled token never leaves a claimant behind.
- Existing coordinator tests pass.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "TileWorkCoordinator"`
- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release`

## Notes

- Do not split F-006 from F-007; they must land together.
- State the accepted duplicate-CPU trade-off in the ticket text: admitting fresh work for a running-canceled key is intentional (ICW-P0-ACTIVECOUNT-residuals residual B).
- Land before ICW-144 closes so its benchmark evidence does not measure the bug.
- Use the existing `ManualResetEventSlim` test pattern to avoid flakiness.

## Related Tasks

- ICW-WAVE-F-VIEWPORT-CANCELLATION (parent)
- ICW-P0-ACTIVECOUNT-residuals (sibling duplicate-admission window)
- ICW-205 (priority queue context)
