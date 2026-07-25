---
id: ICW-201-live-spatial-deduplication
key: ICW-201
title: Live spatial query deduplication & Count consistency
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Summary:
Ensure `LiveSpatialIndexService` returns unique items per logical Id in `Query` and that `Count` reflects unique items (or document semantics clearly). Add tests covering concurrent add/publish scenarios.

Scope:
- `src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs`
- relevant tests in `tests/InfiniteCanvas.Tests`

Acceptance criteria:
- New unit tests assert that items present in both snapshot and hot buffers are deduplicated in `Query` results.
- `Count` either accurately reflects unique items or repository docs are updated to describe the aggregated semantics.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter LiveSpatialIndexServiceTests`
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

Estimated effort: Small
Risk: Low
Suggested owner: @spatial-team
