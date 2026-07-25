---
id: ICW-007-querycount-api
key: ICW-007
title: Icw 007 Querycount Api
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

Several callers only require the count of matching items. Materializing full lists allocates and copies memory unnecessarily. Adding `QueryCount` allows index implementations to provide a lower-cost path.

Acceptance criteria

- `ISpatialIndexService<T>` exposes `QueryCount`.
- Implementations return correct counts and unit tests validate parity with `Query(...).Count`.
