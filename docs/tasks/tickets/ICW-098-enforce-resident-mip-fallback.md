---
id: ICW-098
status: todo
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

Problem: `DrawTile` only attempts resident-payload lookup when `shouldGeneratePixels` is true, which causes placeholders to be painted even when an older resident mip is available.

Evidence:
- "DrawTile only attempts resident-payload lookup when `shouldGeneratePixels` is true." (docs/audits/icw-delta-audit-26-07-25-04-20-00.md)

Recommendation: decouple generation eligibility from fallback selection. Always select the best resident payload for rendering before deciding whether to queue generation.

Validation command:
```
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter EnforceResidentMipFallbackTests
```
