Benchmark guidance and Windows benchmark notes

This document describes the recommended benchmark practices for InfiniteCanvasWPF and explains why the Windows-specific point-based benchmark was replaced.

Recommended practices

- Benchmarks should exercise the real shipped render path (tile generation + annotation composition) rather than legacy point-based primitives.
- Use BenchmarkDotNet stable config and produce CSV/HTML outputs stored under `BenchmarkDotNet.Artifacts/results`.
- Run Windows-specific benchmarks on a stable Windows runner with no other CPU load and consistent power profile.

Reproducibility

- Example run (Windows x64):

```powershell
dotnet run -c Release --project benchmarks/InfiniteCanvas.Benchmarks -f net10.0-windows
```

- Review outputs under `BenchmarkDotNet.Artifacts/results`.

Migration note

The legacy `ProjectionAndBitmapBenchmarks.Windows.cs` previously exercised a point-based `GenerateFrozenBitmap(IEnumerable<ScreenPoint>, ...)` overload not used by the shipping app. Replace that benchmark with a tile+annotation workload to measure realistic allocation/throughput behavior.
# Benchmarks Guide

This document describes how to run and interpret the project's benchmarks.

## Targets
- `net10.0` — cross-platform spatial indexing and snapshot benchmarks.
- `net10.0-windows` — Windows-only projection and zero-copy bitmap benchmarks (requires Windows and WPF support).

## Recommended commands
```powershell
# Cross-platform benchmarks
dotnet run -c Release --project benchmarks/InfiniteCanvas.Benchmarks -f net10.0

# Windows-only projection/bitmap benchmarks (Windows machines only)
dotnet run -c Release --project benchmarks/InfiniteCanvas.Benchmarks -f net10.0-windows
```

## Reproducibility
- Run on a quiescent machine; disable background heavy workloads.
- Use `--runtimes` and `--framework` flags as needed for multiple runtimes.
- Keep BIOS power settings on "High Performance" for consistent CPU frequency.

## Variance and Baselines
- Do not use benchmark results as CI pass/fail checks without a documented baseline policy.
- Capture CSV/HTML outputs under `BenchmarkDotNet.Artifacts/results` and archive them alongside a short description of the machine.

## Rendering matrix policy

`TileMaterializationBenchmarks` uses five warmup and iteration samples for each
parameter combination. It compares noise-only, circle-only, and full Gray8
generation at mip levels 0, 1, and 3. It also measures cold materialization,
warm reuse, and resident mip lookup.

`ProjectionAndBitmapBenchmarks` exercises the shipped tile compositor. It varies
sparse annotation composition and resident pixel state. The benchmark does not
use the legacy point-render overload.

Use `--job Dry` only to verify the harness. Use the stable default job for timing
comparisons. Record the machine CPU, Windows version, .NET runtime, build
configuration, git revision, and parameter table with each archived run.

`TileWorkCoordinatorBenchmarks` uses three warmup iterations and ten measured
iterations. Use the repeat-run script for ICW-144 evidence. A Dry run remains a
smoke check and does not support performance claims.

## Annotation overlay lifecycle

Run the Windows-only overlay lifecycle comparison after a Release build:

```powershell
dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*AnnotationOverlayPooling*"
```

`RecreateDetachedStates` allocates new WPF overlay elements after each detach.
`ReuseDetachedStates` reuses detached elements with the same `Children.Add` and
`Children.Remove` workload. The benchmark excludes raster generation and camera
projection. Use it to evaluate allocation and lifecycle pressure, not full-frame
latency.

The application writes `FrameDiag` and `AnnotationDiag` records at the same
two-second cadence. The app uses retained annotation visuals in every run.
`AnnotationDiag` reports overlay update time, equivalent-item fast-path hits,
state creation, pool reuse, pool size, and visual tree add/remove counts.

The app logs diagnostics to
`%LOCALAPPDATA%\InfiniteCanvas\logs\infinitecanvas-YYYYMMDD.log`.

