---
id: ICW-097
author: Copilot
key: ICW-097
title: Reduce CPU cost of synthetic pixel generation and manipulation
status: In Review
type: Improvement
priority: P1
tags:
  - profiling
  - cpu
  - pixel-generation
  - rendering
  - benchmarks
dependsOn: []
related:
  - ICW-004
  - ICW-050
  - ICW-064
  - ICW-076
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks
  - docs/profiler-results/Report20260725-1202.diagsession
created: 2026-07-25
updated: 2026-07-25
---

## Summary

The supplied CPU profile shows synthetic tile generation dominates the captured run. The profile's RGB-to-gray loop is an avoidable round trip: synthetic backgrounds are already grayscale, so Windows materialization must generate Gray8 pixels directly rather than create a 24bpp GDI+ bitmap and convert it pixel by pixel. Direct requested-mip generation remains the primary zoomed-out optimization; resident fallback is already in place. Only after those should deterministic random arithmetic, zero-noise fills, circle bounds, and repeated per-pixel projection arithmetic be tuned.

## Scope

- Add focused benchmark variants for the current generator and raster paths.
- Narrow random arithmetic to the smallest correct integer domain if benchmarks show a gain; do not assume a precomputed jitter sequence is an optimization because memory bandwidth may cost more than simple computation on modern hardware.
- Fast-path constant pixel fills and reduce repeated circle-bound and projection calculations.
- Compare allocation, wall time, and visual/determinism behavior before and after each change.
- Keep external color-image conversion separate from the synthetic source path; use direct Gray8 copies for indexed/grayscale sources when that provider is introduced.

## Acceptance Criteria

- Release benchmarks show measurable improvement for the changed hot path, or document why a candidate was rejected. The synthetic Windows path must not spend time in a full RGB-to-gray conversion.
- Generated output remains deterministic for the same inputs and retains sufficient visible jitter and defect-circle contrast.
- No new managed pixel-buffer duplication is introduced in the zero-copy path.
- The result is covered by focused tests and benchmark artifacts.

## Validation

Passed: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release` (6/6), `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` (62/62), and `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`. The focused `TileMaterializationBenchmarks` Dry run measured direct Gray8 block-stamped materialization at 23.131 ms / 32.25 MiB for one 8192x4096 tile and 78.370 ms / 129 MiB for four tiles. The single-iteration Dry job is a smoke measurement, not a statistically stable result.

## Notes

The requested low-fidelity random strategy is compatible with the synthetic scene, but precomputed jitter is explicitly a fallback hypothesis rather than the preferred design. Direct Gray8 generation retains the efficient deterministic 512x512 noise-block stamp and applies circles directly to its final Gray8 buffer. This eliminates the profile's 24bpp GDI+ allocation and scalar RGB-to-gray conversion without replacing it with an expensive per-pixel random generator. The scalar conversion shown in the supplied trace is not a candidate for SIMD because the synthetic producer eliminates the conversion entirely. The next performance decision remains ICW-076's variant-aware cache accounting and diagnostics; do not introduce precomputed jitter unless target-hardware benchmarks prove it wins. `Validate-TaskTracker.ps1` remains blocked by unrelated ICW-084 through ICW-092 ticket files that predate this change and omit the required `key` field.

## Related Tasks

- ICW-004 zoomed-out overdraw and inner-loop math spike
- ICW-050 deterministic thread-safe tile generation
- ICW-076 background tile mip levels