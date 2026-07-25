---
status: proposed
title: Add count-only / streaming query API to ISpatialIndexService
repo-area: src/InfiniteCanvas.Spatial
severity: medium
assignee: spatial-team
---

Summary:
Add a non-allocating count and/or streaming query API to `ISpatialIndexService` to reduce allocations for high-frequency viewport checks.

Scope:
- `src/InfiniteCanvas.Spatial/ISpatialIndexService.cs`
- `src/InfiniteCanvas.Spatial/*` implementations
- `src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs` (call-site)

Acceptance criteria:
- API exposes at least one path to obtain a count without materializing full lists.
- Implementations provide efficient count or streaming behavior, or document limitations.
- `CanvasViewportViewModel` uses the count-only path where appropriate and benchmarks show reduced allocations.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Run benchmarks: `dotnet run -c Release -p benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj -- StrTreeQueryBenchmarks`

Estimated effort: Medium
Risk: Medium
Suggested owner: @spatial-team
