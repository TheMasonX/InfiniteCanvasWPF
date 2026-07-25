---
id: ICW-004-bounded-pixel-generation
key: ICW-004
title: Icw 004 Bounded Pixel Generation
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

Per-tile `Task.Run` calls can create many concurrent tasks under large viewport changes. This ticket introduces a shared semaphore to limit concurrency and stabilize scheduling and memory use.

Acceptance criteria

- Pixel generation concurrency is bounded to a configurable value (default Environment.ProcessorCount).
- Stress test demonstrates bounded concurrency reduces scheduling spikes and remains correct.
