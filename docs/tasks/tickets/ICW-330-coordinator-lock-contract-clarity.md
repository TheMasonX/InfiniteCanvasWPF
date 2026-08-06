---
id: ICW-330-coordinator-lock-contract-clarity
author: InfiniteCanvas Agent
key: ICW-330
title: Clarify the coordinator lock contract and SetRunning query semantics
status: Done
type: Task
priority: P3
tags:
  - coordinator
  - readability
  - documentation
  - concurrency
dependsOn: []
related:
  - ICW-320
  - ICW-322
  - ICW-327
  - ADR-0006
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
  - docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-19-50-44.md
  - docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-20-09-37.md
  - docs/audits/icw-wave-e-audit-delta-8.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-330 — Clarify the coordinator lock contract and SetRunning query semantics

## Summary

Audit synthesis cleanup (three small, low-risk items consolidated). All three are documentation and readability, no behavior change:

1. **C-010 (pass6 #4):** `SetRunning()` (`TileWorkCoordinator.cs:868`) uses `Interlocked.Exchange` but every call site already holds `_lock`, making the interlocked exchange redundant. `CancelWorkItem` reuses the mutating method purely to query prior running state (`var wasRunning = !item.SetRunning() && _activeCount > 0;`), which is confusing and flips `_running` for the canceled item.
2. **C-016 (C11):** `CancelWorkItem` relies on an undocumented caller-held-lock contract. Every call site holds `_lock`, but the contract is not documented. The original C11 finding was rejected as "stale" by the 2026-08-03 council without rationale; the code still exhibits the pattern.
3. **C-002 (delta-8 minor note):** `EvictCacheEntry` clears pixels and resets `_generationQueued` but does not bump `_generationEpoch`. The Wave G comment claiming "epoch guards discard the stale result" is imprecise for the eviction case: the actual discard comes from the `_pixels is null` check in `OnCoordinatorPixelsGenerated`.

## Scope

- Split `SetRunning()` into a non-mutating `IsRunning` query used by `CancelWorkItem`, and keep the atomic transition in `StartWorkItem`.
- Document the caller-held-lock requirement on `CancelWorkItem` and `StartWorkItem` (matching the ICW-322 reentrant-chain documentation pattern).
- Fix the eviction-discard comment at the `Request` coalesce site to name the actual mechanism (`_pixels is null` guard) for the eviction case.

## Acceptance Criteria

- The caller-held-lock contract is documented on both methods.
- `CancelWorkItem` no longer calls a mutating method to query state; coordinator behavior is unchanged.
- The eviction comment describes the real discard mechanism.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Result: coordinator suite green; core 183/183, Windows 22/22, solution Release build 0 errors.

## Notes

- Delivered in Wave I (2026-08-06).
- Survived Wave F and Wave G hardening of the same file; deliberately low priority, but cheap to land.
- Landed together with ICW-327 (same file).

## Related Tasks

- ICW-320 (Wave F cancel-and-re-request, Done)
- ICW-322 (reentrant lock chain documentation, Done)
- ICW-327 (AddClaimant registration refresh, Done)
- ADR-0006 (viewport-aware tile work scheduling)
