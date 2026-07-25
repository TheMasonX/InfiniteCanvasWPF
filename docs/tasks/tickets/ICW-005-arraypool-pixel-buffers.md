---
id: ICW-005-arraypool-pixel-buffers
key: ICW-005
title: Icw 005 Arraypool Pixel Buffers
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

Allocating new `byte[]` per tile generation causes allocation churn and LOH pressure. Reusing buffers via `ArrayPool<byte>` reduces GC pressure and improves throughput.

Acceptance criteria

- Tile generation uses rented buffers and returns them on eviction/ResetImageCache.
- Micro-benchmark shows reduced managed allocations during heavy tile regeneration.
