# InfiniteCanvasWPF

A WPF inspection canvas for large monochrome image tiles and spatially indexed defect annotations.
The sample scene generates a configurable grid of deterministic `8192x2048` Gray8 images with separate
grayscale bitmap defects, class-colored bounding boxes, selectable class-or-ID labels, metadata tooltips,
tile-grid overlay, and animated selection outlines.

Architecture baseline for a high-scale infinite canvas engine targeting .NET 10 and WPF.

## Solution layout

- `src/InfiniteCanvas.App` - runnable WPF MVP with live data, pan, zoom, and resize handling
- `src/InfiniteCanvas.Core` - spatial primitives and shared contracts
- `src/InfiniteCanvas.Rendering` - renderer abstractions and the Windows zero-copy bitmap surface
- `src/InfiniteCanvas.Spatial` - pluggable spatial index contracts plus immutable/live hybrid implementations
- `src/InfiniteCanvas.ViewModels` - MVVM-friendly view models using CommunityToolkit.Mvvm
- `tests/InfiniteCanvas.Tests` - focused NUnit coverage for live updates and snapshot publication
- `tests/InfiniteCanvas.Windows.Tests` - Windows-only WPF interop and bitmap lifetime coverage
- `benchmarks/InfiniteCanvas.Benchmarks` - BenchmarkDotNet suites for spatial queries, rebuilds, and frame generation

## Run the MVP

The demo starts with a deterministic 2-by-32 inspection scene containing 64 lazily generated
monochrome image tiles and indexed defect annotations. Use the side panel to regenerate the
scene, tune tile and defect generation, choose annotation display options, and inspect the
selected annotation's feature values. Drag with the left mouse button to pan, use the right
mouse button for anchored panning, and use the mouse wheel or zoom presets to navigate.

```shell
dotnet run --project src/InfiniteCanvas.App/InfiniteCanvas.App.csproj
```

## Implemented design pillars

### Spatial indexing and live data

`ISpatialIndexService<T>` keeps consumers independent from the indexing algorithm. The supplied
`StrTreeSpatialIndexService<T>` uses NetTopologySuite's immutable packed STR-tree, while
`LinearSpatialIndexBuilder<T>` remains available as a simple fallback and testing implementation.

`LiveSpatialIndexService<T>` decorates any builder with the hybrid model used by the spatial
contracts and view-model tests:

- immutable published snapshot for stable reads
- hot buffer for incoming items
- one atomically published state containing the snapshot, publishing batch, and hot buffer
- non-blocking queries that merge those sources without losing or duplicating items
- asynchronous snapshot publication through a pluggable `ISpatialIndexBuilder<T>`

The shipped inspection scene builds its annotation index once per regeneration and uses the
same query contracts for viewport culling and selection. This keeps dynamic R-tree,
uniform-grid, directionally weighted binning, or GPU-backed implementations replaceable
without changing consumers.

### Projection

`CameraTransform` is UI-independent and uses atomic immutable state. It supports panning,
uniform or non-uniform zoom, configured scale limits, world-to-screen projection, and inverse
viewport calculation.

### Rendering

`InfiniteCanvas.Rendering` multi-targets `net10.0` and `net10.0-windows`. The Windows target
provides `ZeroCopyBitmapFactory`, which owns a Kernel32 file mapping, writes BGRA32 pixels
directly into unmanaged memory, and returns a frozen `InteropBitmap`. The factory must remain
alive while WPF uses its bitmap and must be disposed after the bitmap is removed from the
visual tree. The cross-platform target exposes the rendering contracts and validated buffer
layout so core logic and tests do not depend on WPF.

### MVVM

`CanvasViewportViewModel<T>` uses CommunityToolkit.Mvvm source generators. Its refresh
command is asynchronous, runs spatial queries away from the caller thread, exposes
`IsRunning` through `IAsyncRelayCommand`, and publishes only lightweight state changes.

## Validation

```shell
dotnet build InfiniteCanvasWPF.slnx --configuration Release
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release
```

## Benchmarks

Build benchmarks in Release mode, then select the target and suite explicitly. The cross-platform
target includes STR-tree queries, live-buffer queries, and snapshot rebuilds. The Windows target
also includes world-to-screen projection and zero-copy bitmap generation.

```shell
dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release
dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0 --no-build -- --filter "*StrTreeQueryBenchmarks*"
dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*ProjectionAndBitmapBenchmarks*"
dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileMaterializationBenchmarks*"
```

Use `--list flat` to list suites and `--job Dry` for a quick harness smoke test. A full run includes
10-million-record cases and can require substantial time and memory. Benchmark artifacts are ignored,
and benchmark timing is not enforced as a unit-test or CI pass/fail threshold.
