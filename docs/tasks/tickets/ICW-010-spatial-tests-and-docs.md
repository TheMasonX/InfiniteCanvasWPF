---
status: draft
summary: Add targeted concurrency and immutability tests for Spatial subsystem and link tests as verification artifacts in ADR-0003
scope: |
  - Add unit tests for STRtree immutability, `QueryCount` parity, and `LiveSpatialIndexService` publish/failure interleavings.
  - Link tests as verification artifacts in `docs/ADR/0003-live-hybrid-spatial-indexing.md`.
files_to_change:
  - tests/InfiniteCanvas.Tests/StrTreeSpatialIndexServiceTests.cs (new tests)
  - tests/InfiniteCanvas.Tests/LiveSpatialIndexServiceTests.cs (additional interleaving cases)
  - docs/ADR/0003-live-hybrid-spatial-indexing.md (links)
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter LiveSpatialIndexService|StrTree|QueryCount
next_step: |
  - Add tests, run them, and reference in ADR-0003 as verification artifacts.
---

Background

Tests currently do not fully exercise publish interleavings and immutability guarantees. Adding these tests will make future refactors safer and provide evidence during code reviews.

Acceptance criteria

- New tests added and passing locally.
- ADR-0003 references the tests by path as verification.
