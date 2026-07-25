---
id: ICW-014-global-exception-safety-net
key: ICW-014
title: Global UI exception safety net and harden async-void handlers
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

Summary:
Add global unhandled exception handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) and harden `async void` event handlers so UI exceptions are logged and do not crash the process silently.

Scope:
- `src/InfiniteCanvas.App/App.xaml`
- `src/InfiniteCanvas.App/App.xaml.cs`
- selected `async void` handlers in `MainWindow.xaml.cs`

Acceptance criteria:
- App registers global exception handlers and logs exceptions to `StatusText` or telemetry.
- Long-running `async void` handlers use `SafeFireAndForget` or wrap awaits in try/catch.

Validation commands:
- `dotnet build ./InfiniteCanvasWPF.slnx --configuration Release`
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter CoalescingAsyncActionTests`

Estimated effort: Small
Risk: Low
Suggested owner: @app-team
# ICW-014: Global Exception Safety Net for Async UI Pipeline

- Status: In Progress
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Add application-level unhandled exception handling so async-void UI event failures are surfaced, logged, and handled with user-safe behavior.

## Scope

- src/InfiniteCanvas.App/App.xaml
- src/InfiniteCanvas.App/App.xaml.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: the app initially lacked global Dispatcher/AppDomain/TaskScheduler unhandled exception safety hooks; the current worktree now registers and removes all three hooks in `App.xaml.cs`.
- Coalesced render scheduling currently relies on fault-prone task propagation semantics, so unhandled render faults can surface through async-void event paths without centralized reporting.
- Application-level hooks are now registered in `App.OnStartup`; dispatcher faults are logged and marked handled, unobserved task faults are logged and observed, and process-level faults are logged as fatal. Remaining validation is selected async-void handler coverage and close-time lifecycle stress.

## Dependencies

- ICW-034 for render-coalescer-specific fault handling and follow-up request preservation.

## Next Step

- Harden selected `MainWindow` async-void handlers with a shared safe wrapper, then validate close-time cancellation and disposal paths.
