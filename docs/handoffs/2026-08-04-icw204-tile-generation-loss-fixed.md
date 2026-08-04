# Handoff: ICW-204 Tile Generation Loss on Scroll — Fixed

**Date:** 2026-08-04
**Previous handoff:** 2026-08-03-sprint1-wave-f-viewport-cancellation-complete.md

## Summary

Background tile generation was permanently lost during scrolling. Tiles that did not finish generation within one frame stayed blank forever. They only regenerated after a zoom that changed the mip level. The fix makes tile generation-queued flags per-frame scoped so a later frame can always re-request.

## Root Cause

`MainWindow.RenderFrameAsync` cancels the previous frame `CancellationTokenSource` at the start of every frame. Every tile uses that per-frame token as its coordinator claimant token. When the token fires, the coordinator auto-removes the tile's claimant without a completion or failure callback. The tile's `_generationQueued` / `_mipGenerationQueued` dedup flag was never reset, so the tile never re-requested. This re-introduced the cancel thrashing that ICW-142 removed, via the ICW-P1-CLAIMANT-TOKENS per-frame CTS mechanism.

## Deliverables

### D-1: Claimant-token flag reset in `SampleImageTile`

`RegisterClaimantReset(CancellationToken)` resets `_generationQueued` when the frame token fires. `RegisterClaimantReset(int, CancellationToken)` clears the per-mip `_mipGenerationQueued` entry. The reset makes the dedup flag per-frame scoped, matching ADR-0006.

### D-2: Mip failure flag reset

`OnCoordinatorPixelsGenerationFailed` now clears `_mipGenerationQueued` for mip keys. It only reset the mip-0 flag before, so a failed mip stayed queued forever.

### D-3: Regression coverage

- `ClaimantTokenFire_AllowsTileToRegenerateInLaterFrame`
- `MipFactoryFailure_ClearsQueuedFlagAndAllowsRetry`

### D-4: Tracker updates

- New ticket `docs/tasks/tickets/ICW-204-tile-generation-lost-on-scroll.md` (Done).
- `docs/tasks/active-tasks.md` and `docs/tasks/JIRA.md` updated to Done with evidence.
- New invariant `Tile generation recovery` in `docs/requirements/functional-requirements-and-invariants.md`.

## Validation

Commands:

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` — 140 passing, 3 pre-existing unrelated failures
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release` — 12/12 passing
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release` — 0 errors

## Pre-existing Failures (not caused by ICW-204)

Three core tests fail at baseline, confirmed by running them without the ICW-204 changes:

- `BuildFeatureRows_UsesTypedFeatureValuesAndStableOrdering` — `AnnotationFeaturePresenterTests`
- `MainWindow_PreservesScrollbarOverlayAndRenderUpdateHook` — `CanvasScrollbarWiringTests`
- `AnnotationFeatureDisplayItems_ExposeReadableRows` — `SampleImageGeneratorTests`

These are scheduled for repair in the next step.

## Notes

The per-frame CTS design intentionally creates a zero-claimant window at every frame boundary. In-flight work whose WorkToken is cancelled is doomed, but the re-request path recovers through a fresh item or a re-added claimant. A deeper follow-up would avoid dooming in-flight work at the zero-claimant window.

The untracked `docs/tasks/tickets/ICW-205-priority-queue-tile-work.md` ticket belongs to a separate workstream and is left for its own commit.

## Next Step Recommendations

1. Fix the three pre-existing core test failures.
2. Consider avoiding in-flight WorkToken cancellation at the zero-claimant frame boundary.
3. Land `ICW-205` priority queue work in its own commit.
