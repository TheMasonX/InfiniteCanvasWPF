---
id: ICW-023-unmapviewoffile
key: ICW-023
title: Icw 023 Unmapviewoffile
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
`UnmapViewOfFile` return value is ignored in `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs` which can silently swallow failures during Dispose. See code at [src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs](src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs#L248).

Root cause
----------
Inconsistent error handling of Win32 interop calls: constructor checks errors but `Dispose` discards the boolean result.

Scope
-----
- Update `Dispose(bool)` implementation to check `UnmapViewOfFile` return value and log `Marshal.GetLastWin32Error()` on failure.
- Add a unit test that simulates an unmap failure path if possible (integration test or platform shim).

Validation
----------
Run:

```
dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --filter UnmapViewOfFile
```

Notes
-----
This is low impact; we should avoid throwing from `Dispose` but surface a logged diagnostic.

Next steps
----------
1. Implement error-checking and logging in `Dispose(bool)`.
2. Add test or integration harness that validates a logged warning when `UnmapViewOfFile` fails.
