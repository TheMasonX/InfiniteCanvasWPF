---
id: ICW-305-tilecache-eviction-policy
key: ICW-305
title: Make TileCache eviction policy explicit (LRU or documented policy)
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
`TileCacheBudget.TrackTile` currently evicts dictionary-first entries which is unpredictable. Implement a clear eviction strategy (LRU or LFU) or document the current behavior and its invariants.

Scope:
- `src/InfiniteCanvas.Rendering/SampleImageTile.cs` (or `TileCache` related classes)

Acceptance criteria:
- Implement LRU eviction or another chosen policy with tests, or document current behavior and add a warning to callers.
- Add benchmark to show cache hit-rate improvement if LRU implemented.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter TileCache`
- `dotnet run -c Release -p benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj -- --filter TileMaterializationBenchmarks`

Estimated effort: Small-Medium
Risk: Low
Suggested owner: @rendering-team
