---
id: ICW-011-bgra32-getpixeloffset
key: ICW-011
title: Icw 011 Bgra32 Getpixeloffset
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

Current guard throws `ArgumentOutOfRangeException(nameof(x), ...)` even when `y` is invalid, making diagnostics confusing.

Acceptance criteria

- Out-of-range x throws exception naming `x`; out-of-range y names `y`.
- Tests validate behavior.
