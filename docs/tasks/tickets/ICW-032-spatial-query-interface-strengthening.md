# ICW-032: Spatial Query Interface Strengthening

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Strengthen spatial-index abstraction to support count-only and lower-allocation query paths while preserving current behavior.

## Scope

- src/InfiniteCanvas.Spatial/ISpatialIndexService.cs
- src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs
- src/InfiniteCanvas.Spatial/StrTreeSpatialIndexService.cs
- src/InfiniteCanvas.Spatial/ImmutableSpatialIndexService.cs
- src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
- tests/InfiniteCanvas.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Current interface exposes only list-returning query, forcing materialization even when only counts are required.
- This limits optimization options for high-frequency viewport updates.

## Next Step

- Add a compatible count-oriented query contract and migrate count-only call sites first.
