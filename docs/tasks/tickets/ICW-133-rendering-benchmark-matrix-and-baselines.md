---
id: ICW-133
author: Copilot
key: ICW-133
title: Build a stage-isolated rendering benchmark matrix
status: Done
type: Improvement
priority: P1
tags:
  - profiling
  - benchmarks
  - rendering
  - performance-baseline
dependsOn:
  - ICW-132
related:
  - ICW-097
  - ICW-131
links:
  - benchmarks/InfiniteCanvas.Benchmarks/TileMaterializationBenchmarks.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs
  - docs/benchmarks/BENCHMARKS.md
  - BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.TileMaterializationBenchmarks-report-github.md
created: 2026-07-26
updated: 2026-08-06
---

## Summary

Replace the current single-iteration Dry smoke measurements with a reproducible benchmark matrix that can distinguish native noise cost, normalization cost, circle cost, raster composition cost, and cache behavior. The existing 675.74 ms result for an 8192-wide tile is not a statistically stable before/after baseline. Wave P review also found that stage sample counts were not exposed by the diagnostics snapshot, so the benchmark evidence could not report sample volume.

## Scope

- Add benchmarks for native grid generation, pooled-buffer normalization, full Gray8 generation, circle application, `DrawTile`, and full tile-plus-annotation composition where practical.
- Cover native dimensions and canonical mip levels, zero-noise fills, noise-only output, circle-only output, cold generation, warm reuse, reset/regeneration, and resident-mip fallback.
- Use stable BenchmarkDotNet jobs with enough warmup and iteration samples for comparisons; retain a Dry job only as a harness smoke test.
- Expose stage sample counts with stage durations so diagnostics can be correlated with benchmark workloads.
- Archive CSV/HTML results with CPU, OS, runtime, build configuration, benchmark parameters, and git revision.

## Acceptance Criteria

- Every proposed optimization has a before/after benchmark at the same dimensions, mip, settings, and cache state.
- Results report mean, standard deviation/error, allocation, and sample count; one-iteration Dry results are explicitly excluded from percentage claims.
- The matrix identifies whether native generation or managed post-processing dominates for each workload.
- Benchmark coverage includes a realistic shipped tile path rather than only the legacy point overload.

## Validation

- `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release`
- `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileMaterializationBenchmarks*"`
- Archive output under `BenchmarkDotNet.Artifacts/results` and update `docs/benchmarks/BENCHMARKS.md` with the selected job policy.

## Notes

The current artifact reports 95.41 ms at 2048 and 675.74 ms at 8192 with `IterationCount=1`, `LaunchCount=1`, and `RunStrategy=ColdStart`. Those numbers are useful for scale sanity checks, but they cannot establish a percentage improvement or isolate the native call from the normalization loop. Wave P review confirmed that the old projection benchmark also used a legacy point overload outside the shipped path.

Wave Q completed the matrix and archived repeated evidence in `docs/handoffs/2026-08-06-wave-q-rendering-benchmark-matrix.md`. The run used three warmup and iteration samples through the stable benchmark attributes, with one warmup and three iterations for the focused capture. BenchmarkDotNet warned that small mip workloads run below 100 ms, so the results remain baseline evidence and do not support percentage claims.

## Related Tasks

- ICW-132: stage-level rendering performance instrumentation
- ICW-097: synthetic Gray8 generation and CPU pixel-processing optimization
- ICW-131: FastNoise and Gray8 performance review
