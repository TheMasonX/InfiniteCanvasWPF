---
id: ICW-003-tilecachebudget-lru
key: ICW-003
title: Icw 003 Tilecachebudget Lru
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

Eviction currently relies on `Dictionary.Values.FirstOrDefault()` which yields an implementation-dependent choice. Implementing an explicit LRU gives deterministic eviction and O(1) operations.

Acceptance criteria

- `TileCacheBudget` maintains LRU order; eviction removes least-recently-used tile.
- Unit tests validate eviction order and performance under pressure.
