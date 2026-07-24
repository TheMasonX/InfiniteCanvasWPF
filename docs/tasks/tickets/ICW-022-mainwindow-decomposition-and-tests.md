# ICW-022: MainWindow Decomposition and Test Backfill

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Extract pure logic from `MainWindow` into testable units while preserving WPF thread boundaries and behavior.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.ViewModels
- src/InfiniteCanvas.Core
- tests/InfiniteCanvas.Tests

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: code-behind remains large with substantial pure logic that can be unit tested.

## Next Step

- Move zoom-floor, generation-parameter, and pixelometer helper math into focused classes and backfill tests.
