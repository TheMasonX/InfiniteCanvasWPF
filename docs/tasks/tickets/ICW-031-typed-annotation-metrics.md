---
id: ICW-031-typed-annotation-metrics
author: External Audit (Integration-1)
key: ICW-031
title: Replace string-keyed annotation feature dictionary with typed AnnotationMetrics
status: To Do
type: Task
priority: P2
tags:
  - annotation
  - metrics
  - refactoring
  - safety
dependsOn:
  - ICW-101
related:
  - ICW-111
  - ICW-080
  - ICW-101
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-25
updated: 2026-07-30
---

# ICW-031 — Replace string-keyed annotation feature dictionary with typed AnnotationMetrics

## Summary

**Audit finding (95% confidence):** Code reads `annotation.Features["Confidence"]` / `["Severity"]` in multiple places using raw string indexers. These throw `KeyNotFoundException` if either key is ever absent. The concrete call site at `CreateAnnotationToolTip` (`MainWindow.xaml.cs:724`) does `annotation.Features["Confidence"]` with no `TryGetValue`. A `KeyNotFoundException` here would be silently swallowed by the global dispatcher handler.

**Concrete call sites to migrate:**
1. `CreateAnnotationToolTip` (line 724) — raw indexer access
2. `AnnotationFeaturePresenter.BuildRows` (if it uses raw indexers) — verify
3. Feature-grid DataGrid binding — verify whether it uses `Features["Confidence"]` or typed access

## Root Cause

`SampleAnnotation.Features` is typed as `IReadOnlyDictionary<string, double>` with no schema enforcement. Any consumer must know the string keys "Confidence" and "Severity" by convention. There is no compile-time check, no `TryGetValue` fallback, and no documentation of the contract.

## Scope

### Required Changes

1. **Introduce `AnnotationMetrics` record** on `SampleAnnotation`:
   ```csharp
   public record struct AnnotationMetrics(double Confidence, double Severity);
   ```
2. **Add `AnnotationMetrics Metrics` property** to `SampleAnnotation`:
   - Populated during generation (in `AnnotationGenerator` and `SampleImageGenerator`'s dead duplicate).
   - Keep `Features` dictionary for backward compatibility during migration (deprecate with `[Obsolete]`).

3. **Migrate all call sites:**
   - `CreateAnnotationToolTip` → use `annotation.Metrics.Confidence` / `annotation.Metrics.Severity`
   - `AnnotationFeaturePresenter.BuildRows` → use typed metrics
   - Feature-grid DataGrid binding → bind to `Metrics.Confidence` / `Metrics.Severity`

4. **Remove dead duplicate `GenerateAnnotations`** in `SampleImageGenerator.cs` (lines 574-622) which has its own copy of the feature-dictionary construction. Covered by ICW-018.

### Dependency on ICW-101

ICW-101 (tooltip presenter restore) should land first. It replaces the raw-indexer access in `CreateAnnotationToolTip` with the presenter's `TryGetValue`-based method, which eliminates the crash risk immediately. This ticket then replaces the `TryGetValue`-based access with typed access — a pure refactoring with no behavioral change.

### Acceptance Criteria

- `SampleAnnotation` has a typed `Metrics` property instead of (or in addition to) the string-keyed dictionary.
- All call sites use typed access.
- No `KeyNotFoundException` is possible from feature access.
- `Features` dictionary is deprecated with `[Obsolete]` or removed.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | (contains `SampleAnnotation`) — add `Metrics` property |
| `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs` | Populate `Metrics` during generation |
| `src/InfiniteCanvas.Rendering/AnnotationGenerator.cs` | Populate `Metrics` during generation |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Migrate tooltip and feature-grid to typed access |
| `src/InfiniteCanvas.App/AnnotationFeaturePresenter.cs` | Migrate to typed access |
| `tests/InfiniteCanvas.Tests` | Add typed-metrics tests |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "AnnotationMetrics|TypedMetrics"
```

## Related Tasks

- ICW-101: tooltip presenter restore (prerequisite — eliminates crash risk before refactoring)
- ICW-111: annotation metrics migration (same scope — consider merging)
- ICW-080: annotation feature presentation model (related extraction)
