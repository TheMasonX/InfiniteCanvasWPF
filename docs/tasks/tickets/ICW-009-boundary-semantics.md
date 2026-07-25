---
id: ICW-009-boundary-semantics
key: ICW-009
title: Icw 009 Boundary Semantics
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

Different modules use closed vs half-open boundary semantics which can cause double-counting or missed items at tile boundaries. This ticket unifies the policy and adds tests to prevent regressions.

Acceptance criteria

- A canonical boundary policy is documented and implemented consistently.
- Unit tests assert consistent outcomes at boundaries.
