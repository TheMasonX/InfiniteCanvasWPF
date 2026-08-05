---
id: ICW-321-defectbitmap-dead-sampling-removal
author: InfiniteCanvas Agent
key: ICW-321
title: Remove dead DefectBitmap and LockBits sampling
status: Proposed
type: Bug
priority: P2
tags:
  - rendering
  - gdiplus
  - dead-code
  - concurrency
related:
  - ICW-102
links:
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/AnnotationGenerator.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-321 — Remove dead DefectBitmap and LockBits sampling

## Summary

Audit synthesis finding F-008. `DrawDefectPatch` locks a `DefectBitmap`, reads `sourceRow[sourceX * 3]`, and discards the value. The display value comes from `DefectPixels` via the sampler. The dead read adds a native-resource category and is the remaining surface of the dispose-vs-render race. Subsumes the unused `sourceRow[sourceX * 3]` read finding (C2-019).

## Scope

- `ZeroCopyBitmapFactory.Windows.cs` lines 337-380: remove `bitmap.LockBits`, the `sourceRow[sourceX * 3]` read, and `UnlockBits` from `DrawDefectPatch`. Keep the `DefectOverlaySampler.ResolveDisplayValue` output path.
- `SampleImageTile.cs` line 904: remove the `DefectBitmap` property.
- `AnnotationGenerator.cs` line 57: remove the `DefectBitmap` assignment.

## Acceptance Criteria

- `DrawDefectPatch` no longer touches `annotation.DefectBitmap`.
- Rendered defect output is byte-identical (display value comes from `DefectPixels` via the sampler).
- Grep shows zero remaining references to `DefectBitmap`.
- The defect-template pools still dispose their bitmaps.

## Validation

- Command: Windows render test asserting output pixels match pre-removal golden bytes
- Command: source-text assertion that `DrawDefectPatch` never calls `LockBits`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Notes

- Land before the ICW-102 rescope. Removal dissolves most of the dispose-race surface.
- Do not fold this into ICW-023; conflicting edits to the same region would result.

## Related Tasks

- ICW-102 (dispose fence rescope)
