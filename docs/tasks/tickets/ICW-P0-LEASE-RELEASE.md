---
id: ICW-P0-LEASE-RELEASE
author: External Audit (Integration-1)
key: ICW-P0-LEASE-RELEASE
title: Replace ReleaseReservation counter with IDisposable lease pattern
status: Proposed
type: Bug
priority: P0
tags:
  - cache
  - accounting
  - coordinator
  - memory
  - safety
dependsOn:
  - ICW-P0-ACTIVECOUNT-residuals
  - ICW-P1-PIXELCOST-MIPS
related:
  - ICW-134
  - ICW-064
  - ICW-P1-PIXELCOST-MIPS
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-LEASE-RELEASE — Replace `ReleaseReservation` counter with `IDisposable` lease pattern

## Summary

**Critical defect:** `TileWorkCoordinator.ReleaseReservation` (line 431-434) is `Interlocked.Increment(ref _reservationReleases)` — a diagnostic counter with no connection to `TileCacheBudget.UsedBytes`. The actual budget release (`TileCacheBudget.Release(tile)`) is only called from `OnTilePixelsGenerationFailed` on the UI-thread failure event. Cancellations that never surface a UI event (orphaned claimant on tile eviction, frame supersession via `PublishInterestSet`) leak budget bytes forever.

**Confidence:** 98% (external audit: exact line confirmed, mechanism fully traced).

## Root Cause

`TileWorkCoordinator` has a `ReleaseReservation` method that exists as a placeholder but never calls `TileCacheBudget.Release`. There is no `IDisposable` lease object returned from `TryReserve` — the caller receives a `bool` and is expected to remember to call `Release` independently through entirely different code paths (only the UI-thread failure event path does this). Every cancellation, eviction, or frame-supersession path that bypasses `OnTilePixelsGenerationFailed` leaks budget bytes silently.

Compounding the problem: `TileCacheBudget.Release(tile)` decrements by `tile.PixelCost` which is mip-0-only (`ICW-P1-PIXELCOST-MIPS`). Until that ticket lands, even a correct `Release` call would release the wrong byte count.

## Scope

### Required Changes

1. **Introduce `ICacheReservation : IDisposable`** in `TileWorkCoordinator`/`TileCacheBudget`:
   - Returned from a successful `TryReserve` closure instead of `bool`.
   - `Dispose()` performs the actual `TileCacheBudget.Release`-equivalent exactly once.
   - Guard with `Interlocked.CompareExchange` disposed-flag so double-dispose is a no-op.

2. **Change `TileCacheBudget` accounting unit** from `tile.PixelCost` to `ResourceKey`-scoped accounting:
   - Reserve/release per resident payload (source + tile + mip), not per tile.
   - This directly serves ICW-134's variant-aware requirement.

3. **Replace `SampleImageTile.PixelCost`** (single `int`, mip-0-only) with a method that sums all currently-resident mip payload byte counts:
   - `_pixels?.Length ?? 0` plus `_mipPixels.Values.Sum(p => p.Length)`, read under `_cacheGate`.

4. **Route every cancellation/failure/rejection path** through the new lease's `Dispose()`:
   - `CancelWorkItem` (both queued and running branches)
   - `HandleWorkStopped` (normal completion, exception, cancellation)
   - Rejected admission in `Request` (cache budget exceeded)

5. **Fix residual double-`ReleaseReservation` from ICW-P0-ACTIVECOUNT fix** (Audit 2, §2.1 Residual A):
   - `CancelWorkItem` currently calls `ReleaseReservation(key)` for running items.
   - `HandleWorkStopped` in the worker-termination path also calls `ReleaseReservation(item.CacheKey)`.
   - Today this double-call is harmless (counter only). After this ticket, it becomes a double-dispose bug.
   - **Fix:** Remove the eager `ReleaseReservation(key)` call from `CancelWorkItem`'s `wasRunning == true` branch. Keep it only for the queued-item branch (which never reaches worker termination). For running items, rely on `HandleWorkStopped` to do release exactly once.

### Test Requirements

6. **Leak-detection test:** Run N reserve/cancel cycles, assert `TileCacheBudget.UsedBytes == 0` at end.
7. **Double-dispose safety test:** Dispose the same lease twice, assert second call is no-op.
8. **Concurrent reservation test:** Start N concurrent reservations with varying durations, assert `UsedBytes` never exceeds budget and returns to baseline after all dispose.

### Acceptance Criteria

- `ReleaseReservation` counter is replaced or correctly wired to `TileCacheBudget.Release`.
- After any cancellation, failure, eviction, or frame-supersession, `UsedBytes` correctly decreases by the exact bytes that were reserved.
- After N reserve/dispose cycles, `UsedBytes == 0`.
- Double-dispose of any lease is safe (no-op).
- `_activeCount` fix residual A (double `ReleaseReservation`) is resolved as part of this ticket.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs` | Introduce `ICacheReservation`, replace `ReleaseReservation` body, fix double-call in `CancelWorkItem`, route all cancel/fail paths through lease `Dispose` |
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | Replace `PixelCost` with sum-of-resident-mips method, add `_cacheGate`-safe access |
| `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs` | Add `ICacheReservation : IDisposable` interface |
| `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs` | Add leak-detection, double-dispose, and concurrent-reservation tests |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release
```

Scoped filter: `--filter "CacheReservation|LeakDetection|UsedBytes"`

## Related Tasks

- ICW-134: variant-aware cache accounting (this ticket is a prerequisite)
- ICW-P1-PIXELCOST-MIPS: must land before or together with this ticket (lease releases wrong byte count if `_pixelCost` is mip-0-only)
- ICW-P0-ACTIVECOUNT-residuals: fix double-ReleaseReservation before lease is real
- ICW-064: tile-cache admission and diagnostics
