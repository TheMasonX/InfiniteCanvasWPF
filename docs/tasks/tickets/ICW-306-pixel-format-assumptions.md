---
status: proposed
title: Harden pixel-format and stride handling in defect/mask rendering
repo-area: src/InfiniteCanvas.Rendering
severity: medium
assignee: rendering-team
---

Summary:
Rendering code assumes specific pixel formats (e.g., Format24bppRgb) and stride layouts when reading `DefectBitmap` data. This should be made robust or explicitly validated/documented.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`
- `src/InfiniteCanvas.Rendering/SampleImageTile.cs`

Acceptance criteria:
- Validate incoming annotation bitmap pixel formats and throw clear exceptions for unsupported formats, or support common formats via conversion.
- Add unit tests for non-24bpp inputs verifying correct conversions or clear failure messages.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --filter ZeroCopyBitmapFactoryTests`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
