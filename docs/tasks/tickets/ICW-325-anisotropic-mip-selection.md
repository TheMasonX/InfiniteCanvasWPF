---
id: ICW-325-anisotropic-mip-selection
author: InfiniteCanvas Agent
key: ICW-325
title: Fix anisotropic mip-level selection under non-uniform scale
status: Proposed
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

## Notes

- Gate on an ADR-0005 alignment decision before changing behavior.
- Independent of the ICW-316 boundary work; proceeds in parallel.

## Related Tasks

- ICW-076 (background tile mip levels)
- ADR-0005 (source-agnostic background tile mips)
