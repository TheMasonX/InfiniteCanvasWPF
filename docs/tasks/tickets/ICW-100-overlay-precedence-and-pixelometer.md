---
id: ICW-100
key: ICW-100
status: To Do
title: Define overlay precedence and align pixelometer sampling with rendered mip
type: Task
priority: P2
tags: [rendering, pixelometer, contract]
summary: Define overlay precedence and align pixelometer sampling with rendered mip
scope:
  - src/InfiniteCanvas.Rendering/DefectOverlaySampler.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Tests
owner: TBD
estimate: 4d
labels: [rendering, pixelometer, contract]
---

Problems:
- `DefectOverlaySampler.ResolveDisplayValue` is last-wins and depends on enumeration order from spatial backends.
- Pixelometer samples native tile payloads while the renderer may show a lower-res resident mip, causing mismatch between on-screen pixels and reported values.

Quoted evidence:
- "`DefectOverlaySampler.ResolveDisplayValue(IEnumerable<SampleAnnotation>)` is a simple last-wins fold." (docs/audits/icw-delta-audit-26-07-25-04-20-00.md)
- "The pixelometer can report a value from the full-resolution source even while the renderer is showing a lower-res resident mip or a placeholder fallback." (docs/audits/icw-delta-audit-26-07-25-04-20-00.md)

Recommendations:
- Define explicit overlay precedence (e.g., `z-index`, `max-severity`, `first-hit`) and implement it in `DefectOverlaySampler`.
- Update the pixelometer to sample the same resident mip used for rendering, or clearly label the pixelometer as "native source sample" and expose that in the UI.

Validation command:
```
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter OverlayPrecedenceAndPixelometerContractTests
```
