---
status: draft
summary: Reduce allocation churn by using ArrayPool for tile pixel buffers
scope: |
  - Use `ArrayPool<byte>.Shared` to rent pixel buffers during generation.
  - Store rented buffer inside tile and return it to the pool in `ResetImageCache()` (and on eviction).
  - Ensure buffer lifetime is safe when exposing Pixels property; possibly wrap pooled buffer so callers aren't surprised.
files_to_change:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Windows.Tests/* (add micro-benchmark / allocation assertion)
validation_command: |
  dotnet build -c Release
  dotnet test tests/InfiniteCanvas.Windows.Tests/ --filter "SampleImageTile*" -c Release
  run benchmark: benchmarks\\InfiniteCanvas.Benchmarks\\InfiniteCanvas.Benchmarks.csproj (or a micro benchmark) to measure allocations before/after
next_step: |
  - Implement buffer renting and returning, add unit tests ensuring no leaks and validate with benchmark measuring managed allocations.
---

Background

Allocating new `byte[]` per tile generation causes allocation churn and LOH pressure. Reusing buffers via `ArrayPool<byte>` reduces GC pressure and improves throughput.

Acceptance criteria

- Tile generation uses rented buffers and returns them on eviction/ResetImageCache.
- Micro-benchmark shows reduced managed allocations during heavy tile regeneration.
