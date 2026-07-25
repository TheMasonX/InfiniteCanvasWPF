---
status: proposed
title: Protect DefectBitmap GDI+ usage from concurrent mutation/dispose during background render
repo-area: src/InfiniteCanvas.Rendering
severity: medium
assignee: annotations-owner
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
