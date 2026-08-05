---
id: ICW-322-reentrant-lock-chain-eviction
author: InfiniteCanvas Agent
key: ICW-322
title: Document or restructure the reentrant lock chain in cache eviction
status: Proposed
type: Bug
priority: P2
tags:
  - coordinator
  - concurrency
  - locking
  - cache
related:
  - ICW-P0-LEASE-RELEASE
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-322 — Document or restructure the reentrant lock chain in cache eviction

## Summary

Audit synthesis finding F-009. Cache eviction calls back into `TileWorkCoordinator._lock` while `Request` still holds it: `Request` (line 186) → `TileCacheBudget.TryReserve` (line 1070) → `EvictCacheEntry` (line 487) → `RemoveClaimant` (line 223). The chain is safe only through same-thread `Lock` reentrancy and becomes a hard deadlock if any site gains an `await` or a thread hop.

## Scope

- Document the chain at all three sites, or restructure so evicted keys are returned to `Request` and `RemoveClaimant` runs after `_lock` exits.
- Add a comment stating the same-thread reentrancy dependency and forbidding an `await` or thread hop inside the chain.

## Acceptance Criteria

- The chain is documented at all sites, or the callback-outside-lock restructure is in place.
- No behavior change.

## Validation

- Command: review gate tied to ICW-P0-LEASE-RELEASE and the reusable-engine memory governor
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "TileWorkCoordinator"`

## Notes

- Not blocking today. Becomes urgent before ICW-P0-LEASE-RELEASE or any async memory-governor work.
- A regression test cannot easily force a deadlock; the discriminating check is a design review gate.

## Related Tasks

- ICW-P0-LEASE-RELEASE (trigger)
