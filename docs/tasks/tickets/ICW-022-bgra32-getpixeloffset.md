---
status: open
summary: Attribute correct parameter in `Bgra32BufferLayout.GetPixelOffset` and similar guards
assignee: TBD
labels: [bug, low-risk, defensive]
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
