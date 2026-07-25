---
id: ICW-303-pinvoke-lifetime-safety
key: ICW-303
title: Harden P/Invoke and unmanaged memory usage in ZeroCopyBitmapFactory
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
P/Invoke usage (`DangerousGetHandle`, `MapViewOfFile`, `UnmapViewOfFile`) lacks defensive annotations and explicit SafeHandle pinning; this increases the risk of handle leaks, incorrect error reporting, and finalizer races.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`

Acceptance criteria:
- Ensure DllImport signatures use `SetLastError = true` where appropriate.
- Replace raw `DangerousGetHandle()` usage with a pattern that keeps the SafeHandle alive (e.g., `DangerousAddRef`/`DangerousRelease` or returning a wrapper that retains the SafeHandle until bitmap disposal).
- Add unit tests simulating concurrent create/dispose cycles without causing AVs.

Validation commands:
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- `git grep -n "DangerousGetHandle" || true`

Estimated effort: Small
Risk: Medium
Suggested owner: @rendering-team
