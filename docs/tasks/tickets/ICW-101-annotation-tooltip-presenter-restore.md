---
id: ICW-101
author: External Audit (Integration-1)
key: ICW-101
title: Restore tooltip to use AnnotationFeaturePresenter.BuildTooltipContent
status: Proposed
type: Task
priority: P2
tags:
  - annotation
  - tooltip
  - cleanup
  - safety
dependsOn: []
related:
  - ICW-031
  - ICW-111
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/AnnotationFeaturePresenter.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-101 — Restore tooltip to use `AnnotationFeaturePresenter.BuildTooltipContent`

## Summary

**Audit finding (95% confidence):** `AnnotationFeaturePresenter.BuildTooltipContent` (lines 17-29) already exists, already uses `TryGetValue` (safe), and is already correct. `MainWindow.CreateAnnotationToolTip` (line 724-732) does not call it — it duplicates similar formatting using raw indexers (`annotation.Features["Confidence"]`), which throws `KeyNotFoundException` if either key is ever absent.

Currently both keys are always present because `AnnotationGenerator` always populates them, but this is an accident of the current data path, not a contract guarantee. `Features` is typed as `IReadOnlyDictionary<string,double>` with no schema enforcement.

## Root Cause

The tooltip path was reverted to inline string-keyed access while the presenter API remained. This is the cheapest fix in the audit report.

## Scope

### Required Changes

1. **Replace `CreateAnnotationToolTip` body** (lines 724-732 in `MainWindow.xaml.cs`) with a call to `AnnotationFeaturePresenter.BuildTooltipContent(annotation)`.
2. **Delete `CreateAnnotationToolTip` entirely** and call `AnnotationFeaturePresenter.BuildTooltipContent` directly at the one call site (`MainWindow.xaml.cs:552`).
3. **Add a behavioral test** asserting tooltip content matches `BuildTooltipContent` output for a given annotation.

### Acceptance Criteria

- Tooltip no longer uses raw `Features["Confidence"]`/`["Severity"]` indexers.
- A missing feature key produces a graceful empty or default value instead of `KeyNotFoundException`.
- Existing tooltip appearance is preserved (same formatting, same values).

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Replace `CreateAnnotationToolTip` body with presenter call, delete method if possible |
| `tests/InfiniteCanvas.Tests/AnnotationFeaturePresenterTests.cs` | Add tooltip content test |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "Tooltip|AnnotationFeaturePresenter"
```

## Related Tasks

- ICW-031 / ICW-111: typed annotation metrics (this fix is a stopgap that reduces crash risk before the typed migration)
