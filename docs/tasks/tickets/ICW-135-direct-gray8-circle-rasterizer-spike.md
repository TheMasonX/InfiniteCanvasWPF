---
id: ICW-135
author: Copilot
key: ICW-135
title: Benchmark a direct Gray8 anti-aliased circle rasterizer
status: Proposed
type: Spike
priority: P2
tags:
  - rendering
  - gray8
  - rasterization
  - benchmarks
dependsOn:
  - ICW-133
related:
  - ICW-097
  - ICW-131
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileMaterializationBenchmarks.Windows.cs
  - tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
  - tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Evaluate whether the Windows GDI+ `Format32bppArgb` circle intermediate can be replaced with a direct Gray8 anti-aliased rasterizer. This is the remaining plausible 8bpp opportunity; it is not a memcpy optimization and must preserve transparent coverage and minimum-value blending semantics.

## Scope

- Implement a benchmark-only candidate that writes coverage and grayscale values directly into the final Gray8 buffer.
- Compare it with the current GDI+ path across circle count, radius, mip level, dimensions, and clipped/offscreen circles.
- Define acceptable visual and deterministic differences before considering adoption.
- Measure intermediate allocations, CPU time, and final payload bytes.

## Acceptance Criteria

- Candidate output preserves the existing destination blend contract: transparent pixels leave the destination unchanged and covered pixels apply minimum intensity.
- Benchmark results show whether removing the 32bpp intermediate is material relative to native noise generation and normalization.
- Windows pixel tests cover center, edge coverage, clipping, overlap, and deterministic output.
- The candidate is rejected or adopted based on repeated non-Dry measurements, not on allocation reduction alone.

## Validation

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Run the ICW-133 benchmark matrix on the target Windows machine.

## Notes

The current GDI+ path is only used for the circle overlay in `SampleImageGenerator`; synthetic noise itself is already generated into Gray8. A direct rasterizer may improve allocation and copy behavior, but it could cost more CPU than GDI+ anti-aliasing, so visual parity and measured wall time are required.

## Related Tasks

- ICW-097: synthetic Gray8 generation and CPU pixel-processing optimization
- ICW-131: FastNoise and Gray8 performance review
- ICW-133: stage-isolated rendering benchmark matrix
