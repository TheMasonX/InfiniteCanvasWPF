---
id: ICW-006-strtree-immutability
key: ICW-006
title: Icw 006 Strtree Immutability
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

Background

NetTopologySuite's STRtree query returns a mutable `IList<T>`. To preserve immutability guarantees, the service should return an independent snapshot.

Acceptance criteria

- `StrTreeSpatialIndexService.Query` returns an array copy of results.
- Unit test asserts mutating the returned collection does not affect subsequent queries.
