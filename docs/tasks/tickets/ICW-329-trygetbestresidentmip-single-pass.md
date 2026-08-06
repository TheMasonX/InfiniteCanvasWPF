---
id: ICW-329-trygetbestresidentmip-single-pass
author: InfiniteCanvas Agent
key: ICW-329
title: Replace TryGetBestResidentMip allocate-and-sort with a single-pass scan
status: Done
type: Improvement
priority: P2
tags:
  - rendering
  - pixelometer
  - allocation
  - performance
  - mip
dependsOn: []
related:
  - ICW-312
  - ICW-035
  - ICW-096
  - ADR-0005
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Tests/SampleImageTileTests.cs
  - docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-20-09-37.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-329 — Replace TryGetBestResidentMip allocate-and-sort with a single-pass scan

## Summary

Audit synthesis finding (C23, re-verified at HEAD c552830). `SampleImageTile.TryGetBestResidentMip` builds a `List<(int MipLevel, byte[] Pixels)>` and runs `.OrderBy(...).ThenBy(...).FirstOrDefault(...)` while holding `_cacheGate`. The ICW-312 clean pixelometer path (`TryGetResidentPixels`) falls through to this method whenever the exact requested mip is not resident — the most likely case during active pan and zoom. The council rejected the original C23 as "stale"; the code was unchanged and still reachable from the intended long-term pixelometer path.

## Scope

- Replace the list allocation and LINQ sort with a single pass over `_mipPixels` (bounded by `BackgroundTileMipPolicy.MaxMipLevel`) tracking the best candidate by absolute mip distance, preferring the higher-resolution mip at equal distance (preserve the current `ThenBy` ascending tiebreak).
- No behavior change to the selected result.

## Acceptance Criteria

- `TryGetBestResidentMip` performs no list allocation and no sort.
- Selection parity test passes: same result as the current OrderBy/ThenBy semantics across a populated mip dictionary, including the equal-distance tiebreak.
- Pixelometer read path (`TryGetResidentPixels`) behavior is unchanged.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "SampleImageTile"`
- New test: `ResidentRead_EqualDistance_PrefersHigherResolutionMip` (equal-distance tiebreak parity).
- Existing `ResidentMipFallback_PrefersClosestResidentMipOverNativeLevelZero` and `ResidentRead_PrefersClosestResidentMip_WithoutStartingGeneration` cover closest-distance selection and the clean pixelometer path.
- Result: core 183/183, Windows 22/22, solution Release build 0 errors.

## Notes

- Delivered in Wave I (2026-08-06).
- Same spirit as the series' other allocation-reduction recommendations. Mip-selection semantics are unchanged.

## Related Tasks

- ICW-312 (data source abstraction, Done)
- ICW-035 (renderer/pixelometer blend contract)
- ADR-0005 (source-agnostic background tile mips)
