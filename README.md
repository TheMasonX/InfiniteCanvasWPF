# InfiniteCanvasWPF

Initial architecture baseline for a high-scale infinite canvas engine.

## Solution layout

- `src/InfiniteCanvas.Core` - spatial primitives and shared contracts
- `src/InfiniteCanvas.Rendering` - renderer abstractions decoupled from indexing
- `src/InfiniteCanvas.Spatial` - pluggable spatial index contracts plus immutable/live hybrid implementations
- `src/InfiniteCanvas.ViewModels` - MVVM-friendly view models using CommunityToolkit.Mvvm
- `tests/InfiniteCanvas.Tests` - focused NUnit coverage for live updates and snapshot publication

## Current live data approach

`LiveSpatialIndexService<T>` implements the requested hybrid model:

- immutable published snapshot for stable reads
- hot buffer for incoming items
- non-blocking queries that merge published, publishing, and pending items
- asynchronous snapshot publication through a pluggable `ISpatialIndexBuilder<T>`

The default builder is intentionally simple (`LinearSpatialIndexBuilder<T>`) so the architecture can evolve toward STR-tree, dynamic R-tree, uniform grid, or GPU-backed implementations without changing consumers.
