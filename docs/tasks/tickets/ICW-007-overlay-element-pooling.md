---
id: ICW-007-overlay-element-pooling
key: ICW-007
title: Retain annotation overlay elements during frame publication
status: In Progress
type: Task
priority: P2
tags:
  - icw
  - rendering
  - performance
  - wpf
dependsOn: []
related:
  - ICW-019
  - ICW-028
  - ICW-314
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - benchmarks/InfiniteCanvas.Benchmarks/AnnotationOverlayPoolingBenchmarks.Windows.cs
  - scripts/Export-AnnotationDiagnostics.ps1
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-07-25
updated: 2026-08-07
---

# ICW-007 Retain Annotation Overlay Elements

## Summary

- `UpdateAnnotationLayer` currently clears the annotation canvas and allocates a new WPF visual tree for every published frame.
- Rapid pan, zoom, and tile completion can republish an unchanged annotation view, so this churn adds UI work without changing visible content.

## Scope

- Retain annotation visuals and labels in `MainWindow`, keyed by annotation ID.
- Reuse detached overlay states through a bounded pool when annotations re-enter the visible set.
- Reuse geometry, brushes, labels, and selection animation when an annotation remains visible.
- Remove visuals for annotations that leave the current frame.
- Preserve `CanvasControl` tooltip registration cleanup and lazy tooltip content.
- Remove the unnecessary single-child `Grid` from retained annotation visuals.

## Acceptance Criteria

- A repeated frame with the same annotation instances does not clear or recreate the annotation visual tree.
- A camera-only change updates retained element position and size without recreating annotation controls.
- Display option and selection changes update retained controls and preserve the selected outline animation.
- An annotation removed from the frame has no remaining visual or tooltip registration.
- Tooltip content remains lazy and `CanvasControl` clears registrations before the next frame.
- Production diagnostics report annotation update time, fast-path updates, state creation, pool reuse, and visual-tree add/remove counts.
- The Windows benchmark compares detached-state allocation against bounded-state reuse with the same WPF add/remove workload.
- The app supports retained and recreate overlay modes through a startup switch for repeatable A/B runs.
- The diagnostic logs can export to per-sample CSV and mode summary files through `scripts/Export-AnnotationDiagnostics.ps1`.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CanvasControlConsumerHostTests"`
- Result: Passed 8/8. Consumer-host coverage confirms tooltip cleanup and retained wrapper identity.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "AnnotationTooltipWiringTests|AnnotationOverlayPoolingWiringTests"`
- Result: Passed 4/4, including retained and recreate mode wiring.
- Additional command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore`
- Result: Passed with 0 errors and the existing unused `_frameClaimantId` warning.
- Earlier focused validation: Core 203/203 and Windows 30/30 passed. Final suite counts appear below.
- Benchmark command: `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*AnnotationOverlayPooling*"`
- Benchmark result: Dry and 10-iteration Release measurements passed. At 256 annotations and 100 percent churn, reuse measured about 70.3 ms and 6.5 MB allocated versus 112.1 ms and 18.5 MB for fresh allocation. At 25 percent churn, reuse measured about 17.4 ms and 1.6 MB versus 42.3 ms and 4.7 MB. The benchmark keeps WPF add/remove counts equal and excludes raster generation.
- Production evidence: `AnnotationDiag` now logs overlay update time, equivalent-item fast-path hits, state creation, pool reuse, pool size, and element or label add/remove counts every two seconds with `FrameDiag`.
- A/B switch: pass `--annotation-overlay=retained` or `--annotation-overlay=recreate`. The environment variable `INFINITE_CANVAS_ANNOTATION_OVERLAY_MODE` provides the same selection. The selected mode appears in `AnnotationDiag`.
- Export command: `pwsh -NoProfile -File scripts/Export-AnnotationDiagnostics.ps1 -OutputDirectory artifacts/annotation-ab`. The current log export produced 18 annotation rows, 18 frame rows, and a retained or recreate summary with no unparsed diagnostics.
- Final validation: Core 205/205, Windows 30/30, App Release build passed with the existing `_frameClaimantId` warning, benchmark Release build, task validation 231 files validated, and whitespace checks passed.

## Notes

- The profiler report cannot analyze the FastNoise native function, so this slice targets the managed WPF churn that the trace exposes directly.
- The first implementation keeps the app-specific overlay model and does not introduce a custom `DrawingVisual` renderer.

## Related Tasks

- ICW-019 preserves selection animation continuity.
- ICW-028 covers persistent frame-shell retention, which is already implemented by `CanvasControl`.
- ICW-314 owns the canvas tooltip lifecycle contract.
