---
id: ticket-viewmodels-concurrency-and-rendering
key: ICW-999
title: Ticket Viewmodels Concurrency And Rendering
status: Proposed
type: Task
priority: P2
tags:
  - backlog
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Description
-----------

This ticket captures a small, high-impact set of fixes to harden the WPF application lifecycle and viewmodel-related rendering logic. Addressing these points prevents subtle deadlocks, access violations from disposing unmanaged image buffers prematurely, and high GC/alloc pressure from per-frame element construction.
