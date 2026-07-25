---
id: ICW-062
key: ICW-062
title: Ensure StrTreeSpatialIndexService returns immutable query results
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

Summary
-------

NetTopologySuite's `STRtree<T>.Query(...)` returns a mutable `IList<T>` which is currently copied to an array in `StrTreeSpatialIndexService.Query`. Confirm and harden immutability guarantees and document expectations.

Proposed Change
---------------

- Ensure `StrTreeSpatialIndexService.Query` always returns an immutable snapshot (call `.ToArray()` unconditionally).
- Add a unit test asserting returned collection is independent of subsequent internal modifications.
- Document the immutability contract in `ISpatialIndexService`.

Root Cause
----------

STRtree's public API returns mutable lists; callers might accidentally mutate results if they assume immutability, or rely on behavior that later changes.

Risk Level
----------

Low — small change, safe to enforce immutability. Slight allocation overhead for small queries.

Validation Commands
-------------------

```powershell
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter StrTree
```

Minimal Tests
-------------

- `StrTreeSpatialIndexService_Query_ReturnsIndependentSnapshot`: create index, query, mutate returned list (attempt), verify underlying index query unchanged by next query.
