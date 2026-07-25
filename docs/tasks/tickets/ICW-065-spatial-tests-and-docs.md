---
id: ICW-065
author: Copilot
key: ICW-065
title: Add targeted concurrency and immutability tests for Spatial subsystem
status: Proposed
type: Task
priority: P2
tags:
  - spatial
  - tests
  - docs
dependsOn: []
related: []
links:
  - tests/InfiniteCanvas.Tests/
  - docs/ADR/0003-live-hybrid-spatial-indexing.md
created: 2026-07-25
updated: 2026-07-25
---

Summary
-------

Add focused tests that exercise concurrency interleavings, immutability guarantees, and published snapshot contracts for `LiveSpatialIndexService`, `StrTreeSpatialIndexService`, and `ImmutableSpatialIndexService`.

Proposed Change
---------------

- Add tests covering:
  - `StrTree` query immutability (returned list independent of internal structures)
  - `LiveSpatialIndexService` add/publish interleavings and failure recovery
  - `QueryCount` correctness after API addition (ICW-061)
- Update `docs/ADR/0003-live-hybrid-spatial-indexing.md` to reference the new tests as verification artifacts.

Risk Level
----------

Low — focused tests only.

Validation Commands
-------------------

```powershell
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter LiveSpatialIndexService
```

Minimal Tests
-------------

- `StrTreeSpatialIndexService_Query_ReturnsImmutableSnapshot`
- `LiveSpatialIndexService_PublishFailureRestoresHotBuffer`
- `ImmutableSpatialIndexService_QueryCount_MatchesQueryLength`
