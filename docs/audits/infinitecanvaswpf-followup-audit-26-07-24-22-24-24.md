# InfiniteCanvasWPF — Follow-Up Deep Audit (Net-New Findings)

- Date: 2026-07-24
- Reviewer: InfiniteCanvas Agent
- Scope: full static review pass with cross-check against the current ICW backlog and task tickets
- Purpose: capture only new findings, task corrections, or scope extensions not already represented in the current backlog

## Executive Summary

This pass identified six net-new issues and two backlog corrections.

- 1 high reliability/lifecycle defect
- 3 medium architecture and safety defects
- 2 low correctness/consistency issues
- 2 task-structure corrections to reduce backlog fragmentation

## New Findings

### 1) [HIGH] Close-time race can fault RegenerateSceneAsync during shutdown

Confidence: 90%

Evidence:

- Regeneration acquires and releases the semaphore in finally at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L104) and [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L152).
- OnClosed disposes the same semaphore without coordinating with active generation flow at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L902).

Risk:

If regeneration is in-flight while shutdown proceeds, finally can execute after semaphore disposal and throw ObjectDisposedException. Because several entry points are async void, this can become process-terminating behavior when combined with missing global exception handlers.

Recommendation:

- Make generation lifecycle explicitly awaitable on shutdown or defer semaphore disposal until no active users remain.
- Harden the release path against shutdown disposal race.
- Coordinate with the exception safety work.

Proposed task: ICW-029 (P0)

---

### 2) [MEDIUM] Unbounded objectsPerTile input creates OOM/freeze risk

Confidence: 95%

Evidence:

- The UI validates only non-negative integer input at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L874).
- No upper bound is applied before assignment at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L888).
- The generator uses the value as direct annotation-array size through GenerateAnnotations at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L123).

Risk:

A large user-entered value can force huge allocations and long generation times, degrading responsiveness or triggering OutOfMemoryException.

Recommendation:

- Add a bounded validation policy for objects-per-tile with user-facing error text.
- Add tests for max accepted value and rejection behavior.

Proposed task: ICW-030 (P1)

---

### 3) [MEDIUM] Primitive obsession in annotation metadata introduces brittle string-key contracts

Confidence: 92%

Evidence:

- Metadata is generated as a dictionary of string keys at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L155).
- The annotation model stores the data as an IReadOnlyDictionary<string,double> at [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L210).
- The UI reads string keys directly at [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L442) and [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L443).

Risk:

Silent schema drift or typos become runtime failures. Compiler assistance is lost during refactors.

Recommendation:

- Replace the string-key dictionary with a typed value object such as an AnnotationMetrics record.
- Keep the metadata bag only if future extensibility truly requires it.

Proposed task: ICW-031 (P1)

---

### 4) [MEDIUM] Spatial query abstraction is shallow and forces materialization even when only counts are needed

Confidence: 85%

Evidence:

- The current abstraction only exposes Query(SpatialBounds viewport) at [src/InfiniteCanvas.Spatial/ISpatialIndexService.cs](src/InfiniteCanvas.Spatial/ISpatialIndexService.cs#L9).
- The view-model count path uses full list materialization then Count at [src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs](src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs#L46).

Risk:

The contract prevents specialized index implementations from providing lower-allocation count-only or streaming query paths. This limits scalability and makes optimizations awkward.

Recommendation:

- Extend the interface with a count-oriented query contract while preserving the current query API.
- Migrate count-only consumers first.

Proposed task: ICW-032 (P2)

---

### 5) [LOW] Annotation placement has edge bias due to exclusive upper-bound arithmetic

Confidence: 88%

Evidence:

- Local placement uses exclusive upper bounds at [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L139) and [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](src/InfiniteCanvas.Rendering/SampleImageGenerator.cs#L140).

Risk:

Because Random.Next(max) is exclusive, the rightmost and bottommost legal start positions are never chosen. This creates subtle spatial-distribution bias.

Recommendation:

- Use an inclusive max-start policy with an explicit upper-bound offset.

Proposed task: ICW-033 (P3)

---

### 6) [LOW] Boundary semantics are inconsistent between Intersects and pixel sampling

Confidence: 82%

Evidence:

- SpatialBounds.Intersects uses closed comparisons at [src/InfiniteCanvas.Core/SpatialBounds.cs](src/InfiniteCanvas.Core/SpatialBounds.cs#L45).
- Pixel and defect sampling use half-open bounds at [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L136) and [src/InfiniteCanvas.Rendering/SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L223).

Risk:

Edge-touch conditions can be treated differently across query/filter/sampling stages, creating subtle ambiguity at tile and annotation borders.

Recommendation:

- Document a canonical boundary policy and apply it consistently.
- Add explicit tests at border coordinates.

Proposed task: ICW-033 (P3) as a paired cleanup with placement correction.

## Corrections / Extensions To Existing Tasks

### A) ICW-007, ICW-019, and ICW-028 should be managed as one overlay-retention epic

Current state splits overlapping work across three tickets. This increases sequencing risk and duplicate effort.

Correction:

- Keep ICW-007 as the parent scope for retained overlay performance and continuity.
- Treat ICW-019 and ICW-028 as dependent deliverables in the same implementation plan.

### B) ICW-014 should explicitly include shutdown-path exception handling acceptance criteria

Current text focuses on global handlers. It should also require controlled behavior during close-time cancellation and disposal races.

Correction:

- Add acceptance notes requiring no unhandled exceptions during close while render or regeneration operations are active.

## Priority Order (New Items)

1. ICW-029 (P0) — shutdown lifecycle race
2. ICW-030 (P1) — generation input bounds
3. ICW-031 (P1) — typed annotation metrics
4. ICW-032 (P2) — stronger spatial query abstraction
5. ICW-033 (P3) — placement and boundary consistency cleanup

## Open Questions and Validation Gaps

- The shutdown race is strongly evidenced by control flow but should be validated with a close-stress scenario in Windows-targeted tests or a manual run.
- The bounds policy should be aligned with the intended runtime cost model for the defect raster pipeline before finalizing the cap.
- The typed-metrics migration should be scoped carefully so it does not expand beyond the immediate annotation path.
