---
id: ICW-068-about-and-licensing-dialog
key: ICW
title: Icw 068 About And Licensing Dialog
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

# ICW-068-about-and-licensing-dialog

## Summary

Add a small about/licensing entry to the main window that opens a lightweight attribution dialog for project information, licensing, and third-party credits.

## Status

Implemented

## Scope

- Update the main window header with a compact about button.
- Add a dialog that presents project attribution, MIT licensing context, and third-party credits.
- Verify the app still builds successfully.

## Acceptance Criteria

- The task has a clear implementation goal. Completed.
- The task is linked to the relevant files or design notes. Completed via this ticket and the main window implementation.
- The validation command and outcome are recorded. Completed.

## Validation

- Command: dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
- Result: Succeeded with 10 warnings.

## Notes

- Implemented in src/InfiniteCanvas.App/MainWindow.xaml, MainWindow.xaml.cs, and src/InfiniteCanvas.App/AboutDialog.cs.
- The dialog is intentionally lightweight and uses built-in WPF controls to avoid expanding the scope beyond the requested UI addition.

## Related Tasks

- ICW-000
