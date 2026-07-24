# ICW-005: DPI-Aware Resize And Maximum Surface Policy

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Define and test explicit resize and max-render-surface policy for high-DPI and large monitor scenarios.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering
- tests/InfiniteCanvas.Windows.Tests
- docs/tasks/JIRA.md

## Validation

- Audit capture only in this pass.
- Implementation validation command (planned):
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Findings

- Max render dimension clamp literal appears in multiple call sites.
- Policy needs to cover per-monitor DPI behavior and 4K/5K display expectations.
- Audit recommends consolidating clamp to one named policy constant while implementing DPI behavior.

## Next Step

- Specify policy constants and resize behavior first, then add coverage for clamp and DPI scenarios.
