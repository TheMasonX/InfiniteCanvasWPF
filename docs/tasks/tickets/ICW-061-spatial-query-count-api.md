---
id: ICW-061
author: Copilot
key: ICW-061
title: Strengthen spatial query API with count/streaming paths
status: Proposed
type: Task
priority: P2
tags:
  - spatial
  - api
  - performance
dependsOn: []
related: []
links:
  - src/InfiniteCanvas.Spatial/
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Summary
-------

Add a low-allocation/count-oriented query contract to `ISpatialIndexService<T>` so callers that only need a count (or that can iterate without full materialization) can avoid large allocations and copies.

Background / Motivation
-----------------------

Currently `ISpatialIndexService<T>.Query(SpatialBounds)` returns an `IReadOnlyList<T>` and forces materialization of results. Several consumers only need counts (`.Count`) or perform streaming operations, which causes unnecessary allocation and prevents index implementations from returning lower-cost results.

Proposed Change
---------------

- Add a new API to `ISpatialIndexService<T>`:
  - `int QueryCount(SpatialBounds viewport);`
  - Optionally: `IEnumerable<T> QueryStream(SpatialBounds viewport);` as a follow-up.
- Implement `QueryCount` in existing implementations (`ImmutableSpatialIndexService`, `StrTreeSpatialIndexService`, `LiveSpatialIndexService`) to use index-native fast paths where available.
- Migrate count-only call sites (e.g. `CanvasViewportViewModel`) to use `QueryCount`.

Risk Level
----------

Medium — API change requires updating implementations and call sites. Can be introduced as additive API (non-breaking) but requires careful migration.

Validation Commands
-------------------

```powershell
dotnet build InfiniteCanvasWPF.slnx --configuration Release
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter QueryCount
```

Minimal Tests To Add / Update
-----------------------------

- Add `ISpatialIndexService_QueryCount_ReturnsCorrectCount` tests for:
  - `ImmutableSpatialIndexService` (small arrays, edge bounds)
  - `StrTreeSpatialIndexService` (verify using STRtree fast-count when available)
  - `LiveSpatialIndexService` (cover Snapshot/Hot/Publishing combinations)
- Update `CanvasViewportViewModelTests` to call `QueryCount` in count-oriented assertions.

Notes
-----

Keep the existing `Query` API and add `QueryCount` as an additive optimization to preserve compatibility.
