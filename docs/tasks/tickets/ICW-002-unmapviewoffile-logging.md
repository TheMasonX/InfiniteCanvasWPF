---
id: ICW-002-unmapviewoffile-logging
key: ICW-002
title: Icw 002 Unmapviewoffile Logging
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

UnmapViewOfFile returns a boolean that is currently ignored. On rare platform failures this can hide resource leaks. This ticket ensures failures are surfaced during development and logged in production.

Acceptance criteria

- `Dispose(bool)` checks the return value and logs `Marshal.GetLastWin32Error()` on failure.
- DEBUG builds throw `Win32Exception` to surface the failure during development runs.
- Unit/integration test added to cover logging path.
