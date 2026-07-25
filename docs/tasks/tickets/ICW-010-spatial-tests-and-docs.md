---
id: ICW-010-spatial-tests-and-docs
key: ICW-010
title: Icw 010 Spatial Tests And Docs
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

Tests currently do not fully exercise publish interleavings and immutability guarantees. Adding these tests will make future refactors safer and provide evidence during code reviews.

Acceptance criteria

- New tests added and passing locally.
- ADR-0003 references the tests by path as verification.
