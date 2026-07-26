id: ICW-079-render-busy-state-coalescing
key: ICW-079
title: Make busy-state updates and render coalescing resilient to rapid input churn
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

# ICW-079 - Make busy-state updates and render coalescing resilient to rapid input churn

## Summary

The current render request path increments and decrements a busy-operation counter around every frame request. Rapid mouse movement, tile generation callbacks, and regeneration updates can trigger lots of near-simultaneous work, which makes the busy UI noisy and can leave the overlay state in an inconsistent transient state.

## Scope

- Review the render-request budget and busy-state bookkeeping for high-frequency input events.
- Make the busy indicator reflect the latest meaningful render window instead of every micro-request.
- Ensure the render coalescer and busy-state updates are resilient to reentrancy and shutdown timing.

## Acceptance Criteria

- High-frequency input events do not cause spurious busy-state oscillation or redundant frame work.
- The busy indicator remains accurate when many requests arrive in quick succession.
- The behavior is covered by focused regression tests or a deterministic integration scenario.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~Coalescing|FullyQualifiedName~MainWindow"`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending implementation.

## Notes

- This is narrower than the broader exception-safety work in ICW-014 and the lifecycle hardening in ICW-029, but it targets the current request-churn behavior in MainWindow directly.
- The current evidence is the repeated `RequestRenderAsync` usage from pointer movement, tile events, selection changes, and resize handling.

## Related Tasks

- ICW-014
- ICW-029
- ICW-034
