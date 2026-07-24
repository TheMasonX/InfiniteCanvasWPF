# ICW-033: Boundary Semantics And Placement Consistency

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Unify coordinate-boundary semantics and remove annotation placement edge bias for deterministic and auditable spatial behavior.

## Scope

- src/InfiniteCanvas.Core/SpatialBounds.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- tests/InfiniteCanvas.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Placement math excludes rightmost/bottommost legal start positions because random upper bounds are exclusive.
- Intersection checks use closed bounds while sampling checks use half-open bounds.

## Next Step

- Define one boundary policy, apply consistently, and add edge-coordinate tests to lock expected behavior.
