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
  - benchmarks/InfiniteCanvas.Benchmarks/TileDrawBenchmarks.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileMaterializationBenchmarks.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs
  - tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
  - docs/tasks/tickets/ICW-097-cpu-pixel-processing-optimization.md
  - docs/tasks/tickets/ICW-132-rendering-performance-stage-instrumentation.md
created: 2026-07-25
updated: 2026-08-04
---

## Summary

The supplied Visual Studio profiler capture makes `ZeroCopyBitmapFactory.DrawTile` the next rendering priority. The method accounts for about 20.09 percent inclusive CPU and 10.33 percent self CPU in the captured path, with repeated per-pixel scale division, clamping, and pixel-offset calculation.

Vectorize the pixel math only after preserving the current mip, placeholder, bounds, and Gray8-to-BGRA behavior. Keep the zero-copy destination contract and the non-blocking tile generation contract unchanged.

Implementation began with the benchmark harness. The harness measures the shipped tile-composition overload across the hot-loop changes.

## Scope

- Benchmark the current `DrawTile` and `DrawDefectPatch` paths with repeated Release runs on the target machine. Record width, height, mip level, visible pixel count, resident or placeholder state, allocations, and wall time.
- Add `TileDrawBenchmarks.Windows.cs` as the Phase 0 benchmark surface. Exercise native and nonzero mip scales with resident and placeholder tiles.
- Move invariant camera and tile calculations outside the inner loops. Prefer incremental source-coordinate or fixed-point stepping over repeated division when the result matches the current truncation and clamp semantics.
- Use a packed scalar BGRA store as the validated baseline for any later SIMD destination writer.
- Use an SSE2 four-pixel destination writer for contiguous grayscale output. Keep scalar source-coordinate lookup and scalar tail handling.
- Split placeholder rows from resident rows so placeholder output does not perform source-coordinate lookup.
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
  - `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows`
  - `dotnet build src/InfiniteCanvas.Rendering/InfiniteCanvas.Rendering.csproj --configuration Release --framework net10.0-windows`
  - `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~ZeroCopyBitmapFactoryTests"`
  - `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileDrawBenchmarks*" --job Dry --iterationCount 1 --warmupCount 0`
  - `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
  - `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileMaterializationBenchmarks*"`
- Result: Benchmark harness and rendering builds passed. `ZeroCopyBitmapFactoryTests` passed 12/12 after adding clamped-edge and resident-mip assertions. The repeated post-hoist Release run measured 7.467 to 9.933 ms. The packed-store run measured 6.536 to 9.514 ms. The SSE2 run measured 6.361 to 8.472 ms. The placeholder-path split now measures 1.442 to 1.818 ms for nonresident cases and 8.118 to 8.467 ms for resident cases. These runs provide matched evidence for the destination-write and placeholder slices, but stage diagnostics and a separate archived report remain open.

## Notes

The capture also reports `Bgra32BufferLayout.GetPixelOffset` at about 4.79 percent self CPU, `Math.Clamp` at about 0.41 percent, and `DrawDefectPatch` at about 3.07 percent inclusive CPU. The line-level trace shows the Y source-coordinate calculation at about 8.61 percent and the X calculation at about 2.73 percent. These values establish priority, not a guaranteed SIMD win.

The implementation sequence starts with a direct compositor benchmark. Safe invariant hoisting follows only after the benchmark builds and runs.

The scalar packed-store comparison uses the same benchmark matrix on the same Intel Core i5-6600K host. Placeholder cases moved from 7.467 to 7.717 ms down to 6.536 to 6.705 ms. Resident cases moved from 9.593 to 9.933 ms down to 9.241 to 9.514 ms. Treat these ranges as run observations, not a single aggregate percentage.

The SSE2 writer expands four grayscale values into sixteen BGRA bytes with one unaligned vector store. Source-coordinate calculation remains scalar to preserve truncation and clamp behavior. The SSE2 run measured 6.361 to 6.660 ms for placeholder cases and 8.124 to 8.472 ms for resident cases.

The profile showed four `GetTilePixelValue` calls at about 2.86 to 3.42 percent each. The placeholder split removes those calls when no source payload is resident. `CommunityToolkit.HighPerformance` is not required for this path because no managed collection crosses the rendering boundary. The remaining resident cost is scalar source-coordinate division and lookup. Evaluate exact source-run processing or SIMD coordinate math only after stage diagnostics identify the dominant portion.

The managed CPU capture reports `presentationframework.dll` at about 40.43 percent inclusive CPU and `MainWindow.RenderFrameAsync` at about 17.75 percent. The application hot path is therefore broader than `DrawTile`; use ICW-132 stage attribution before widening the vectorization scope.

The memory capture reports 512 `SampleImageTile` instances using about 154 MiB inclusive size, 8,192 `SampleAnnotation` instances using about 6.27 MiB inclusive size, and 8,192 feature dictionaries using about 1.77 MiB. Treat these as captured-run observations, not general heap limits.

## Related Tasks

- ICW-097: synthetic Gray8 generation and CPU pixel processing
- ICW-132: stage-level rendering performance instrumentation
- ICW-150: mip-aware background generation and cache accounting
