---
id: ICW-097
author: Copilot
key: ICW-097
title: Reduce CPU cost of synthetic pixel generation and manipulation
status: Proposed
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

The supplied CPU profile shows synthetic tile generation dominates the captured run. The primary optimization is to generate the requested mip directly from deterministic underlying noise rather than regenerate the full native image and reduce every intermediate level. The next priority is retaining resident data during asynchronous replacement. Only after those should deterministic random arithmetic, zero-noise fills, circle bounds, and repeated per-pixel coordinate arithmetic be tuned.

## Scope

- Add focused benchmark variants for the current generator and raster paths.
- Narrow random arithmetic to the smallest correct integer domain if benchmarks show a gain; do not assume a precomputed jitter sequence is an optimization because memory bandwidth may cost more than simple computation on modern hardware.
- Fast-path constant pixel fills and reduce repeated circle-bound and projection calculations.
- Compare allocation, wall time, and visual/determinism behavior before and after each change.

## Acceptance Criteria

- Release benchmarks show measurable improvement for the changed hot path, or document why a candidate was rejected.
- Generated output remains deterministic for the same inputs and retains sufficient visible jitter and defect-circle contrast.
- No new managed pixel-buffer duplication is introduced in the zero-copy path.
- The result is covered by focused tests and benchmark artifacts.

## Validation

Planned: targeted generator tests; `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release`; focused BenchmarkDotNet suites for generation and projection/raster composition.

## Notes

The requested low-fidelity random strategy is compatible with the synthetic scene, but precomputed jitter is explicitly a fallback hypothesis rather than the preferred design. Benchmark direct deterministic computation against any table-based approach on the target hardware, preserving tile/object seed offsets, reproducibility, and parallel isolation. The implementation order is direct requested-level generation first, resident-data fallback second, and general hot-path tuning third.

## Related Tasks

- ICW-004 zoomed-out overdraw and inner-loop math spike
- ICW-050 deterministic thread-safe tile generation
- ICW-076 background tile mip levels