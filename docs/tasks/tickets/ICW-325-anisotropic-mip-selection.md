---
id: ICW-325-anisotropic-mip-selection
author: InfiniteCanvas Agent
key: ICW-325
title: Fix anisotropic mip-level selection under non-uniform scale
status: Done
type: Bug
priority: P2
tags:
  - rendering
  - mip
  - camera
  - anisotropic
related:
  - ICW-076
  - ADR-0005
links:
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-325 — Fix anisotropic mip-level selection under non-uniform scale

## Summary

Audit synthesis finding F-011. `SelectMipLevel` uses `Math.Min(ScaleX, ScaleY)` (`BackgroundTileContracts.cs:175-176`). ADR-0005 requires the coarsest mip whose texel density stays at or above one texel per screen pixel on both axes. With texel density `1/(Scale * 2^L)` per axis, the binding axis is the larger scale, so `Math.Min` under-resolves the zoomed-in axis in any real anisotropic state (ICW-011 axis-clamped zoom).

## Scope

- Decide the selection rule against ADR-0005: use `Math.Max(ScaleX, ScaleY)`, per-axis LOD, or an explicit anisotropic sampling decision.
- Do not leave the `Math.Min` behavior undocumented.
- Add a non-uniform-camera unit test asserting the zoomed-in axis is not under-resolved relative to the finest available mip.

## Acceptance Criteria

- `SelectMipLevel` never returns a level coarser than `floor(log2(1/max(ScaleX, ScaleY)))` for the requested camera.
- A non-uniform-camera test passes in a real supported anisotropic state.
- ADR-0005 alignment is recorded in the ticket.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "Mip|BackgroundTile"`
- Command: visual regression of an anisotropic zoom state (this changes which payload is sampled)

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackgroundTileMipPolicy"` passes, 2/2.
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore` passes, 198/198.
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-restore` passes, 28/28.
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore` passes with the existing `_frameClaimantId` warning.
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks` passes.
- `git diff --check` passes.

## Notes

- The policy now uses `Math.Max(camera.ScaleX, camera.ScaleY)` because the larger scale has the highest texel density.
- A visual anisotropic zoom regression remains useful when Windows runtime review is available.

## Wave Y Review

ADR-0005 defines the binding axis as the larger camera scale. The current implementation uses
the smaller scale, so an anisotropic camera can select a mip that is too coarse for the zoomed-in
axis. This wave changes only the pure mip policy and its regression coverage. It does not touch
the concurrent settings and property-editor work.

## Related Tasks

- ICW-076 (background tile mip levels)
- ADR-0005 (source-agnostic background tile mips)
