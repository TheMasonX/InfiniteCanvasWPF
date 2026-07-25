---
id: ICW-022-mainwindow-decomposition-and-tests
key: ICW
title: Icw 022 Mainwindow Decomposition And Tests
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-022-mainwindow-decomposition-and-tests

## Summary

- Status: To Do
- Objective: reduce MainWindow from a monolithic shell into a composition of smaller, testable views and presenters while preserving current behavior.

## Scope

- Extract the viewport host, settings sidebar, feature inspector, and footer/status area into dedicated subcontrols or user controls.
- Move pure interaction and presentation logic out of MainWindow code-behind where practical, especially zoom, pixelometer, generation input validation, and selection/tooltip formatting.
- Consolidate repeated visual patterns into reusable styles/templates so the growing XAML surface becomes more maintainable.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- MainWindow becomes a thin shell that composes a small set of focused views or controls.
- The viewport and settings interactions are backed by small presenter/controller classes or view-models rather than being embedded directly in the window code-behind.
- Repeated UI patterns (section headers, button groups, panel spacing, slider labels) use shared styles/templates.
- Pure logic for zoom, generation validation, and pixelometer/view-state handling is unit-testable without instantiating the full window.
- The validation command and outcome are recorded.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: To be completed when implemented.

## Notes

- This is the primary backlog home for the current MainWindow extraction work.
- The current evidence is the large single-window composition in MainWindow.xaml and the mixed viewport/render/settings logic in MainWindow.xaml.cs.
- The style-consolidation and subcontrol-extraction work should be treated as part of the same effort rather than as separate tickets unless a narrower slice becomes worthwhile later.

## Related Tasks

- ICW-080
- ICW-037
