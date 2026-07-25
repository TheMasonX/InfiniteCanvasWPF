---
id: ICW-017-refreshcommand-dead-path-removal
author: Copilot
key: ICW
title: Icw 017 Refreshcommand Dead Path Removal
status: Done
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

# ICW-017-refreshcommand-dead-path-removal

## Summary

- Status: Done
- Removed dead `RefreshCommand`/`RefreshAsync` from `CanvasViewportViewModel` because production flow uses `ApplyFrame` exclusively.
- Replaced command-specific tests with `ApplyFrame` behavior tests for both live and non-live index implementations.

## Scope

- src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
- tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs

## Acceptance Criteria

- No unused refresh command path remains in the view model.
- Canonical `ApplyFrame` behavior is covered by tests.
- Validation command and outcome are recorded.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter FullyQualifiedName~CanvasViewportViewModelTests
- Result: Passed (see latest execution evidence in implementation batch).

## Notes

- If a future UX requirement introduces manual refresh, re-introduce a command only with a real UI binding and non-duplicative semantics.

## Related Tasks

- ICW-000
