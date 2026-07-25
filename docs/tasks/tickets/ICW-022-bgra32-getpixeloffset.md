---
id: ICW-022-bgra32-getpixeloffset
key: ICW-022
title: Icw 022 Bgra32 Getpixeloffset
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

Problem
-------
`GetPixelOffset(int x, int y)` throws `ArgumentOutOfRangeException` but attributes the exception to the `x` parameter even when `y` is invalid. See [src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs](src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs#L29).

Root cause
----------
Compound guards that check both `x` and `y` but only name a single parameter in the thrown exception, leading to unclear diagnostics.

Proposed change
---------------
Split validation into two checks and throw with the correct parameter name. Apply the same pattern across similar methods.

Risk level
----------
Low — purely defensive text changes in exception attribution.

Validation commands
-------------------
```
dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --filter GetPixelOffset
```

Tests to add
------------
- Unit test asserting `ArgumentOutOfRangeException` references the correct parameter when `x` is valid and `y` is invalid and vice versa.
