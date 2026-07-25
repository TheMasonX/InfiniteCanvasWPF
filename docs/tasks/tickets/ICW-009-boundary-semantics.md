---
status: draft
summary: Unify SpatialBounds boundary semantics across index and renderer
scope: |
  - Define canonical boundary semantics (recommend closed intervals) in `SpatialBounds` XML docs and DesignDoc.md.
  - Update renderer sampling and index query call sites to adopt canonical semantics or convert explicitly.
  - Add edge-case unit tests verifying adjacency behavior at tile borders.
files_to_change:
  - src/InfiniteCanvas.Core/SpatialBounds.cs
  - src/InfiniteCanvas.Rendering/* (sampling code)
  - src/InfiniteCanvas.Spatial/* (query code)
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter Boundary
next_step: |
  - Document chosen semantics, update code paths or add conversion helpers, and add tests for boundary edge cases.
---

Background

Different modules use closed vs half-open boundary semantics which can cause double-counting or missed items at tile boundaries. This ticket unifies the policy and adds tests to prevent regressions.

Acceptance criteria

- A canonical boundary policy is documented and implemented consistently.
- Unit tests assert consistent outcomes at boundaries.
