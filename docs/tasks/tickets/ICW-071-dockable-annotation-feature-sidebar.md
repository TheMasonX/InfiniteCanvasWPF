---
id: ICW-071-dockable-annotation-feature-sidebar
key: ICW-071
title: Move annotation features into a collapsible dockable sidebar
status: Proposed
type: Story
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

## Summary

Move selected-object feature inspection out of the display-options panel into a dedicated sidebar that can collapse and eventually dock within the canvas workspace.

## Scope

- Separate selected annotation feature presentation from generation and display controls.
- Add a collapsible sidebar shell with an empty selection state.
- Preserve current feature values and selection lifecycle.

## Acceptance Criteria

- Feature inspection is independently discoverable and can be collapsed without hiding display controls.
- The sidebar remains synchronized with selection and regeneration.

## Validation

- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- Full drag-and-drop docking is deferred; this task establishes the structural boundary.

## Related Tasks

- ICW-048
