---
id: ICW-203-snapshot-cancellation
key: ICW-203
title: Make snapshot build cooperative with cancellation and avoid unbounded blocking
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
Ensure `PublishSnapshotAsync` honors cancellation tokens and that index builders are cooperative or cancellable to avoid long-running, uninterruptible snapshot builds.

Scope:
- `src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs`
- index builder implementations (e.g., `StrTreeSpatialIndexBuilder`)

Acceptance criteria:
- `PublishSnapshotAsync` responds quickly to cancellation in unit tests.
- Index builders accept or observe cancellation tokens, or the publish path is made incremental/cooperative.
- Tests demonstrate cancellation prevents long-running builder execution.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter PublishSnapshotAsync`

Estimated effort: Medium
Risk: Medium
Suggested owner: @spatial-team
