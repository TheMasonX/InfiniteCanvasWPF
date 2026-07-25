---
id: ICW-064-tile-cache-capacity-and-materialization-metrics
author: Copilot
key: ICW-064
title: Bound lazy tile-cache admission without evicting visible tiles
status: Done
type: Bug
priority: P0
tags:
  - rendering
  - tile-cache
  - memory
  - regression
dependsOn: []
related:
  - ICW-047
  - ICW-049
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
created: 2026-07-25
updated: 2026-07-25
---

## Summary

Zoomed-out rendering can repeatedly generate then evict visible background tiles once the cache reaches its pixel budget. This creates a render-generation loop that never settles.

## Scope

- Change cache admission to use an estimated byte ceiling of 4 GiB.
- Pin tiles participating in the current rendered viewport so they cannot be evicted while their frame is being materialized.
- Refuse new lazy-generation requests when the cap is full of pinned tiles; render placeholders instead of scheduling work that cannot remain cached.
- Display byte capacity, byte usage, resident tile count, eviction count, and visible background-tile count.

## Acceptance Criteria

- A zoomed-out stable viewport does not continue queueing generations after the cache admission ceiling is reached.
- A visible tile is never evicted by cache-pressure processing during a frame that references it.
- Cache diagnostics report byte-based capacity and resident tile count.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: `SampleImageGeneratorTests` passed 13/13 and the Release WPF app build succeeded.

## Notes

- The cache is memoization, not an ownership boundary for tiles visible in the active frame.
- The 4 GiB default is an admission ceiling for cached Gray8 tile bytes, not a target allocation.
- Cache admission reserves space before background work starts. Eviction skips viewport-pinned and still-generating entries; a rejected reservation leaves the placeholder in place and a failed generation releases its reservation.

## Related Tasks

- ICW-047
- ICW-049
