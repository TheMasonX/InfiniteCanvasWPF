---
status: draft
summary: Fix parameter attribution in `Bgra32BufferLayout.GetPixelOffset` guards
scope: |
  - Split compound Contains(x,y) guard into two explicit checks and throw `ArgumentOutOfRangeException` naming the offending parameter.
  - Audit similar patterns across rendering code.
files_to_change:
  - src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter Bgra32BufferLayout
next_step: |
  - Implement the guard fixes and add tests validating per-parameter exceptions.
---

Background

Current guard throws `ArgumentOutOfRangeException(nameof(x), ...)` even when `y` is invalid, making diagnostics confusing.

Acceptance criteria

- Out-of-range x throws exception naming `x`; out-of-range y names `y`.
- Tests validate behavior.
