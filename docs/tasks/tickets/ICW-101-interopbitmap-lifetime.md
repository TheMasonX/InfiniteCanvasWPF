---
id: ICW-101-interopbitmap-lifetime
key: ICW-101
title: Ensure ZeroCopyBitmapFactory memory mapping remains valid while WPF/compositor uses InteropBitmap
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
`ZeroCopyBitmapFactory` can unmap or dispose its underlying file-mapping while previously returned `InteropBitmap` instances remain referenced by WPF or the compositor. This can cause AccessViolationException, corrupted frames, or compositor crashes.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`
- UI call sites that receive `InteropBitmap` (e.g., `src/InfiniteCanvas.App/MainWindow.xaml.cs`)

Acceptance criteria:
- The backing mapping is guaranteed alive while any `InteropBitmap` produced by the factory may be referenced.
- Either: returned bitmaps carry an owning handle/wrapper that pins the mapping, or callers are prevented from disposing the factory until bitmaps are released.
- Add deterministic smoke test that produces an `InteropBitmap`, sets it as `Image.Source`, then requests factory disposal; the app must not crash and no AccessViolationException should be observed.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter FullyQualifiedName~ZeroCopyBitmapFactoryTests`
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Estimated effort: Medium
Risk: Medium
Suggested owner: @rendering-team-lead
