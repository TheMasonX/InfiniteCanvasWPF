---
id: ICW-204
author: Copilot
key: ICW-204
title: Fix tile generation permanently lost on scroll (claimant token reset gap)
status: Done
type: Bug
priority: P1
tags:
  - rendering
  - tiles
  - coordinator
  - cancellation
  - regression
dependsOn:
  - ICW-P1-CLAIMANT-TOKENS
related:
  - ICW-142
  - ICW-143
  - ICW-146
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Tests/SampleImageTileTests.cs
created: 2026-08-04
updated: 2026-08-04
---

## Summary

Background tile generation is permanently lost during scrolling. Tiles that do not finish generation within one frame stay blank forever. They only regenerate after a zoom that changes the mip level.

## Current behavior

- `MainWindow.RenderFrameAsync` cancels the previous frame `CancellationTokenSource` at the start of every frame (`previousCts?.Cancel()`).
- Every tile uses this per-frame token as its coordinator claimant token (`ClaimantTokenProvider`).
- When the token fires, the coordinator auto-removes the tile's claimant without delivering a completion or failure callback. The work item is orphaned.
- The tile's `_generationQueued` / `_mipGenerationQueued` dedup flag is never reset, so the tile never re-requests generation.
- Zooming changes the mip level, which creates a new cache key and a new `_mipGenerationQueued` entry, so the tile regenerates.

## Root cause

ICW-142 removed per-frame `RemoveAllClaimants` because it caused cancel thrashing. ICW-P1-CLAIMANT-TOKENS re-introduced the same per-frame cancellation through the claimant token. The coordinator removes a claimant on token fire without a callback. The tile's dedup flag is tied to the claim, but nothing resets it when the claim is revoked, so the flag survives the claim.

## Scope

- Reset the tile generation-queued flags when the claimant token fires, so a later frame can re-request.
- Clear the per-mip generation-queued flag on mip factory failure. `OnCoordinatorPixelsGenerationFailed` currently only resets `_generationQueued` (mip 0), so a failed mip stays queued forever.
- Add regression tests for both paths.

## Acceptance Criteria

- A tile whose frame token fires while generation is in flight regenerates in a later frame.
- A mip whose factory fails retries on a later request.
- No regression in the coordinator shared-fill and cancellation tests.

## Validation

Commands:
`dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

Outcome: Passed for changed areas. Core 140 passed, 3 pre-existing unrelated failures (`BuildFeatureRows_UsesTypedFeatureValuesAndStableOrdering`, `MainWindow_PreservesScrollbarOverlayAndRenderUpdateHook`, `AnnotationFeatureDisplayItems_ExposeReadableRows`). Windows 12/12. Release app build 0 errors (pre-existing `_frameClaimantId` warning only).

## Notes

The per-frame CTS design intentionally creates a zero-claimant window at every frame boundary. The dedup flag must be per-frame scoped, matching the ADR-0006 rule that a frame owns a request only for its lifetime.

## Follow-up (2026-08-06, audit synthesis)

The previous "optional follow-up: avoid dooming in-flight work when a frame boundary creates a zero-claimant window" note understated a precisely-diagnosed, high-severity defect in the same vicinity: `TileWorkItem.AddClaimant` re-coalesce never refreshed the claimant's `CancellationTokenRegistration`. The per-frame CTS design means every claimant token fires at the next frame boundary; after one coalesce cycle a multi-frame generation became uncancellable. Tracked and fixed by ICW-327 (Wave I, 2026-08-06).

## Findings

- Confirmed: auto-removal via claimant token leaves `_items` entries with `ClaimantCount == 0` and no callback delivery.
- Confirmed: `OnCoordinatorPixelsGenerationFailed` does not clear `_mipGenerationQueued` for the failed mip.
- In-flight work whose WorkToken is cancelled is doomed, but the re-request path recovers it through a fresh item or a re-added claimant.

## Implementation

- `SampleImageTile.RegisterClaimantReset` resets `_generationQueued` / `_mipGenerationQueued` when the claimant token fires. This makes the dedup flag per-frame scoped, matching the ADR-0006 rule that a frame owns a request only for its lifetime.
- `OnCoordinatorPixelsGenerationFailed` now clears the per-mip flag for mip keys.
- Regression tests: `ClaimantTokenFire_AllowsTileToRegenerateInLaterFrame`, `MipFactoryFailure_ClearsQueuedFlagAndAllowsRetry`.

## Next step

Keep complete. Optional follow-up: avoid dooming in-flight work when a frame boundary creates a zero-claimant window (per-frame WorkToken cancellation). See ICW-205 for the related priority-queue improvement.
