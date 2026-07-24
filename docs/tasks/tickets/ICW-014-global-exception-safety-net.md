# ICW-014: Global Exception Safety Net for Async UI Pipeline

- Status: To Do
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

- Cross-validated audit finding: the app currently has no global Dispatcher/AppDomain/TaskScheduler unhandled exception safety hooks.
- Coalesced render scheduling currently relies on fault-prone task propagation semantics, so unhandled render faults can surface through async-void event paths without centralized reporting.

## Dependencies

- ICW-034 for render-coalescer-specific fault handling and follow-up request preservation.

## Next Step

- Implement centralized exception handlers and define logging plus fail-safe user messaging policy, then validate no unhandled exceptions occur during close-time cancellation and disposal races.
