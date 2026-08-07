---
id: ICW-031-typed-annotation-metrics
author: External Audit (Integration-1)
key: ICW-031
title: Replace string-keyed annotation feature dictionary with typed AnnotationMetrics
status: In Progress
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
updated: 2026-08-07
---

# ICW-031 — Replace string-keyed annotation feature dictionary with typed AnnotationMetrics

## Summary

**Audit finding (95% confidence):** Annotation metrics use a string-keyed dictionary. This forces consumers to know the keys by convention and weakens refactoring safety.

**Concrete call sites to migrate:**
1. `AnnotationFeaturePresenter.BuildRows` — feature-row projection
2. `AnnotationFeaturePresenter.BuildTooltipContent` — typed metric presentation
3. Feature-grid DataGrid binding — verify that it consumes the presenter rows

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

ICW-101 already landed the deferred presenter path. This ticket replaces the presenter’s remaining metric-key lookup with typed access before ICW-314 moves tooltip ownership into the reusable control.

### Acceptance Criteria

- `SampleAnnotation` has a typed `Metrics` property instead of (or in addition to) the string-keyed dictionary.
- All call sites use typed access.
- No `KeyNotFoundException` is possible from feature access.
- `Features` remains a compatibility surface for non-metric rows and is marked obsolete.

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

Wave V evidence:

- Focused annotation presenter tests pass, `3/3`.
- Core tests pass, `196/196`.
- Windows tests pass, `26/26`.
- App Release build passes with the known unused `_frameClaimantId` warning.
- Task tracker validation passes, `224` task files validated and `5` legacy files skipped.
- `git diff --check` passes.

## Related Tasks

- ICW-101: tooltip presenter restore (prerequisite — eliminates crash risk before refactoring)
- ICW-111: annotation metrics migration (same scope — consider merging)
- ICW-080: annotation feature presentation model (related extraction)
