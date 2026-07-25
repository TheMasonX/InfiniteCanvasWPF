---
status: draft
summary: Harden LiveSpatialIndexService.PublishSnapshotAsync failure-recovery and interleavings
scope: |
  - Make failure recovery deterministic by using captured publishing state rather than current mutable state.
  - Make publish status observable and add a single-publish guard with clear semantics.
  - Add unit tests to exercise add-during-publish and failure-recovery interleavings.
files_to_change:
  - src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs
  - tests/InfiniteCanvas.Tests/LiveSpatialIndexServiceTests.cs
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter LiveSpatialIndexService
next_step: |
  - Implement deterministic recovery using publishing snapshot; add tests simulating failures.
---

Background

CAS-based state swaps are used to publish hot buffers; failure recovery currently uses current state during restore which can cause lost/duplicated items under interleavings. This ticket ensures deterministic restores.

Acceptance criteria

- Publish failure recovery uses captured publishingState to compute restored HotItems.
- Tests covering interleavings pass and document expected behavior.
