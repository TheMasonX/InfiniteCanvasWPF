---
id: ICW-101
author: External Audit (Integration-1)
key: ICW-101
title: Make annotation tooltips safe and lazy
status: Done
type: Task
priority: P1
tags:
  - annotation
  - tooltip
  - lazy-ui
  - profiling
  - cleanup
  - safety
dependsOn: []
related:
  - ICW-031
  - ICW-111
  - ICW-004
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/AnnotationFeaturePresenter.cs
  - src/InfiniteCanvas.Rendering/DeferredAnnotationToolTip.cs
  - tests/InfiniteCanvas.Tests/AnnotationFeaturePresenterTests.cs
  - tests/InfiniteCanvas.Tests/AnnotationTooltipWiringTests.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-08-03
---

## Summary

The frame builder creates a WPF `ToolTip` for every visible annotation during every frame. The supplied profile reports about 8,192 annotation tooltips, about 3.98 percent inclusive CPU in `BuildFrameVisual`, and about 3.94 percent self CPU in `CreateAnnotationToolTip`.

Make tooltip construction lazy and retain the presenter as the single formatting path. The annotation visual must not allocate or format tooltip content until WPF requests the tooltip for a hovered annotation.

Currently both keys are always present because `AnnotationGenerator` always populates them, but this is an accident of the current data path, not a contract guarantee. `Features` is typed as `IReadOnlyDictionary<string,double>` with no schema enforcement.

## Profiler Evidence

The supplied profiler capture provides the baseline: 8,192 eagerly created tooltips, about 3.98 percent inclusive CPU in `BuildFrameVisual`, and about 3.94 percent self CPU in `CreateAnnotationToolTip`.

The new frame path assigns one `DeferredAnnotationToolTip` source per annotation. It does not create a WPF `ToolTip` or format presenter content during frame construction. WPF requests the source text when it opens the tooltip. The focused source regression test protects this allocation contract. A new runtime profiler capture remains useful for quantifying the post-change CPU reduction.

## Root Cause

The tooltip path was reverted to inline string-keyed access while the presenter API remained. The frame builder also eagerly creates one `ToolTip` per annotation instead of assigning a deferred content factory or equivalent WPF lazy source.

## Scope

### Required Changes

1. **Remove eager tooltip construction** from `BuildFrameVisual`. Store the annotation as the tooltip source or use a deferred WPF content path that creates the `ToolTip` only on demand.
2. **Route deferred content through `AnnotationFeaturePresenter.BuildTooltipContent`** and remove raw `Features["Confidence"]` and `Features["Severity"]` access.
3. **Add a behavioral test** asserting deferred tooltip content matches `BuildTooltipContent` output for a given annotation.
4. **Add a source or allocation regression check** proving frame construction does not create one tooltip per annotation.

### Acceptance Criteria

- Frame construction does not allocate or format tooltip content for annotations that are not hovered.
- Tooltip content is created on demand and matches `AnnotationFeaturePresenter.BuildTooltipContent`.
- A missing feature key produces a graceful empty or default value instead of `KeyNotFoundException`.
- Existing tooltip appearance is preserved (same formatting, same values).

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Remove eager tooltip allocation and use a deferred presenter-backed content path |
| `tests/InfiniteCanvas.Tests/AnnotationFeaturePresenterTests.cs` | Add tooltip content test |
| `tests/InfiniteCanvas.Windows.Tests/` | Add a focused WPF lazy-tooltip behavior test if the deferred source requires STA coverage |

## Validation

`dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~AnnotationFeaturePresenterTests|FullyQualifiedName~AnnotationTooltipWiringTests"` passed 4/4.

`dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-restore` passed 10/10.

`dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore` passed with the existing unused `_frameClaimantId` warning.

## Notes

The managed memory capture reports 8,192 `SampleAnnotation` instances and 8,192 feature dictionaries. Lazy tooltip creation targets the measured per-frame CPU cost first, while ICW-031 and ICW-111 remain responsible for the longer-term typed metrics migration.

## Notes

The post-change profiler percentage is not available in this environment. The recorded baseline remains valid evidence for the original defect, while the source and behavioral tests verify the intended lazy path.

## Related Tasks

- ICW-031 / ICW-111: typed annotation metrics (this fix is a stopgap that reduces crash risk before the typed migration)
