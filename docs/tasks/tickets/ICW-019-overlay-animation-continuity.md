# ICW-019: Overlay Animation Continuity During Rerenders

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Maintain selected-annotation animation continuity across rerenders while integrating with overlay pooling goals.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - Visual verification during pan/zoom and resize interaction.

## Findings

- Cross-validated audit finding: frame-level element recreation restarts the selected outline animation clock each render.

## Next Step

- Persist selected overlay visuals or animation clocks independent of full-frame rebuild cadence.
