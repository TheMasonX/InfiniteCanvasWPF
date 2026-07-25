# ICW-034: Coalescing Render Fault Handling And Follow-Up Preservation

- Status: Done
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

- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - Passed: 35 tests, 0 failures.
- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release --filter FullyQualifiedName~CoalescingAsyncActionTests`
  - Passed: 4 tests, 0 failures.
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - Succeeded.

## Findings

- `ProcessAsync` executes `await _action(...)` with no catch policy, so non-cancellation faults bubble through the shared processing task.
- A queued `_requested = true` follow-up can be discarded if `_action` throws before the loop reevaluates pending work.
- `DisposeAsync` can rethrow stale non-cancellation failures by awaiting an already-faulted processing task during window close.

## Outcome

- `CoalescingAsyncAction` now reports non-cancellation action failures through an optional callback instead of faulting its shared processing task.
- A request coalesced while a failing action is in flight is processed by the next scheduler iteration.
- Lifetime cancellation still propagates so active callers observe the expected canceled task during disposal.
