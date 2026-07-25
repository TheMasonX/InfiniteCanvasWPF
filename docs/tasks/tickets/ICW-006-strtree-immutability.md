---
status: draft
summary: Enforce unconditional copy-to-array in StrTreeSpatialIndexService.Query and document immutability
scope: |
  - Always copy STRtree query results to an immutable snapshot (call `.ToArray()` unconditionally) before returning.
  - Document immutability expectation in `ISpatialIndexService` API contract.
  - Add a unit test proving returned collection is independent from subsequent index changes.
files_to_change:
  - src/InfiniteCanvas.Spatial/StrTreeSpatialIndexService.cs
  - src/InfiniteCanvas.Spatial/ISpatialIndexService.cs (XML docs)
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter StrTree
next_step: |
  - Implement unconditional copy in Query and add `StrTreeSpatialIndexService_Query_ReturnsIndependentSnapshot` test.
---

Background

NetTopologySuite's STRtree query returns a mutable `IList<T>`. To preserve immutability guarantees, the service should return an independent snapshot.

Acceptance criteria

- `StrTreeSpatialIndexService.Query` returns an array copy of results.
- Unit test asserts mutating the returned collection does not affect subsequent queries.
