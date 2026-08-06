---
id: ICW-327-addclaimant-recoalesce-registration-refresh
author: InfiniteCanvas Agent
key: ICW-327
title: Refresh the CancellationTokenRegistration on AddClaimant re-coalesce
status: Done
type: Bug
priority: P1
tags:
  - coordinator
  - cancellation
  - claimant-token
  - concurrency
dependsOn: []
related:
  - ICW-204
  - ICW-320
  - ICW-143
  - ADR-0006
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
  - docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-20-52-39.md
  - docs/audits/icw-wave-e-audit-delta-6.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-327 — Refresh the CancellationTokenRegistration on AddClaimant re-coalesce

## Summary

Audit synthesis finding (delta-6 via report 2026-08-05-20-52-39, verified at HEAD c552830). `TileWorkItem.AddClaimant` re-coalesce path never refreshed the claimant's `CancellationTokenRegistration`, so a claimant that re-requested a still-running item kept only the spent registration from its first (already-fired) token. After one coalesce cycle the claimant had no live registration on any token that would ever fire again, making the generation uncancellable for its full duration.

## Fix (Wave I, delivered 2026-08-06)

- The re-coalesce branch now disposes the old registration, registers the newest token, and handles the synchronous-fire case exactly like the ICW-320 F-014 first-add path (`TileWorkCoordinator.cs:823`, `:834`).
- New regression test `ReCoalescedClaimant_RegistersNewestToken_CancelStopsWork` (`TileWorkCoordinatorTests.cs:518`) was verified to fail on the buggy shape and passes with the fix.

## Acceptance Criteria

- A claimant that re-coalesces onto a running item is cancellable through its latest token.
- The regression test holds a generation across frame boundaries and confirms canceling the latest token cancels the work.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "TileWorkCoordinator"`
- Evidence: core 182/182, Windows 22/22, App Release 0 errors (Wave I).

## Notes

- Replaces the vague ICW-204 "optional follow-up" note with a precisely-diagnosed, high-severity fix.
- This defect defeated claimant-token cancellation for exactly the multi-frame generations ICW-204 was built to handle. It is a prerequisite for trusting the coordinator under live web-inspection streaming.

## Related Tasks

- ICW-204 (tile generation lost on scroll, Done)
- ICW-320 (Wave F cancel-and-re-request, Done)
- ICW-143 (viewport culling, Done)
- ADR-0006 (viewport-aware tile work scheduling)
