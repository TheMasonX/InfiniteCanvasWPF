# ICW-037: MainWindow Accessibility Baseline

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent
- Priority: P3

## Summary

Add foundational accessibility metadata and keyboard affordances to the MainWindow control surface so key workflows are screen-reader and keyboard friendly.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - Manual keyboard traversal and action check (zoom preset, custom zoom apply, regenerate, cache reset)

## Findings

- No `AutomationProperties.Name` metadata found on interactive controls.
- No keyboard shortcuts/access keys are defined for primary actions.
- No explicit key bindings exist for high-frequency workflows.

## Next Step

- Add automation names and minimally invasive keyboard access cues/bindings for primary controls while preserving current visual and interaction behavior.
