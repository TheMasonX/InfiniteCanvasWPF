---
status: draft
summary: Bound tile pixel generation concurrency to avoid threadpool saturation
scope: |
  - Add a shared bounded concurrency primitive (static `SemaphoreSlim`) to `SampleImageTile` generation path.
  - In `EnsurePixelsGenerationStarted`, acquire semaphore before running generation body and release in finally block.
  - Add a configuration point (internal constant or factory parameter) for maximum concurrent pixel generators (default Environment.ProcessorCount).
files_to_change:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
validation_command: |
  dotnet build -c Release
  dotnet test tests/InfiniteCanvas.Windows.Tests/ --filter "GenerateFrozenBitmap_GeneratesOnlyTilesWithVisiblePixels" -c Release
next_step: |
  - Implement bounded concurrency and add stress test simulating many concurrent tile generations; measure threadpool usage and generation latency.
---

Background

Per-tile `Task.Run` calls can create many concurrent tasks under large viewport changes. This ticket introduces a shared semaphore to limit concurrency and stabilize scheduling and memory use.

Acceptance criteria

- Pixel generation concurrency is bounded to a configurable value (default Environment.ProcessorCount).
- Stress test demonstrates bounded concurrency reduces scheduling spikes and remains correct.
