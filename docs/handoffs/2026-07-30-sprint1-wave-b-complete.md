# Handoff: Sprint 1 Wave B Complete — Viewport Correctness Harness

**Date:** 2026-07-30
**HEAD:** ea399d0
**Previous handoff:** 2026-07-30-sprint1-wave-a-complete.md

## Summary

Sprint 1 Wave B delivered three items that complete the coordinator correctness layer. ICW-143 (viewport culling) is now unblocked. Each item was validated with `dotnet test` (88/88 passing) and a Release build.

## Deliverables

### Wave B-1: ICW-P1-CLAIMANT-TOKENS — Per-Tile Claimant Identity + Real Cancellation Tokens

**Status:** Done

**Changes to `SampleImageTile.cs`:**
- Replaced `static readonly object DefaultCoordinatorClaimant` with instance field `_perTileClaimant` — each tile now has its own identity. `RemoveAllClaimants` on one tile no longer cancels every tile's generation.
- Added `Func<CancellationToken>? ClaimantTokenProvider` property (mirrors `ClaimantIdProvider` pattern). When set, provides the per-frame cancellation token to coordinator `Request` calls.
- Added `GetClaimantToken()` helper that returns `ClaimantTokenProvider?.Invoke()` or `CancellationToken.None`.
- Both `Request` call sites (`EnsurePixelsGenerationStarted` and `EnsureMipPixelsGenerationStarted`) now use `GetClaimantToken()` instead of `CancellationToken.None`.

**Changes to `MainWindow.xaml.cs`:**
- Added `_frameTileCts` and `_previousFrameTileCts` fields for per-frame token-source replacement.
- In `RenderFrameAsync`, the previous frame's CTS is cancelled before rendering the new frame, causing stale claimants to be auto-removed from the coordinator.
- Deferred two-frame disposal pattern: the cancelled CTS is disposed two frames later to avoid disposing while in-flight cancellation callbacks are still running.
- `RegenerateSceneAsync` now sets `ClaimantTokenProvider` on each tile to return `_frameTileCts?.Token`.

### Wave B-2: ICW-P0-QUEUE-DRAIN Phase 0 + Phase 1 — Full Liveness Check

**Status:** Done

**Changes to `TileWorkCoordinator.cs`:**
- Phase 0 (from Wave A): `DrainQueueWithLivenessCheck(CancellationToken)` method skeleton with CancellationToken.None placeholder.
- Phase 1 (this wave): Replaced placeholder token check with real `item.ClaimantCount == 0` check. When a queued item has no live claimants (their tokens fired and the auto-removal callback removed them), it is canceled and skipped instead of promoted. This prevents stale-token items from blocking usable items behind them or wasting concurrency slots.

**Changes to `TileWorkCoordinatorTests.cs`:**
- Replaced Phase 0 placeholder test with `DrainQueueWithLivenessCheck_RemovedItem_SkipsInQueue` — verifies that orphaned queue entries (items whose claimants were removed) are correctly skipped during drain.

### Wave B-3: ICW-P0-PIXELOMETER-READOUT — Cache Budget in Pixelometer Path

**Status:** Done (interim fix)

**Changes to `MainWindow.xaml.cs`:**
- In `TryReadPixelValue`, the `TryGetPixelsNonBlocking` call now passes `_tileCacheBudget.TryReserve` as the reservation function.
- Previously, the default `null` value meant hover-triggered tile generation bypassed `TileCacheBudget` entirely, creating untracked, unevictable tiles.
- Long-term conversion to published-frame snapshot is deferred.

## Validation

```
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
Passed! 88/88 tests
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
Build succeeded, 0 errors, 1 pre-existing warning
```

## Remaining Work

These items are still open but no longer block ICW-143:

| Priority | Task | Status | Notes |
|---|---|---|---|
| P0 | ICW-143 — Viewport culling | **Unblocked** | All Phase 0/1 coordinator dependencies and ICW-100 are now complete |
| P0 | ICW-P0-STALE-PUB — Tile-level stale publication guard | Open | Shares epoch mechanism with ICW-100 |
| P0 | ICW-P0-SPATIAL-INDEX-SAFETY — Immutability copy-on-query | Open | ICW-060 tracks this |
| P1 | ICW-P0-TRANSACTIONAL-REGEN — Atomic regenerate | Open | Depends on ICW-102 render disposal fence |
| P1 | ICW-P0-BUFFER-REUSE-SYNC — Compositor handoff race | Open | Linked to ICW-021 |
| P1 | ICW-P0-LEASE-RELEASE — IDisposable lease pattern | Open | Depends on ICW-P1-PIXELCOST-MIPS |
| P1 | ICW-P1-COOPERATIVE-CANCEL — In-factory cancellation | Open | |
| P1 | ICW-P1-GDI-CONCURRENCY — GDI+ bounding | Open | Depends on ICW-P0-ACTIVECOUNT (already resolved) |
| P1 | ICW-P1-SETTINGS-VALIDATION — Unified validation | Open | |
| P1 | ICW-P1-PIXELCOST-MIPS — Mip-aware cost accounting | Open | |

## Next Step Recommendation

**ICW-143 (viewport culling)** is now unblocked. The coordinator correctly bounds concurrency, per-tile claimant identity prevents cross-tile cancellation, per-frame tokens auto-remove stale claimants, frame-level stale rejection (ICW-100) guards `PublishFrame`, and the pixelometer no longer bypasses cache budget. Viewport culling and relevance-priority scheduling can safely begin.
