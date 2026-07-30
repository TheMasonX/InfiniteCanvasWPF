---
id: ICW-P0-ACTIVECOUNT-residuals
author: Follow-Up Audit (Integration-1)
key: ICW-P0-ACTIVECOUNT-residuals
title: Close two residual issues from the ICW-P0-ACTIVECOUNT fix
status: Proposed
type: Bug
priority: P0
tags:
  - coordinator
  - concurrency
  - cancellation
  - accounting
  - safety
dependsOn: []
related:
  - ICW-P0-ACTIVECOUNT
  - ICW-P0-LEASE-RELEASE
  - ICW-P1-GDI-CONCURRENCY
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-ACTIVECOUNT-residuals — Close two residual issues from the ICW-P0-ACTIVECOUNT fix

## Summary

ICW-P0-ACTIVECOUNT (confirmed fixed in Sprint 1 Wave A) introduced two residual issues that are harmless today but must be resolved before ICW-P0-LEASE-RELEASE ships. Both are in `TileWorkCoordinator.CancelWorkItem`.

**Residual A — Double `ReleaseReservation` call for "canceled while running" items:**
- `CancelWorkItem` still unconditionally executes `_items.Remove(key); ReleaseReservation(key);` at the end of the method, regardless of `wasRunning`.
- For a running item, the worker's eventual termination (`HandleWorkStopped`) *also* calls `ReleaseReservation(item.CacheKey)`.
- Today this only inflates a diagnostic counter — harmless.
- When ICW-P0-LEASE-RELEASE replaces `ReleaseReservation` with an `IDisposable` lease, this becomes a double-dispose bug.
- **Confidence: 85%** (mechanism fully traced; "harmless today" confirmed by reading `ReleaseReservation`'s current body).

**Residual B — Duplicate-admission-during-cancel race:**
- `CancelWorkItem` removes the key from `_items` synchronously when cancellation is *requested* (not when the physical `Task.Run` body actually exits).
- A fresh `Request()` for the same key — e.g., user pans away and back to the same tile — will not coalesce against the still-running old item (since `_items.TryGetValue` now returns false).
- A second, fully independent `Task.Run` starts for the same tile/mip, with duplicate GDI+/noise work.
- `SampleImageTile`'s epoch-based completion guards safely discard whichever result loses the race — no data corruption — but CPU/GDI+ work is wasted.
- **Recommendation:** Accept as known bounded inefficiency, document with a code comment at the `_items.Remove(key)` line.
- **Confidence: 85%** (mechanism fully traced; practical frequency during normal use estimated at 60%).

## Scope

### Required Changes

**Residual A fix:**
1. Move `_items.Remove(key); ReleaseReservation(key);` out of `CancelWorkItem`'s shared tail.
2. Place it in the `else` (queued item) branch only — queued items never reach a worker-termination path, so they must release here.
3. For the `wasRunning` branch, rely on `item.CancelWork()` + the eventual `HandleWorkStopped` to do both removal and release exactly once.

```csharp
// CancelWorkItem (after fix)
if (wasRunning)
{
    item.CancelWork();  // signals token, doesn't touch _activeCount or ReleaseReservation
    // _activeCount decrement and ReleaseReservation happen in HandleWorkStopped
}
else
{
    // Queued item — never reached HandleWorkStopped, must clean up here
    _items.Remove(key);
    ReleaseReservation(key);
}
```

**Residual B documentation:**
4. Add a code comment at the `_items.Remove(key)` line in the queued-item branch:
   ```csharp
   // NOTE: This removes the key from _items at cancel-request time, not at
   // physical worker exit. A subsequent Request() for the same key may start a
   // duplicate worker for the same tile/mip. This is safe (epoch guards in
   // SampleImageTile discard stale results) but wastes CPU during rapid pan-
   // away-and-back. Tracked as ICW-P0-ACTIVECOUNT-residuals.
   ```
5. No additional tracking state should be built for this — the inefficiency is bounded and self-healing.

### Acceptance Criteria

- `CancelWorkItem` no longer calls `ReleaseReservation` (or future `IDisposable.Dispose()`) for running items — only `HandleWorkStopped` does.
- The race window is documented at the `_items.Remove(key)` call.
- Existing coordinator tests still pass (the fix only affects running-item cancel path, which existing tests may not cover — see test gap below).

### Test Gap

The existing `CancelAll_WhileItemRunning_DecrementsActiveCount` test (or equivalent) should be checked: does it assert that `ReleaseReservation` is called exactly once per canceled-running item? If not, add that assertion.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs` | Restructure `CancelWorkItem` tail, add code comment for residual B |
| `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs` | Add assertion for exactly-one `ReleaseReservation` per canceled-running item |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "Cancel|ReleaseReservation|ActiveCount"
```

## Related Tasks

- ICW-P0-ACTIVECOUNT: original fix (prerequisite, already done)
- ICW-P0-LEASE-RELEASE: must land after this fix (this ticket removes the double-release hazard)
- ICW-P1-GDI-CONCURRENCY: residual B's duplicate workers make GDI+ concurrency risk marginally higher
