---
id: ICW-012-extract-withlockedbits-helper
key: ICW-012
title: Icw 012 Extract Withlockedbits Helper
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

Multiple codepaths lock and iterate bitmap bits manually. Extracting a helper reduces duplication and ensures correct UnlockBits usage on exceptions.

Acceptance criteria

- Helper exists and duplicated codepaths are replaced.
- Unit test validates UnlockBits is called on exception.
