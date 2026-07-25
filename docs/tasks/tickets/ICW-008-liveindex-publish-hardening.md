---
id: ICW-008-liveindex-publish-hardening
key: ICW-008
title: Icw 008 Liveindex Publish Hardening
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

CAS-based state swaps are used to publish hot buffers; failure recovery currently uses current state during restore which can cause lost/duplicated items under interleavings. This ticket ensures deterministic restores.

Acceptance criteria

- Publish failure recovery uses captured publishingState to compute restored HotItems.
- Tests covering interleavings pass and document expected behavior.
