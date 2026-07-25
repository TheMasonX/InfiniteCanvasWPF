---
id: ICW-077-viewport-scrollbar-overlay-hardening
key: ICW-077
title: Harden viewport scrollbar overlay geometry and initialization state
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

# ICW-077 - Harden viewport scrollbar overlay geometry and initialization state

## Summary

The current viewport scrollbar path assumes the overlay tracks and thumbs are always present and fully initialized. Static analysis reports nullable/initialization hazards around the scrollbar geometry updates, which is a realistic source of intermittent UI breakage during resize, initial load, and teardown.

## Scope

- Review the scrollbar overlay lifecycle in MainWindow and make the track/thumb state explicit instead of relying on nullable state implicitly.
- Guard all geometry and layout calculations with a clear initialization contract and fallback behavior.
- Add regression coverage around layout/metrics calculations so scrollbar updates remain stable when the overlay is not yet measured or has been detached.

## Acceptance Criteria

- The scrollbar overlay path no longer depends on implicit nullable assumptions for track/thumb construction and update.
- Resize and initial-load flows do not enter a broken state when scrollbar overlay elements are missing or not yet measured.
- There is explicit test coverage for the guard path and for normal overlay metrics updates.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~ViewportScrollbar"`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- Related to the existing scrollbar work in ICW-065 but focuses on hardening the current implementation rather than adding new interaction behavior.
- The current risk is supported by the nullable and possible-null diagnostics around the scrollbar geometry update path in MainWindow.

## Related Tasks

- ICW-065
- ICW-022
