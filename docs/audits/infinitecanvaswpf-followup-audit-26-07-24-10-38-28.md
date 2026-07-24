# InfiniteCanvasWPF — Follow-Up Deep Audit (Net-New Findings)

- Date: 2026-07-24
- Reviewer: InfiniteCanvas Agent
- Scope: full static review pass with cross-check against ICW-001 through ICW-028
- Purpose: capture only new findings, task corrections, or scope extensions not already represented in the current backlog

## Executive Summary

This pass identified six net-new issues and two backlog corrections.

- 1 High reliability/lifecycle defect
- 3 Medium architecture and safety defects
- 2 Low correctness/consistency issues
- 2 task-structure corrections to reduce backlog fragmentation

## New Findings

### 1) [HIGH] Close-time race can fault `RegenerateSceneAsync` during shutdown

Confidence: 90%

Evidence:

- `RegenerateSceneAsync` acquires and always releases `_generationGate` in `finally`:
  - `await _generationGate.WaitAsync(_lifetime.Token)` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L104)
  - `_generationGate.Release()` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L152)
- `OnClosed` disposes `_generationGate` without coordinating with active generation flow:
  - `_generationGate.Dispose()` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L902)

Risk:

If regeneration is in-flight while shutdown proceeds, `finally` can execute after semaphore disposal and throw `ObjectDisposedException`. Because several entry points are `async void`, this can become process-terminating behavior when combined with missing global exception handlers.

Recommendation:

- Make generation lifecycle explicitly awaitable on shutdown (or defer semaphore disposal until no active users).
- Harden `finally` release path against shutdown disposal race.
- Coordinate with ICW-014 exception safety work.

Proposed task: ICW-029 (P0)

---

### 2) [MEDIUM] Unbounded `objectsPerTile` input creates OOM/freeze risk

Confidence: 95%

Evidence:

- UI validates only non-negative integer for objects per tile:
  - parse and guard at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L874)
- no upper bound is applied before assignment:
  - `_objectsPerTile = objectsPerTile` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L888)
- generator uses `objectsPerTile` as direct annotation-array size:
  - `GenerateAnnotations(..., int count, ...)` at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L123)

Risk:

A large user-entered value can force huge allocations and long generation times, degrading responsiveness or causing `OutOfMemoryException`.

Recommendation:

- Add bounded validation policy for objects-per-tile with user-facing error text.
- Add tests for max accepted value and rejection behavior.

Proposed task: ICW-030 (P1)

---

### 3) [MEDIUM] Primitive obsession in annotation metadata (`Dictionary<string,double>`) introduces brittle string-key contracts

Confidence: 92%

Evidence:

- metadata generated as `new Dictionary<string, double>` at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L155)
- annotation model stores as `IReadOnlyDictionary<string, double>` at [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L210)
- UI dereferences string keys directly:
  - `annotation.Features["Confidence"]` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L442)
  - `annotation.Features["Severity"]` at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L443)

Risk:

Silent schema drift or typos become runtime failures. Compiler cannot help during refactors.

Recommendation:

- Replace string-key dictionary with typed value object (`AnnotationMetrics` record struct).
- Keep extensibility via optional metadata bag only if needed.

Proposed task: ICW-031 (P1)

---

### 4) [MEDIUM] Spatial query abstraction is shallow and forces materialization even when only counts are needed

Confidence: 85%

Evidence:

- current abstraction only exposes `IReadOnlyList<T> Query(SpatialBounds viewport)` at [src/InfiniteCanvas.Spatial/ISpatialIndexService.cs](src/InfiniteCanvas.Spatial/ISpatialIndexService.cs#L9)
- view-model count path uses full list materialization then `.Count`:
  - `_spatialIndexService.Query(viewport).Count` at [src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs](src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs#L46)

Risk:

The contract prevents specialized index implementations from providing lower-allocation count-only or streaming query paths. This limits scalability and makes optimizations awkward.

Recommendation:

- Extend interface with `QueryCount` (or projection callback/iterator contract) while preserving current query API.
- Migrate count-only consumers first.

Proposed task: ICW-032 (P2)

---

### 5) [LOW] Annotation placement has edge bias due exclusive upper bound arithmetic

Confidence: 88%

Evidence:

- local placement uses:
  - `random.Next(0, Math.Max(1, (int)tileBounds.Width - width))` at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L139)
  - `random.Next(0, Math.Max(1, (int)tileBounds.Height - height))` at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L140)

Risk:

Because `Random.Next(max)` is exclusive at upper bound, the rightmost/bottommost legal start positions are never chosen. This introduces subtle spatial-distribution bias.

Recommendation:

- Use inclusive max-start policy with `+ 1` in the exclusive upper bound expression.

Proposed task: ICW-033 (P3)

---

### 6) [LOW] Boundary semantics are inconsistent between `Intersects` and pixel sampling

Confidence: 82%

Evidence:

- `SpatialBounds.Intersects` uses closed comparisons (`<=`, `>=`) at [src/InfiniteCanvas.Core/SpatialBounds.cs](src/InfiniteCanvas.Core/SpatialBounds.cs#L45)
- pixel and defect sampling use half-open bounds (`>= Right` and `>= Bottom` rejected) at:
  - [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L136)
  - [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L223)

Risk:

Edge-touch conditions can be treated differently across query/filter/sampling stages, creating subtle ambiguity at tile/annotation borders.

Recommendation:

- Document canonical boundary policy (closed vs half-open) and apply consistently.
- Add explicit tests at border coordinates.

Proposed task: ICW-033 (P3) as a paired cleanup with placement correction.

## Corrections / Extensions To Existing ICW Tasks

### A) ICW-007, ICW-019, ICW-028 should be managed as one overlay-retention epic

Current state splits overlapping work across three tickets. This increases sequencing risk and duplicate effort.

Correction:

- Keep ICW-007 as parent scope (retained overlay performance + continuity).
- Treat ICW-019 (animation continuity) and ICW-028 (frame visual shell retention) as dependent deliverables in one implementation plan.

### B) ICW-014 should explicitly include shutdown-path exception handling acceptance criteria

Current text focuses on global handlers. It should also require controlled behavior during close-time cancellation/disposal races.

Correction:

- Add acceptance note requiring no unhandled exceptions during close while render/regenerate operations are active.

## Priority Order (New Items)

1. ICW-029 (P0) — shutdown lifecycle race
2. ICW-030 (P1) — generation input bounds
3. ICW-031 (P1) — typed annotation metrics
4. ICW-032 (P2) — stronger spatial query abstraction
5. ICW-033 (P3) — placement/boundary consistency cleanup
