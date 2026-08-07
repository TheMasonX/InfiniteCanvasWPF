---
id: ICW-P1-COOPERATIVE-CANCEL
author: External Audit (Integration-1)
key: ICW-P1-COOPERATIVE-CANCEL
title: Add cancellation checks in tile generation factories around each expensive sub-phase
status: Done
type: Bug
priority: P1
tags:
  - cancellation
  - performance
  - gdi
  - noise
dependsOn:
  - ICW-P1-CLAIMANT-TOKENS
related:
  - ICW-P1-GDI-CONCURRENCY
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-30
updated: 2026-08-06
---

# ICW-P1-COOPERATIVE-CANCEL — Add cancellation checks in tile generation factories around each expensive sub-phase

## Summary

The tile generation factories now observe their live `CancellationToken` throughout the hot path. The factory delegates in `SampleImageTile.cs` pass the coordinator work token into the generator, and the generator checks it before and during each expensive phase.
- The native path does not touch `token` at all.
- The mip path only checks cancellation **after** the expensive GDI+ work completes (line 549: post-hoc `token.ThrowIfCancellationRequested()`).

During fast scroll, GDI+ operations and noise generation continue to completion even after the tile is no longer visible. This wastes CPU and, critically, keeps GDI+ objects alive longer than necessary (compounding the ICW-P1-GDI-CONCURRENCY risk).

**Confidence:** 90% (the original mechanism was fully traced; the implementation and integration regression now verify the cancellation path).

## Resolution

Both coordinator call sites pass a real `CancellationToken` since ICW-P1-CLAIMANT-TOKENS landed in Sprint 1 Wave B. The factories now preserve that token through these stages:

- `SampleImageGenerator.GenerateMonochromeMipPixels` checks before allocation, noise generation, detail rendering, and return.
- `SampleImageGenerator.GenerateNoisePixelsCore` checks before native noise generation and during pixel mapping.
- `SampleImageGenerator.ApplyMipDetails` checks while creating circle inputs and before rasterization.
- `SampleImageGenerator.ApplyDetailsWithGdiPlus` checks before GDI+ work, between drawing operations, and while copying pixels.

The causal chain now works. A claimant token fires, the coordinator cancels `WorkToken`, and the factory observes cancellation at its checkpoints.

## Scope

### Required Changes

1. **Add `CancellationToken` parameter** to `SampleImageGenerator` methods that do expensive work:
   - `GenerateMonochromeMipPixels`
   - `ApplyDetailsWithGdiPlus`
   - `GenerateNoisePixelsCore`
   - `ApplyMipDetails`

2. **Add `token.ThrowIfCancellationRequested()` calls** before and after each expensive sub-phase:

   ```
   ApplyDetailsWithGdiPlus:
     - token.ThrowIfCancellationRequested() at entry
     - Before: System.Drawing.Bitmap construction
     - Before: Graphics.FromImage creation
     - Between: circle rasterization batches (if loop is large enough)
     - After: composite back to target array
   
   GenerateMonochromeMipPixels:
     - token.ThrowIfCancellationRequested() at entry
     - Before: GenerateNoisePixelsCore call
     - Before: ApplyMipDetails call
   
   GenerateNoisePixelsCore:
     - token.ThrowIfCancellationRequested() at entry
     - Before: FastNoise2 GenUniformGrid2D call
   ```

3. **Fix the native factory asymmetry** in `SampleImageTile.cs:420-431`:
   - Currently: `(key, token) => { ... body that never reads token ... }`
   - After: add `token.ThrowIfCancellationRequested()` before/after the expensive calls, mirroring the mip factory pattern.

4. **Thread current token through** to the generator from the factory:
   - The factory receives `token` from `TileWorkCoordinator.StartWorkItem` via `item.Factory(item.WorkToken)`.
   - `WorkToken` is linked to `_disposeCts` and canceled when the last claimant leaves.
   - With ICW-P1-CLAIMANT-TOKENS, this now fires on frame supersession. The token is real and cancelable.

### Test Evidence

- `SampleImageGeneratorTests` verifies pre-canceled and mid-generation cancellation.
- `SampleImageTileTests.ClaimantTokenFire_CancelsRunningFactoryThroughTile` verifies claimant cancellation reaches a running coordinator-backed factory and releases the active slot within two seconds.

### Acceptance Criteria

- During fast scroll, cooperative tile generation stops at the next cancellation checkpoint after the tile leaves the viewport.
- CPU/wall-clock time for stale frame generation approaches zero (instead of full generation time per discarded tile).
- All existing tests continue to pass (cancellation checks are no-ops when token is not canceled).
- Injection test verifies cancellation is responsive.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs` | Add `CancellationToken` parameter to `GenerateMonochromeMipPixels`, `ApplyDetailsWithGdiPlus`, `GenerateNoisePixelsCore`, `ApplyMipDetails`; add `ThrowIfCancellationRequested()` checks |
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | Add token checks in native factory (lines 420-431), mip factory (lines 546-551) |
| `tests/InfiniteCanvas.Tests/SampleImageTileTests.cs` | Add injection test for mid-execution cancellation responsiveness |

## Validation

`dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ClaimantTokenFire_CancelsRunningFactoryThroughTile"`

## Notes

This ticket was previously blocked on ICW-P1-CLAIMANT-TOKENS (tokens were `CancellationToken.None` and cooperative checks would be inert). That dependency is now resolved — tokens flowing into factories are real, non-`None` tokens that can be canceled. This ticket's implementation will now have real, testable effect.

## Related Tasks

- ICW-P1-CLAIMANT-TOKENS: prerequisite (real tokens now flowing)
- ICW-P1-GDI-CONCURRENCY: the remaining GDI+ concurrency risk needs separate focused validation
