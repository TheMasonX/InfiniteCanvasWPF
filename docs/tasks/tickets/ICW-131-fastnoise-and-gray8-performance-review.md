---
id: ICW-131
author: Copilot
key: ICW-131
title: Review FastNoise and Gray8 pixel-transfer optimization proposals
status: In Review
type: Spike
priority: P1
tags:
  - profiling
  - rendering
  - gray8
  - fastnoise2
  - benchmarks
dependsOn: []
related:
  - ICW-064
  - ICW-076
  - ICW-097
  - ICW-129
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - submodules/FastNoise2Bindings/CSharp/FastNoise2.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileMaterializationBenchmarks.Windows.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Review the proposed 8bpp/memcpy path and the Visual Studio Profiler agent recommendations against the current FastNoise2 and Gray8 rendering implementation. Preserve the current deterministic output and zero-copy/lazy-cache ownership model while separating measured opportunities from speculative optimizations.

## Scope

- Evaluate whether the current GDI+ circle overlay can be replaced by an 8bpp bitmap and direct copy without changing transparent-pixel handling or `Math.Min` blending.
- Verify the FastNoise2 wrapper contract, including span pinning, native min/max output, and managed call overhead.
- Compare reduced-resolution generation, direct mip generation, tile regeneration, and float-grid caching against the existing byte-budgeted materializer/cache.
- Keep benchmark evidence for any wrapper, SIMD, cache, or transfer change on the target Windows machine.

## Acceptance Criteria

- Document that a direct memcpy from the current `Format32bppArgb` buffer is not a correct drop-in: the source has four bytes per pixel, transparent pixels must be skipped, and opaque circle values are merged with the destination using minimum intensity.
- Document that synthetic backgrounds already generate directly into Gray8 `byte[]` payloads; an 8bpp transfer proposal must identify a remaining 32bpp allocation or copy before implementation is justified.
- Treat direct mip-level generation and resident/cache reuse as higher-value controls than wrapper micro-optimizations or an unbounded float-grid cache.
- Require repeated benchmark comparisons plus deterministic and visual regression tests before changing the generator or materializer contract.

## Validation

- Review evidence: `FastNoise.GenUniformGrid2D` accepts `Span<float>`, pins its first element for the native call, and returns two native min/max values through a small stack buffer.
- Existing validation: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`; `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`.
- Existing benchmark: `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*TileMaterializationBenchmarks*" --job Dry`.
- Tracker validation: `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`.

## Notes

The current Windows circle path allocates a `Format32bppArgb` intermediate because GDI+ provides anti-aliased ellipse rasterization there. Copying its pixels directly would copy transparent background data and would not apply the existing minimum-value blend. An 8bpp GDI+ bitmap is worth benchmarking only as a replacement rasterization target, not as a memcpy-only optimization; it may also have different drawing support and grayscale/palette semantics.

The profiler recommendation to reduce native sample count is directionally sound, but this repository already requests canonical lower-resolution mips directly and uses pooled float samples for each generation. Caching full float grids would add large memory pressure and duplicate the byte payload cache unless keyed, bounded, invalidated, and charged explicitly. The wrapper's debug length check and `OutputMinMax` construction are secondary candidates and should not be changed without a focused microbenchmark.

## Related Tasks

- ICW-064: byte-budgeted tile-cache admission and diagnostics
- ICW-076: source-agnostic background tile mip levels
- ICW-097: synthetic Gray8 generation and CPU pixel-processing optimization
- ICW-129: FastNoise2 background-noise migration
