# ICW-029: Shutdown Lifecycle Race Hardening

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Eliminate close-time race conditions between active generation/render operations and disposal of lifecycle primitives.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.App/App.xaml.cs
- tests/InfiniteCanvas.Windows.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`
  - Manual close-stress run while regenerate/render operations are active.

## Findings

- Regeneration acquires and releases `_generationGate` in `finally`, while `OnClosed` disposes the same semaphore without active-operation coordination.
- Close-time cancellation plus async-void handlers can convert disposal races into unhandled exceptions.
- Busy indicator transitions call `Dispatcher.Invoke` after increment/decrement updates; dispatcher teardown faults can leave counter state skewed during close-time races.

## Next Step

- Add explicit shutdown sequencing so regeneration/render completion or cancellation is awaited before disposing shared primitives.
