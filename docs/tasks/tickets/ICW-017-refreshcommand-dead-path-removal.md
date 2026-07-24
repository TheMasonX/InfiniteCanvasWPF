# ICW-017: RefreshCommand Dead Path Removal or Rewire

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Decide whether `RefreshCommand` remains a supported app path; remove it if redundant or rewire it as the canonical flow.

## Scope

- src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: app flow currently uses `ApplyFrame`, while `RefreshCommand` is exercised only by tests.

## Next Step

- Choose one canonical presentation update path and delete redundant branch logic/tests.
