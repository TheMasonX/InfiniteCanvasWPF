---
id: ICW-080-annotation-feature-presentation-model
key: ICW-080
title: Extract annotation feature formatting from MainWindow into a dedicated presentation model
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

# ICW-080 - Extract annotation feature formatting from MainWindow into a dedicated presentation model

## Summary

The selected-annotation feature panel and tooltip formatting live directly inside MainWindow and depend heavily on string-keyed access and ad-hoc formatting behavior. That makes the inspection surface hard to unit-test and easy to regress when metadata shape changes.

## Scope

- Move the formatting, fallback, and selection-view logic for annotation features out of MainWindow into a small presentation model or view-model.
- Keep the UI layer as a thin adapter for display and selection state.
- Add focused tests for formatting or fallback behavior so the inspection panel is not coupled to WPF event wiring.

## Acceptance Criteria

- Annotation feature formatting and selection logic are testable without instantiating the full window.
- MainWindow contains only thin UI wiring for feature display and tooltip presentation.
- The solution retains the current inspection UX while reducing UI coupling.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~Annotation|FullyQualifiedName~Feature"`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- This is adjacent to ICW-031 but narrower: it targets the presentation surface and testability of the current UI, not the underlying annotation-metadata model.
- The current evidence is the direct string-key feature access and formatting code in MainWindow.

## Related Tasks

- ICW-031
- ICW-048
