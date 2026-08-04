---
id: ICW-098
key: ICW-098
status: To Do
title: Enforce resident-mip fallback for rendering during mip transitions
type: Bug
priority: P2
tags: [rendering, cache, bugfix]
summary: Enforce resident-mip fallback for rendering during mip transitions
scope:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Tests
owner: TBD
estimate: 3d
labels: [rendering, cache, bugfix]
---

Problem: `DrawTile` only attempts resident-payload lookup when `shouldGeneratePixels` is true, which causes placeholders to be painted even when an older resident mip is available. The fallback also previously jumped straight to the native level-0 payload instead of preferring the nearest resident mip during transitions.

Evidence:
- "DrawTile only attempts resident-payload lookup when `shouldGeneratePixels` is true." (docs/audits/icw-delta-audit-26-07-25-04-20-00.md)
- The existing mip fallback logic selected resident payloads purely by absolute mip-distance and could still prefer native level 0 over a closer lower-level resident mip when both were available.

Recommendation: decouple generation eligibility from fallback selection. Always select the best resident payload for rendering before deciding whether to queue generation, and prefer the nearest resident mip with a small higher-resolution bias so transitions stay visually stable.

Validation command:
```
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter SampleImageTileTests
```
