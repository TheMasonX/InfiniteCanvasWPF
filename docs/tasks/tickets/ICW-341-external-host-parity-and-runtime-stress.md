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
  - ICW-343
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md
  - docs/audits/external-material-source-annotation-readiness-audit-26-08-08.md
  - tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs
  - benchmarks/InfiniteCanvas.Benchmarks/TileWorkCoordinatorBenchmarks.cs
  - docs/benchmarks/BENCHMARKS.md
created: 2026-08-07
updated: 2026-08-08
---

## Summary

Add application-like Windows evidence for an external material inspection host.
The current consumer-host tests prove generic construction and frame publication, while existing benchmarks focus on coordinator behavior.

## Scope

- Add a neutral external source with source-qualified tile and layer revisions.
- Exercise the complete material layer plan through a second host fixture.
- Stress fast scroll, zoom, resize, scene regeneration, tile failure, and close during generation.
- Check stale publication, frame stability, resource cleanup, exception reporting, and resident cache behavior.
- Check colliding tile IDs across sources, revisions, and mip levels.
- Check two scanner columns with horizontal overlap and both precedence modes.
- Check vertical non-overlap validation within one camera column.
- Check defect, marker, and region data through one external annotation adapter.
- Check same-epoch duplicate-worker completion ordering after the focused materializer test passes.
- Archive machine, runtime, build, and result metadata.

## Acceptance Criteria

- A second host renders material layers without referencing `MainWindow` or synthetic source identity.
- Repeated navigation does not publish stale raster, layer, or pixelometer state.
- Resize and close during active generation complete without unobserved exceptions or leaked control resources.
- Failure and regeneration paths preserve the last valid frame or show the defined fallback.
- Identity-collision runs show that each requested source, revision, and mip receives its own payload.
- Same-epoch completion runs show one resident result, one reservation release, and the correct callback after cancel-and-re-request.
- Adapter runs show no reusable path dependency on `SampleImageGenerator` or `SampleAnnotation`.
- Stress results include reproducible commands and target machine metadata.
- The evidence distinguishes unit, integration, and runtime validation levels.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`; `dotnet run --project benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release -- --filter *TileWorkCoordinator*`.
- Result: Consumer-host coverage now includes semantic stale rejection, source-session replacement, ordered layer plans, frozen raster rejection, and colliding payload identities. ICW-144 still does not cover WPF host lifecycle, same-epoch completion ordering, or runtime stress.

## Notes

This task is an evidence gate. It does not claim that a benchmark result alone proves production readiness.

## Latest Audit Findings

- [F-003, same-epoch duplicate completion lacks direct proof](../../audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md)
- [F-005, scanner overlap has no deterministic policy](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)
- [F-006, external heterogeneous annotations lack an adapter boundary](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)
- [F-007, deterministic demo data is not fully extracted](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)

## Related Tasks

- ICW-337
- ICW-076
- ICW-144
- ICW-316
- ICW-343