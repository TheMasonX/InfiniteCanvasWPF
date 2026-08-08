# InfiniteCanvasWPF Render Orchestration Performance Audit

**Description:** Findings-only performance audit of the MainWindow render orchestration path.
**Timestamp:** 2026-08-07 19:11:04
**Baseline:** HEAD `ccc5bdf`, with a dirty working tree that contains user changes.
**Scope:** Net-new findings only. This audit does not review source changes.
**Status:** Proposed follow-up recorded as [ICW-342](../tasks/tickets/ICW-342-render-orchestration-tile-scan-amortization.md).

## Executive Summary

The render path performs repeated full-scene tile enumeration around each published frame. The path first builds the viewport interest keys, then rebuilds the visible tile array for composition, then scans the scene again for status and diagnostics values. The current benchmark suites measure compositor stages and coordinator behavior, but they do not measure this MainWindow orchestration cost.

This audit accepts one net-new performance improvement task. The impact is classified as P2 because the cost scales with total scene size, while the current source review does not provide runtime measurements for typical or maximum scenes. Confidence is high for the source behavior and medium for the user-visible impact.

## Evidence Baseline

- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L535-L543) scans `_tiles` to build `visibleTileKeys`.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L575-L585) scans `_tiles` again inside the background compositor delegate and passes the new array to cache pinning and bitmap generation.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L600-L613) scans `_tiles` for fetched and queued counts, materializes `completedTiles`, and materializes `completedConversionTiles` after each published frame.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L636-L644) repeats a fetched-tile scan and generation-duration aggregation during periodic diagnostics.
- [ProjectionAndBitmapBenchmarks.Windows.cs](../../benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs) measures the shipped compositor, but not `RenderFrameAsync`, interest publication, cache pinning, or status aggregation.
- [TileWorkCoordinatorBenchmarks.cs](../../benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs) measures interest publication and queue scheduling, but not MainWindow tile selection or diagnostics traversal.

## Standards Findings

### S-001. RenderFrameAsync repeats scene-wide tile enumeration and materializes diagnostic snapshots

**Priority:** P2
**Confidence:** 95 percent
**Classification:** Standards judgement call, duplicated code and allocation pressure.

The frame method contains multiple independent traversals of the same mutable `_tiles` collection. The visible tile predicate appears in both the interest-set construction and the compositor delegate. The post-publication path adds predicate scans for fetched and queued state, then creates arrays for generation and conversion statistics. The periodic diagnostic branch repeats part of that work.

This pattern adds managed CPU work and temporary allocations to the asynchronous frame orchestration path. It does not violate the zero-copy bitmap buffer contract, but it weakens the documented low-allocation rendering goal around the compositor boundary. The duplicated visible-tile selection also makes future changes more likely to update one consumer and miss another.

**Recommended direction:** Compute one frame-local visible tile snapshot and reuse it for interest keys, cache pinning, composition, and publication. Replace repeated metric enumeration with one metrics snapshot or maintained lifecycle counters. Preserve the current status text and diagnostic values.

**Acceptance direction:** A focused test or source guard proves that one render request computes the visible tile collection once. A benchmark or allocation capture measures the remaining status and diagnostic work at increasing scene sizes.

## Spec Findings

### P-001. The benchmark matrix does not cover MainWindow render orchestration cost

**Priority:** P2
**Confidence:** 90 percent
**Classification:** Spec evidence gap.

The design target requires responsive asynchronous rendering for large scenes. The available benchmark matrix isolates tile generation and bitmap composition. Coordinator benchmarks isolate interest publication and queue draining. Neither path exercises the repeated `_tiles` traversals, cache pinning, frame publication, status updates, or periodic diagnostics in `RenderFrameAsync`.

The repository therefore has no repeatable evidence for how frame orchestration scales with total tile count when the viewport contains only part of the scene. This gap prevents a defensible before-and-after performance claim for S-001. It also leaves the cost of the current scene-wide work invisible when compositor benchmarks remain stable.

**Recommended direction:** Add a deterministic orchestration benchmark or a narrowly scoped instrumentation harness. Vary total tile count and visible tile count independently. Record predicate evaluations, managed allocations, elapsed time, and the diagnostic branch state. Use repeated Release measurements and archive the result with the existing benchmark metadata policy.

**Acceptance direction:** The benchmark distinguishes compositor cost from orchestration cost and reports scene-size scaling. The task does not claim an optimization until repeated measurements show the change at the same camera, tile state, and build configuration.

## Corrections and Extensions to Existing Tasks

- [ICW-326](../tasks/tickets/ICW-326-tile-grid-rebuild-scaling.md) is complete and remains closed. It removed the grid overlay traversal. S-001 concerns visible tile selection and post-publication metrics in `RenderFrameAsync`.
- [ICW-132](../tasks/tickets/ICW-132-rendering-performance-stage-instrumentation.md) and [ICW-133](../tasks/tickets/ICW-133-rendering-benchmark-matrix-and-baselines.md) provide stage instrumentation and compositor benchmarks. They do not cover MainWindow orchestration.
- [ICW-144](../tasks/tickets/ICW-144-fast-scroll-tile-queue-stress-validation.md) covers coordinator scheduling and fast-scroll queue behavior. It does not cover scene-wide tile enumeration.
- [ICW-317](../tasks/tickets/ICW-317-persistent-frame-shell.md) and the current user changes cover retained visual surfaces. They do not remove the tile collection scans in `RenderFrameAsync`.

No existing task was reopened. The new scope is recorded in [ICW-342](../tasks/tickets/ICW-342-render-orchestration-tile-scan-amortization.md).

## Priority Order

1. **P2, S-001:** Consolidate visible tile selection and per-frame tile metrics.
2. **P2, P-001:** Add end-to-end orchestration measurement before claiming a performance improvement.

## Open Questions and Validation Gaps

- No runtime benchmark or allocation capture ran during this audit.
- The source review does not establish the common or maximum scene tile count.
- The implementation must preserve cache pinning, interest-set revisions, stale-frame behavior, status text, and periodic diagnostic values.
- A future benchmark must avoid measuring only `ProjectionAndBitmapBenchmarks.Windows.cs`, because that suite bypasses the MainWindow orchestration path.

**Summary:** One Standards finding and one Spec finding support one P2 task. The worst issue on both axes is unmeasured scene-size scaling in the MainWindow frame orchestration path.