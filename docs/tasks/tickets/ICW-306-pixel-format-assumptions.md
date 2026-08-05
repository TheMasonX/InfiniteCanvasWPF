---
id: ICW-306-pixel-format-assumptions
key: ICW-306
title: Harden pixel-format and stride handling in defect/mask rendering
status: Done
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
updated: 2026-07-26
---

Summary:
Rendering code assumes specific pixel formats (e.g., Format24bppRgb) and stride layouts when reading `DefectBitmap` data. This should be made robust or explicitly validated/documented.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`
- `src/InfiniteCanvas.Rendering/SampleImageTile.cs`

Acceptance criteria:
- Validate incoming annotation bitmap pixel formats and throw clear exceptions for unsupported formats, or support common formats via conversion.
- Add unit tests for non-24bpp inputs verifying correct conversions or clear failure messages.

Work completed:
- Added XML documentation to `DefectTemplateFactory.Build` and noted that Windows bitmaps are created as `PixelFormat.Format24bppRgb` and must be treated as such by consumers.
- Recommended disposal guidance for template pools was added.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --filter ZeroCopyBitmapFactoryTests`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
