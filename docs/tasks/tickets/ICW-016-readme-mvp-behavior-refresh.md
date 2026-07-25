---
id: ICW-016-readme-mvp-behavior-refresh
author: Copilot
key: ICW
title: Icw 016 Readme Mvp Behavior Refresh
status: Proposed
type: Task
priority: P2
tags:
  - task-tracker
  - icw
  - backlog
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-016-readme-mvp-behavior-refresh

## Summary

- Status: Done
- README now describes the current deterministic inspection-tile MVP and its controls.

- Status: To Do

## Scope

- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- The task has a clear implementation goal.
- The task is linked to the relevant files or design notes.
- The validation command and outcome are recorded.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: README reviewed against `MainWindow` startup and interaction paths.

## Notes

- Removed the obsolete periodic point-ingestion narrative and documented lazy tile generation,
  annotation inspection, pan, and zoom behavior.

## Related Tasks

- ICW-000
