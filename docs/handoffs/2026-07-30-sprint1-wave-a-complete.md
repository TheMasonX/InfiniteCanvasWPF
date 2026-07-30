# Handoff: Sprint 1 Wave A Complete — Viewport Safety Harness

**Date:** 2026-07-30
**HEAD:** (current)
**Previous handoff:** 2026-07-30-viewport-requirements-council-handoff.md

## Summary

Sprint 1 Wave A delivered three items from the viewport safety harness. Each item was validated with `dotnet test` (88/88 passing) and a Release build.

## Deliverables

### Wave A-1: ICW-100 — Frame-Level Stale Render Rejection (TRIVIAL)

**Status:** Done

**Changes:**
- Added `RenderRequestTracker _renderRequestTracker` field to `MainWindow.xaml.cs`
- Wired `BeginRequest()` before the background render work in `RenderFrameAsync`
- Added `IsCurrent(requestVersion)` check before `PublishFrame` — stale frames are discarded
- Called `Advance()` after `PublishFrame` to prepare for the next render cycle

**Files changed:** `src/InfiniteCanvas.App/MainWindow.xaml.cs`

### Wave A-2: ICW-P0-QUEUE-DRAIN Phase 0 — DrainQueue Liveness Skeleton (SMALL)

**Status:** Phase 0 complete (proceeding to Phase 1 when ICW-P1-CLAIMANT-TOKENS lands)

**Changes:**
- Added `DrainQueueWithLivenessCheck(CancellationToken)` method to `TileWorkCoordinator`
- `DrainQueue()` now delegates to `DrainQueueWithLivenessCheck(CancellationToken.None)` — no behavior change
- The token check compiles but is a placeholder (Phase 0). Phase 1 wires real per-claimant tokens
- Added 2 focused tests:
  - `DrainQueueWithLivenessCheck_PromotesWhenSlotAvailable`: verifies normal drain behavior
  - `DrainQueueWithLivenessCheck_CallableWithCanceledToken_DoesNotThrow`: verifies skeleton compiles and runs

**Files changed:** `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs`, `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs`

### Wave A-3: Background Noise Settings Reset Fix (SMALL)

**Status:** Done

**Changes:**
- In `RegenerateSceneAsync`, the previous `MainViewModel`'s noise settings snapshot is captured before `InitializeSpatialState()` creates a new `MainViewModel`
- The preserved snapshot is used for tile generation instead of the new default
- First-time initialization (no previous `MainViewModel`) falls back to the new instance's defaults

**Files changed:** `src/InfiniteCanvas.App/MainWindow.xaml.cs`

## Validation

```
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
Passed! 88/88 tests
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
Build succeeded, 0 errors, 2 warnings (1 pre-existing)
```

## Remaining Critical Path

These items block ICW-143 (viewport culling) and are the highest priority for Sprint 1 Wave B:

| Priority | Task | Status | Effort |
|---|---|---|---|
| P0 | ICW-P1-CLAIMANT-TOKENS — Wire per-frame/viewport CancellationToken | Open | Small-medium |
| P0 | ICW-P0-QUEUE-DRAIN Phase 1 — Wire real claimant-token liveness check | Blocked on ICW-P1-CLAIMANT-TOKENS | Small |
| P1 | ICW-P0-PIXELOMETER-READOUT — Hover must not trigger untracked tile generation | Open | Small |
| P1 | Background noise settings — UI reset was fixed; ICW-P1-SETTINGS-VALIDATION structural pattern remains | Open | Small |
| P0 | ICW-P0-STALE-PUB — Tile-level stale publication guard (share epoch with ICW-100) | Open | Small |

## Notes

- ICW-P0-ACTIVECOUNT was verified as already resolved in the current codebase. The `_activeCount` decrement happens in the worker's termination path, not in `CancelWorkItem`. The coordinator correctly bounds concurrency even during cancellation storms.
- The council finding about ICW-081 (ticket deduplication) was not addressed in this wave. It should be prioritized before creating new P0/P1 ticket files to avoid propagating duplicate IDs.
