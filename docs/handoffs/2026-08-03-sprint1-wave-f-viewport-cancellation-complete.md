# Handoff: Wave F Viewport Cancellation Complete

**Date:** 2026-08-03
**Previous handoff:** 2026-07-30-sprint1-wave-e-complete.md

## Summary

Wave F delivers the viewport cancellation safety slice. Running tile work now releases coordinator ownership only when the worker physically stops. Queued work releases its ownership exactly once. Coordinator-backed native and mip tile generation observes the live claimant cancellation token during expensive phases.

## Deliverables

### F-1: Coordinator cancellation cleanup

`TileWorkCoordinator.CancelWorkItem` no longer removes and releases running items at cancellation-request time. The running branch relies on `HandleWorkStopped` for removal and reservation cleanup. The queued branch keeps removal and reservation release because queued work never reaches worker termination. A code comment documents the bounded duplicate-admission window during cancel and re-request.

### F-2: Cooperative cancellation in generation

`SampleImageTile` now accepts optional token-aware pixel and mip factories while preserving the legacy constructor overloads. Coordinator-backed native and mip paths pass the live work token into generation. `SampleImageGenerator` gained a `CancellationToken` parameter across `GenerateMonochromeMipPixels`, `GenerateMonochromeMipPixelsSeeded`, `ApplyMipDetails`, `ApplyDetailsWithGdiPlus`, `ApplyCirclesWithRasterizer`, and `GenerateNoisePixelsCore`. Checks run before and after noise, circle, label, and pixel-transfer phases.

### F-3: Regression coverage

Two new tests cover cooperative cancellation:

- `GenerateMonochromeMipPixels_WithCanceledToken_ThrowsPromptly`
- `GenerateMonochromeMipPixels_WithTokenCanceledMidGeneration_StopsWithinBound`

### F-4: Tracker updates

- New ticket `docs/tasks/tickets/ICW-WAVE-F-VIEWPORT-CANCELLATION.md` (Done).
- `docs/tasks/active-tasks.md` and `docs/tasks/task-tracker.md` updated to Done with evidence.

## Validation

Commands:

- `dotnet build tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` — 0 errors, 0 warnings
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` — 95/95 passing
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release` — 10/10 passing
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release` — 0 errors

## Notes and Blocker History

The workspace editor buffer and the on-disk file diverged for `SampleImageTile.cs`. The editor held the correct token-aware getter call while disk kept the old zero-argument call, which blocked the build. The on-disk file was corrected and validated.

## Next Step Recommendations

1. Prioritize `ICW-P0-LEASE-RELEASE` (IDisposable cache reservation lease) and `ICW-P1-PIXELCOST-MIPS` (mip-aware byte accounting). These are the remaining cache-accounting prerequisites.
2. Land `ICW-P0-ACTIVECOUNT-residuals` before the lease work to remove the running-item double-release hazard.
3. Continue `ICW-144` stress benchmark evidence with stage diagnostics counters (ICW-132).

