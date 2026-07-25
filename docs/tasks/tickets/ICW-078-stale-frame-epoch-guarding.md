---
id: ICW-078-stale-frame-epoch-guarding
key: ICW-078
title: Guard render and regeneration paths against stale frame publication
status: Proposed
type: Task
priority: P1
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

# ICW-078 - Guard render and regeneration paths against stale frame publication

## Summary

The render pipeline currently publishes frames from a shared coalescer without any explicit epoch/version tracking for scene changes. A slow frame can still complete after a newer pan, zoom, or regeneration request and briefly display stale state.

## Scope

- Introduce a request or generation epoch that is attached to each render attempt and checked before a frame is published.
- Ensure stale render work is dropped cleanly rather than silently overwriting newer state.
- Cover the guard behavior with targeted unit or integration tests around render request ordering.

## Acceptance Criteria

- A slower earlier render cannot overwrite a later frame after a newer camera or scene state has already been requested.
- The UI remains consistent with the latest request even if older frame work is still finishing.
- The new guard behavior is covered by regression tests.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~Coalescing|FullyQualifiedName~Viewport"`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- This is distinct from the existing fault-handling work in ICW-034 because the concern here is stale output publication rather than exception containment.
- The current implementation already exposes multiple asynchronous entry points for render work in MainWindow and the coalescing action.

## Related Tasks

- ICW-014
- ICW-029
- ICW-034
