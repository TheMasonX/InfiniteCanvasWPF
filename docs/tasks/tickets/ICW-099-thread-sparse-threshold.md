---
id: ICW-099
key: ICW-099
status: To Do
title: Thread `MinimumSparseTilePixelSize` into render pipeline and UI
type: Improvement
priority: P3
tags: [ui, settings, rendering]
summary: Thread `MinimumSparseTilePixelSize` setting into render pipeline and UI
scope:
  - src/InfiniteCanvas.Core/CanvasUserSettings.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - tests/InfiniteCanvas.Tests
owner: TBD
estimate: 2d
labels: [ui, settings, rendering]
---

Problem: `CanvasUserSettings.MinimumSparseTilePixelSize` is persisted but not applied in the main render path; the XAML lacks a control to adjust it at runtime.

Evidence:
- `CanvasUserSettings.MinimumSparseTilePixelSize` exists but `MainWindow.RenderFrameAsync` does not pass the threshold to `ZeroCopyBitmapFactory.GenerateFrozenBitmap(...)`. (docs/audits/icw-next-slice-delta-audit-26-07-25-05-10-00.md)

Recommendation: pass the persisted threshold into the bitmap factory, add the missing UI control (or remove the dead setting), and add a test that asserts the round-trip persisted value affects the render decision path.

Validation command:
```
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter MinimumSparseTilePixelSizeTests
```
