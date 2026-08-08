---
id: ICW-342-render-orchestration-tile-scan-amortization
author: Copilot
key: ICW-342
title: Measure and reduce repeated scene-wide tile scans in RenderFrameAsync
status: Proposed
type: Improvement
priority: P2
tags:
  - rendering
  - performance
  - tiles
  - diagnostics
  - benchmarks
  - mainwindow
dependsOn: []
related:
  - ICW-132
  - ICW-133
  - ICW-144
  - ICW-317
  - ICW-326
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs
  - docs/benchmarks/BENCHMARKS.md
  - docs/audits/infinitecanvaswpf-render-orchestration-performance-audit-26-08-07-19-11-04.md
created: 2026-08-07
updated: 2026-08-07
---

# ICW-342, Measure and reduce repeated scene-wide tile scans in RenderFrameAsync

## Summary

`RenderFrameAsync` scans the full `_tiles` collection to build viewport interest keys, then scans it again for visible composition. After publication, it scans the collection again for status and periodic diagnostic values. The current benchmark suites do not measure this orchestration path.

## Scope

- Add a deterministic Release benchmark or instrumentation harness for MainWindow render orchestration.
- Vary total tile count and visible tile count independently.
- Record elapsed time, managed allocations, tile predicate evaluations, and diagnostic branch state.
- Compute one frame-local visible tile snapshot and reuse it for interest keys, cache pinning, composition, and publication.
- Consolidate status and diagnostic tile metrics without changing displayed values or cache lifecycle behavior.
- Preserve current interest-set epoch identity, stale-frame rejection, and zero-copy bitmap handoff.

## Acceptance Criteria

- One render request does not repeat visible tile selection across the interest-set and compositor paths.
- Status and periodic diagnostics do not create avoidable scene-wide snapshots on every frame.
- Cache pinning and bitmap composition receive the same visible tile snapshot used for interest publication.
- Benchmark output reports orchestration scaling separately from compositor scaling.
- Repeated Release measurements compare the same camera, tile state, scene sizes, and build configuration.
- Existing core and Windows tests remain green, and the App Release build has no new errors.

## Validation

- `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release`
- `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*RenderOrchestration*"`
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- `git diff --check`

Current outcome: source review confirms the repeated traversal. Runtime measurements and implementation evidence are pending.

## Notes

ICW-326 removed the separate full-scene grid-overlay traversal. This ticket covers the remaining visible tile selection and status or diagnostics scans in `RenderFrameAsync`.

ICW-132 and ICW-133 cover stage-level rendering evidence. ICW-144 covers coordinator scheduling evidence. None of these tasks exercise MainWindow orchestration.

The audit found no source defect that requires an immediate code change. Keep this task Proposed until the orchestration benchmark confirms the scene-size impact and establishes a before baseline.

## Related Tasks

- ICW-132, stage-level rendering performance instrumentation.
- ICW-133, stage-isolated rendering benchmark matrix.
- ICW-144, fast-scroll tile queue stress validation.
- ICW-317, persistent frame shell.
- ICW-326, visible tile grid rebuild scaling.