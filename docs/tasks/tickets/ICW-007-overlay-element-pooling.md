# ICW-007: Overlay Element Pooling

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Pool retained annotation overlay elements to reduce per-frame allocation churn and preserve smooth selection animation continuity.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.App/MainWindow.xaml
- tests/InfiniteCanvas.Windows.Tests
- docs/tasks/JIRA.md

## Validation

- Audit capture only in this pass.
- Implementation validation command (planned):
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- Overlay visuals are recreated each frame per visible annotation.
- Allocation pattern includes brushes, rectangles, containers, labels, and tooltips.
- Rebuild strategy can reset selection animation phase each frame during interaction.

## Next Step

- Implement retained element pool as the parent overlay slice, then deliver ICW-019 animation continuity and ICW-028 frame-shell retention in the same rollout to avoid duplicate rewrites.
