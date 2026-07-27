---
id: ICW-132
author: Copilot
key: ICW-132
title: Add stage-level rendering performance instrumentation
status: To Do
type: Improvement
priority: P1
tags:
  - profiling
  - rendering
  - diagnostics
  - benchmarks
dependsOn: []
related:
  - ICW-064
  - ICW-076
  - ICW-097
  - ICW-131
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Make profiling actionable by attributing rendering cost to native noise generation, float-to-Gray8 normalization, circle rasterization, tile projection/composition, cache misses, and cache hits. Current reports identify a hot method but cannot show which stage or cache state caused the wall time.

## Scope

- Add low-overhead stage timing and counters around `GenUniformGrid2D`, normalization, circle application, tile materialization, and raster composition.
- Record width, height, mip level, sample count, source/tile identity, cache state, resident payload bytes, and generation outcome in a structured snapshot or benchmark-only diagnostics surface.
- Keep instrumentation disabled or sampling-controlled in normal production rendering; do not add per-pixel logging.
- Make cold generation, warm cache hits, mip fallback, reservation rejection, and reset/regeneration distinguishable.

## Acceptance Criteria

- A benchmark or diagnostic run reports separate elapsed time for native generation, managed conversion, circle rasterization, and composition.
- Counters distinguish requested, generated, reused, rejected, failed, and evicted payloads by mip level.
- The diagnostic surface reports actual sample count and payload bytes, not only tile dimensions or a formatted total.
- Deterministic output and the current non-blocking render path remain unchanged.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Run the stage-attribution benchmark on a quiescent Windows machine and archive CSV/HTML output with hardware and runtime metadata.

## Notes

The profiler agent correctly identifies the full-grid native call and the second normalization pass, but method-level CPU attribution alone cannot justify caching or lower-resolution changes. Instrumentation must expose whether time is spent in cold generation, repeated generation after reset, mip transitions, or the final raster loop.

## Related Tasks

- ICW-064: byte-budgeted tile-cache admission and diagnostics
- ICW-076: source-agnostic background tile mip levels
- ICW-097: synthetic Gray8 generation and CPU pixel-processing optimization
- ICW-131: FastNoise and Gray8 performance review
