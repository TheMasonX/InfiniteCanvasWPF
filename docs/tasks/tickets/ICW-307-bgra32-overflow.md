---
status: proposed
title: Document Bgra32BufferLayout overflow behavior and validate dimensions earlier
repo-area: src/InfiniteCanvas.Rendering
severity: low
assignee: rendering-team
---

Summary:
`Bgra32BufferLayout` uses checked arithmetic for width/height multiplication and can throw `OverflowException` for extremely large dimensions. Validate and document allowed maximums earlier in the pipeline.

Scope:
- `src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs`

Acceptance criteria:
- Document maximum supported width/height and handle overly large inputs with clear errors.
- Add unit test that ensures an explicit `ArgumentOutOfRangeException` is raised for invalid dimensions instead of `OverflowException`.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter Bgra32BufferLayout`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
