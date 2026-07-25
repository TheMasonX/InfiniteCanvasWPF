# Task Tracking

This folder contains lightweight markdown-based task tracking for the repository when a richer task system is not available.

## Conventions

- Use docs/tasks/active-tasks.md as the current backlog and status board.
- Create a ticket file under docs/tasks/tickets/ for larger or multi-step work.
- Capture every user requirement, bug report, or task note immediately in a task entry or ticket so it is not lost.
- If a note materially affects architecture or project direction, add or update an ADR in docs/ADR/ and reference it from the task entry.
- If a note captures recurring functional behavior or product invariants that should survive future refactors, add it to docs/requirements/functional-requirements-and-invariants.md and reference that registry from the task entry.
- Keep the task files machine-searchable with a stable frontmatter block and consistent section names.
- Each task should capture:
  - status
  - summary
  - scope
  - acceptance criteria
  - validation command
  - findings or blockers
  - next step

## Task schema

- The canonical schema lives in docs/tasks/TASK_SCHEMA.md.
- A starter template is available at docs/tasks/templates/task-template.md.
- The validation script is scripts/Validate-TaskTracker.ps1.

## Recommended workflow

1. Review the relevant design notes, ADRs, and current task list.
2. Add or update a task entry before making changes.
3. Record the validation command and its result once the work is done.
4. Leave enough detail that another agent can resume the work quickly.
5. Validate the tracker files with the task validation script when you add or revise tasks.

## Suggested task shape

Use a consistent structure like this for each task:

```md
---
id: ICW-999
author: Copilot
key: ICW-999
title: Short descriptive task title
status: Proposed
type: Task
priority: P2
tags:
  - rendering
  - ui
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

## Summary

## Scope

## Acceptance Criteria

## Validation

## Notes

## Related Tasks
```

## Validation command

Run the following from the repository root:

```powershell
pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks
```
