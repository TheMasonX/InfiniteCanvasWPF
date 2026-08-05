---
id: ICW-308-spatialbounds-semantics
key: ICW-308
title: Clarify SpatialBounds intersection semantics (inclusive vs exclusive)
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

Summary:
`SpatialBounds.Intersects` currently treats touching edges as intersecting (inclusive). Make this contract explicit in docs and unit tests to avoid misunderstandings.

Audit synthesis (2026-08-04) widened this ticket: `TileGridIndexLookup` uses the opposite exclusive right/bottom convention, and the pixelometer performs the same half-open reads (findings C1-030 + C3-022). Document all three conventions and add edge/zero-area tests. Keep as documentation plus tests; no behavior change, to preserve spatial-index parity.

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
