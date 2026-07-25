---
id: ICW-063
author: Copilot
key: ICW-063
title: Harden LiveSpatialIndexService publish semantics and failure recovery
status: Proposed
type: Task
priority: P2
tags:
  - spatial
  - concurrency
  - tests
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

Review and tighten the publish flow in `LiveSpatialIndexService<T>` to ensure no items can be lost or duplicated during interleaved Add/Publish/Query operations, and to make failure recovery explicit and well-tested.

Findings / Root Cause
---------------------

- The publish flow uses CAS swaps and moves HotItems -> PublishingItems; on build failure the code attempts to restore the hot buffer. Edge cases exist when concurrent `Add` occurs during failure recovery and when `PublishSnapshotAsync` is re-entrant guarded only by an `int _publishInProgress` flag.

Proposed Change
---------------

- Add explicit unit tests covering interleavings: Add during publish, Add after publish failure, concurrent queries while publishing.
- Replace `int _publishInProgress` with `0/1` `Interlocked` flag and ensure callers get clearer status (e.g. `PublishSnapshotAsync` returns `Published`/`NoWork`/`Skipped` enum).
- Ensure failure recovery uses the captured publishingState to restore HotItems deterministically (avoid using current state which may have mutated).

Risk Level
----------

Medium — changes are concurrency-sensitive and must preserve lock-free read path and snapshot semantics.

Validation Commands
-------------------

```powershell
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter LiveSpatialIndexService
```

Minimal Tests
-------------

- `PublishSnapshotAsync_PromotesHotBufferWithoutDroppingNewItems` (existing) extended to assert correctness when build fails mid-way and new adds happen.
- New test: `PublishSnapshotAsync_FailureRestoresHotBufferAndPreservesAddsDuringRecovery`.
- New test: `PublishSnapshotAsync_IsSerialized` asserts only one publish proceeds at a time.
