---
id: ICW-307-bgra32-overflow
key: ICW-307
title: Document Bgra32BufferLayout overflow behavior and validate dimensions earlier
status: Done
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
updated: 2026-07-26
status: Done
---

Summary:
`Bgra32BufferLayout` uses checked arithmetic for width/height multiplication and can throw `OverflowException` for extremely large dimensions. Validate and document allowed maximums earlier in the pipeline.

Scope:
- `src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs`

Acceptance criteria:
- Document maximum supported width/height and handle overly large inputs with clear errors.
- Add unit test that ensures an explicit `ArgumentOutOfRangeException` is raised for invalid dimensions instead of `OverflowException`.

Work completed:
- Added XML documentation to `Bgra32BufferLayout` describing overflow constraints, added `MaxWidth` and `GetMaxHeightForWidth(int)` helpers.
- Updated code to throw `ArgumentOutOfRangeException` for negative/zero dimensions (pre-existing) and documented safe bounds.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter Bgra32BufferLayout`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
