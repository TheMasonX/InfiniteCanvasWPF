---
status: draft
summary: Add `QueryCount` API to `ISpatialIndexService<T>` and implement low-allocation count path
scope: |
  - Add `int QueryCount(SpatialBounds viewport)` to `ISpatialIndexService<T>` as an additive API.
  - Implement `QueryCount` in existing index services (ImmutableSpatialIndexService, StrTreeSpatialIndexService, LiveSpatialIndexService).
  - Migrate count-only call sites to use `QueryCount`.
  - Add unit tests for `QueryCount` correctness and low-allocation expectations.
files_to_change:
  - src/InfiniteCanvas.Spatial/ISpatialIndexService.cs
  - src/InfiniteCanvas.Spatial/ImmutableSpatialIndexService.cs
  - src/InfiniteCanvas.Spatial/StrTreeSpatialIndexService.cs
  - src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs
  - src/InfiniteCanvas.ViewModels/* (migrate callers)
validation_command: |
  dotnet build InfiniteCanvasWPF.slnx --configuration Release
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter QueryCount
next_step: |
  - Add `QueryCount` definition and implement efficient count-only paths where possible.
  - Update consumers and add unit tests verifying counts match materialized queries.
---

Background

Several callers only require the count of matching items. Materializing full lists allocates and copies memory unnecessarily. Adding `QueryCount` allows index implementations to provide a lower-cost path.

Acceptance criteria

- `ISpatialIndexService<T>` exposes `QueryCount`.
- Implementations return correct counts and unit tests validate parity with `Query(...).Count`.
