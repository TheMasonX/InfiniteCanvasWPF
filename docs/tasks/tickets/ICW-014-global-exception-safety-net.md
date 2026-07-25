---
id: ICW-014-global-exception-safety-net
key: ICW-014
title: Global UI exception safety net and harden async-void handlers
status: In Progress
type: Task
priority: P2
tags:
  - icw
  - task-tracker
  - stability
  - logging
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-014: Global Exception Safety Net for Async UI Pipeline

## Summary

Add application-level unhandled-exception handling so async-void UI event failures are surfaced, logged, and handled without crashing the process silently.

## Scope

- src/InfiniteCanvas.App/App.xaml.cs
- src/InfiniteCanvas.App/Logging/SerilogHost.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests

## Acceptance Criteria

- The app registers global Dispatcher/AppDomain/TaskScheduler exception handlers and logs failures centrally.
- Logging initialization degrades gracefully if the Windows Event Log sink cannot be created.
- Selected async-void handlers remain safe under unexpected faults.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Succeeded with 1 existing nullable warning in the renderer path.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~SampleImageTileTests|FullyQualifiedName~CanvasUserSettingsTests|FullyQualifiedName~CoalescingAsyncActionTests"`
- Result: Passed (9/9 tests, 0 failures)

## Findings

- The current worktree now registers and removes all three global handlers in App startup/shutdown.
- The logging host now falls back to file-only logging if the Event Log sink cannot be initialized, which closes the new first-launch crash path identified in the audit.
- Remaining work is to harden selected MainWindow async-void handlers with a shared safer wrapper and to validate close-time cancellation paths.

## Dependencies

- ICW-034 for render-coalescer-specific fault handling and follow-up request preservation.

## Next Step

- Add a shared async-void wrapper for the main UI handlers and confirm no close-time disposal regressions.
