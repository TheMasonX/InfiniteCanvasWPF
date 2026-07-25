---
id: ICW-103-defectbitmap-concurrency
key: ICW-103
title: Protect DefectBitmap GDI+ usage from concurrent mutation/dispose during background render
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
`DrawDefectPatch` locks bits of `DefectBitmap` without guarding against concurrent `Dispose` or mutation; `System.Drawing.Bitmap` is not thread-safe and this can surface as GDI+ exceptions or memory corruption.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`
- Annotation data producers in the app view-model layers

Acceptance criteria:
- Add synchronization or immutable-copy semantics for annotation bitmaps so `DrawDefectPatch` cannot lock disposed bitmaps.
- Add concurrent tests that update/dispose `DefectBitmap` while rendering and assert no exceptions and proper cleanup.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter FullyQualifiedName~ZeroCopyBitmapFactoryTests`
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Estimated effort: Small
Risk: Small
Suggested owner: @annotations-owner
