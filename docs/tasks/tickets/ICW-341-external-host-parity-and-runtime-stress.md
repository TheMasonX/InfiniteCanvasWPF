---
id: ICW-341
author: Copilot
key: ICW-341
title: Prove external host parity and runtime stress behavior
status: Proposed
type: Spike
priority: P1
tags:
  - external-viewport
  - windows
  - runtime-validation
  - stress
dependsOn:
  - ICW-076
  - ICW-339
  - ICW-340
related:
  - ICW-337
  - ICW-144
  - ICW-316
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs
  - docs/benchmarks/BENCHMARKS.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Add application-like Windows evidence for an external material inspection host.
The current consumer-host tests prove generic construction and frame publication, while existing benchmarks focus on coordinator behavior.

## Scope

- Add a neutral external source with source-qualified tile and layer revisions.
- Exercise the complete material layer plan through a second host fixture.
- Stress fast scroll, zoom, resize, scene regeneration, tile failure, and close during generation.
- Check stale publication, frame stability, resource cleanup, exception reporting, and resident cache behavior.
- Archive machine, runtime, build, and result metadata.

## Acceptance Criteria

- A second host renders material layers without referencing `MainWindow` or synthetic source identity.
- Repeated navigation does not publish stale raster, layer, or pixelometer state.
- Resize and close during active generation complete without unobserved exceptions or leaked control resources.
- Failure and regeneration paths preserve the last valid frame or show the defined fallback.
- Stress results include reproducible commands and target machine metadata.
- The evidence distinguishes unit, integration, and runtime validation levels.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`; `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release -- --filter *TileWorkCoordinator*`.
- Result: Pending implementation. ICW-144 does not cover the WPF host lifecycle or complete material layer publication.

## Notes

This task is an evidence gate. It does not claim that a benchmark result alone proves production readiness.

## Related Tasks

- ICW-337
- ICW-076
- ICW-144
- ICW-316