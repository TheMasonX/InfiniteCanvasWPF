# Wave Q Rendering Benchmark Matrix Handoff

Status: Complete
Date: 2026-08-06
Commit target: ICW-133

## Summary

Wave Q replaces the old rendering smoke benchmarks with a stable matrix.
The matrix measures isolated generation and the shipped tile compositor.
Rendering diagnostics now exposes stage sample counts with stage durations.

## Critical review of Wave P

The prior Wave P diagnostics correctly recorded stage durations and per-mip outcomes.
The snapshot did not expose stage sample counts.
The old Windows matrix measured `SampleImageTile.Pixels` and a legacy point renderer.
It did not measure the shipped tile and sparse composition overload.
Wave Q fixes both gaps.

## Implementation

- `TileMaterializationBenchmarks` measures noise-only, circle-only, and full generation.
- The generation matrix covers tile widths 2048 and 8192.
- The generation matrix covers mip levels 0, 1, and 3.
- The tile matrix measures cold materialization, warm reuse, and resident mip reuse.
- `ProjectionAndBitmapBenchmarks` measures the shipped tile compositor.
- The compositor matrix varies sparse annotations and resident pixels.
- Stable benchmark attributes use two warmup samples and five iterations.
- The focused evidence run uses one warmup and three iterations.
- Dry runs remain harness checks only.

## Repeated evidence

The focused stage run executed 18 combinations in 39 seconds.
The focused compositor run executed 4 combinations.
BenchmarkDotNet produced these local artifacts:

- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.TileMaterializationBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.TileMaterializationBenchmarks-report.csv`
- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.TileMaterializationBenchmarks-report.html`
- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.ProjectionAndBitmapBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.ProjectionAndBitmapBenchmarks-report.csv`
- `BenchmarkDotNet.Artifacts/results/InfiniteCanvas.Benchmarks.ProjectionAndBitmapBenchmarks-report.html`

The most stable warm compositor means were 7.622 ms without sparse annotations and 8.615 ms with sparse annotations.
Cold placeholder means were 1.309 ms without sparse annotations and 2.208 ms with sparse annotations.
These values are baseline observations only.
The small mip workloads generated low-duration warnings.
Use more iterations before percentage claims.

## Machine metadata

- CPU: Intel Core i5-6600K CPU @ 3.50GHz
- OS: Windows 10 Pro 10.0.19045, x64
- Runtime: .NET 10.0.10
- SDK: 10.0.302
- BenchmarkDotNet: 0.15.8
- Git revision: `be0eca977dfb15a125440a5fb89a27a27dac9193`
- Configuration: Release, `net10.0-windows`
- Power profile: High performance

## Validation

- Focused diagnostics tests: 2 passed.
- Windows benchmark project build: passed with existing nullable warnings.
- Repeated stage matrix: passed, 18 combinations.
- Repeated shipped compositor matrix: passed, 4 combinations.
- `git diff --check`: passed.
- Full core, Windows, App, and task-tracker validation remain the final pre-push gates.

## Next step

Keep ICW-133 complete.
Repeat the stable matrix with more iterations on target hardware before accepting optimization percentage claims.
Use the stage sample counts and benchmark parameter table when comparing future rendering changes.
