---
status: proposed
title: Clarify SpatialBounds intersection semantics (inclusive vs exclusive)
repo-area: src/InfiniteCanvas.Core
severity: low
assignee: core-team
---

Summary:
`SpatialBounds.Intersects` currently treats touching edges as intersecting (inclusive). Make this contract explicit in docs and unit tests to avoid misunderstandings.

Scope:
- `src/InfiniteCanvas.Core/SpatialBounds.cs`

Acceptance criteria:
- Add XML docs describing `Intersects` semantics.
- Add unit tests covering boundary-touching cases.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter SpatialBounds`

Estimated effort: Small
Risk: Low
Suggested owner: @core-team
