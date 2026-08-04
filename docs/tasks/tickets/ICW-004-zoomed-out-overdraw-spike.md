---
id: ICW-004-zoomed-out-overdraw-spike
author: Copilot
key: ICW-004
title: Vectorize zoomed-out tile pixel math and measure overdraw
status: In Progress
type: Improvement
priority: P1
tags:
  - profiling
  - rendering
  - pixel-math
  - vectorization
  - benchmarks
dependsOn: []
related:
  - ICW-097
  - ICW-132
  - ICW-150
links:
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileMaterializationBenchmarks.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs
  - tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
  - docs/tasks/tickets/ICW-097-cpu-pixel-processing-optimization.md
  - docs/tasks/tickets/ICW-132-rendering-performance-stage-instrumentation.md
created: 2026-07-25
updated: 2026-08-03
---

## Summary

The supplied Visual Studio profiler capture makes `ZeroCopyBitmapFactory.DrawTile` the next rendering priority. The method accounts for about 20.09 percent inclusive CPU and 10.33 percent self CPU in the captured path, with repeated per-pixel scale division, clamping, and pixel-offset calculation.

Vectorize the pixel math only after preserving the current mip, placeholder, bounds, and Gray8-to-BGRA behavior. Keep the zero-copy destination contract and the non-blocking tile generation contract unchanged.

## Scope

- Benchmark the current `DrawTile` and `DrawDefectPatch` paths with repeated Release runs on the target machine. Record width, height, mip level, visible pixel count, resident or placeholder state, allocations, and wall time.
- Move invariant camera and tile calculations outside the inner loops. Prefer incremental source-coordinate or fixed-point stepping over repeated division when the result matches the current truncation and clamp semantics.
- Evaluate `System.Numerics` or hardware intrinsics for contiguous pixel work. Use a scalar fallback when the row width, source stride, overlap rule, or platform does not support the vector path.
- Keep `DefectOverlaySampler.ResolveDisplayValue` semantics and annotation overlap ordering unchanged. Do not replace the compositor with a managed duplicate buffer.
- Add stage counters through ICW-132 so vector benchmarks distinguish projection setup, source lookup, overlay composition, and destination writes.

## Acceptance Criteria

- Release benchmark output shows a repeated before and after comparison for `DrawTile` at native and nonzero mip levels. Do not claim a percentage from a single Dry iteration.
- The optimized path produces byte-identical output for deterministic tile, camera, placeholder, edge-clamp, partial-visibility, and resident-mip cases covered by focused Windows tests.
- The vector path does not change source-coordinate truncation, half-open destination bounds, alpha writes, overlay max-wins behavior, or cache reservation behavior.
- The path introduces no managed per-pixel allocation or managed copy of the unmanaged destination buffer.
- The benchmark report records the selected vector width, hardware support, scalar fallback count, and stage timings.

## Validation

- Commands:
  - `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
  - `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileMaterializationBenchmarks*"`
- Result: Profiling evidence captured. Implementation and repeated before and after benchmark remain open.

## Notes

The capture also reports `Bgra32BufferLayout.GetPixelOffset` at about 4.79 percent self CPU, `Math.Clamp` at about 0.41 percent, and `DrawDefectPatch` at about 3.07 percent inclusive CPU. The line-level trace shows the Y source-coordinate calculation at about 8.61 percent and the X calculation at about 2.73 percent. These values establish priority, not a guaranteed SIMD win.

The managed CPU capture reports `presentationframework.dll` at about 40.43 percent inclusive CPU and `MainWindow.RenderFrameAsync` at about 17.75 percent. The application hot path is therefore broader than `DrawTile`; use ICW-132 stage attribution before widening the vectorization scope.

The memory capture reports 512 `SampleImageTile` instances using about 154 MiB inclusive size, 8,192 `SampleAnnotation` instances using about 6.27 MiB inclusive size, and 8,192 feature dictionaries using about 1.77 MiB. Treat these as captured-run observations, not general heap limits.

## Related Tasks

- ICW-097: synthetic Gray8 generation and CPU pixel processing
- ICW-132: stage-level rendering performance instrumentation
- ICW-150: mip-aware background generation and cache accounting
