# InfiniteCanvasWPF

Architecture baseline for a high-scale infinite canvas engine targeting .NET 10 and WPF.

## Solution layout

- `src/InfiniteCanvas.App` - runnable WPF MVP with live data, pan, zoom, and resize handling
- `src/InfiniteCanvas.Core` - spatial primitives and shared contracts
- `src/InfiniteCanvas.Rendering` - renderer abstractions and the Windows zero-copy bitmap surface
- `src/InfiniteCanvas.Spatial` - pluggable spatial index contracts plus immutable/live hybrid implementations
- `src/InfiniteCanvas.ViewModels` - MVVM-friendly view models using CommunityToolkit.Mvvm
- `tests/InfiniteCanvas.Tests` - focused NUnit coverage for live updates and snapshot publication
- `tests/InfiniteCanvas.Windows.Tests` - Windows-only WPF interop and bitmap lifetime coverage

## Run the MVP

The demo starts with 100,000 deterministic spatial records, ingests 250 more every 500 ms,
and publishes the hot buffer into a packed STR snapshot every two seconds. Drag to pan and
use the mouse wheel to zoom.

```shell
dotnet run --project src/InfiniteCanvas.App/InfiniteCanvas.App.csproj
```

## Implemented design pillars

### Spatial indexing and live data

`ISpatialIndexService<T>` keeps consumers independent from the indexing algorithm. The supplied
`StrTreeSpatialIndexService<T>` uses NetTopologySuite's immutable packed STR-tree, while
`LinearSpatialIndexBuilder<T>` remains available as a simple fallback and testing implementation.

`LiveSpatialIndexService<T>` decorates any builder with the requested hybrid model:

- immutable published snapshot for stable reads
- hot buffer for incoming items
- one atomically published state containing the snapshot, publishing batch, and hot buffer
- non-blocking queries that merge those sources without losing or duplicating items
- asynchronous snapshot publication through a pluggable `ISpatialIndexBuilder<T>`

This permits dynamic R-tree, uniform-grid, directionally weighted binning, or GPU-backed
implementations without changing consumers.

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
