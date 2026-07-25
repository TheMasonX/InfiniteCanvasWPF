# Task Tracking

This folder contains lightweight markdown-based task tracking for the repository when a richer task system is not available.

## Conventions

- Use docs/tasks/active-tasks.md as the current backlog and status board.
- Create a ticket file under docs/tasks/tickets/ for larger or multi-step work.
- Capture every user requirement, bug report, or task note immediately in a task entry or ticket so it is not lost.
- If a note materially affects architecture or project direction, add or update an ADR in docs/ADR/ and reference it from the task entry.
- If a note captures recurring functional behavior or product invariants that should survive future refactors, add it to docs/requirements/functional-requirements-and-invariants.md and reference that registry from the task entry.
- Each task should capture:
  - status
  - summary
  - scope
  - validation command
  - findings or blockers
  - next step

## Recommended workflow

1. Review the relevant design notes, ADRs, and current task list.
2. Add or update a task entry before making changes.
3. Record the validation command and its result once the work is done.
4. Leave enough detail that another agent can resume the work quickly.
