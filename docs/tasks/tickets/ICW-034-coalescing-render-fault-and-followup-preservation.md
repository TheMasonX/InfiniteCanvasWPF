# ICW-034: Coalescing Render Fault Handling And Follow-Up Preservation

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent
- Priority: P1

## Summary

Harden `CoalescingAsyncAction` so render scheduling survives `_action` faults without dropping queued follow-up requests or rethrowing stale processing exceptions during disposal.

## Scope

- src/InfiniteCanvas.Core/CoalescingAsyncAction.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests/CoalescingAsyncActionTests.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- `ProcessAsync` executes `await _action(...)` with no catch policy, so non-cancellation faults bubble through the shared processing task.
- A queued `_requested = true` follow-up can be discarded if `_action` throws before the loop reevaluates pending work.
- `DisposeAsync` can rethrow stale non-cancellation failures by awaiting an already-faulted processing task during window close.

## Next Step

- Define explicit fault policy (surface/log + preserve or retry queued work), implement deterministic pending-request handling after failures, and add fault-path unit tests including dispose-time behavior.
