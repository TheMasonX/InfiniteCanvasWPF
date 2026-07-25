---
id: ICW-013-ci-and-nullable-enforcement
key: ICW-013
title: Icw 013 Ci And Nullable Enforcement
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

There is no repository-wide CI or nullable enforcement. Adding these reduces regressions and provides consistent build settings.

Acceptance criteria

- CI workflow builds and runs tests on PRs.
- `Directory.Build.props` exists with `Nullable` enabled and instructions for staged `TreatWarningsAsErrors` adoption.
