---
id: ICW-144
author: Copilot
key: ICW-144
title: Add fast-scroll tile queue stress telemetry and benchmarks
status: In Review
type: Spike
priority: P1
tags:
  - rendering
  - tiles
  - performance
  - benchmarks
  - diagnostics
  - wave-e
dependsOn:
  - ICW-142
  - ICW-143
related:
  - ICW-132
  - ICW-133
  - ICW-064
links:
  - benchmarks/InfiniteCanvas.Benchmarks
  - src/InfiniteCanvas.Rendering
  - docs/requirements/functional-requirements-and-invariants.md
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
created: 2026-07-26
updated: 2026-08-03
---

## Summary

Prove that viewport-aware scheduling reduces stale work and improves useful tile completion during rapid navigation, using repeatable telemetry and benchmark scenarios rather than anecdotal queue counts.

## Scope

- Add deterministic pan/zoom traces that produce viewport updates faster than generation can complete.
- Measure queue depth, active work, cancellation latency, coalescing rate, stale completion count, cache-hit/useful-completion rate, and reservation balance.
- Compare the current behavior against bounded cancellation and priority scheduling at matching tile sizes, mip levels, cache state, and build configuration.
- Archive machine/runtime configuration and identify practical concurrency/prefetch defaults.

## Acceptance Criteria

- [x] A stress trace demonstrates bounded queue depth and no unbalanced reservations after completion or cancellation.
- [x] Diagnostics distinguish canceled, stale, failed, resident-fallback, and useful current-viewport completions.
- [ ] Benchmarks report repeated measurements; one-iteration Dry runs are labeled smoke checks only.
- [ ] The result records whether debounce, priority, cancellation, or concurrency limiting is the dominant improvement and identifies remaining bottlenecks.

## Validation

Commands: `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release`, `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SampleImageTileTests|FullyQualifiedName~RenderingDiagnosticsTests"`, and `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

Outcome: `TileWorkCoordinatorBenchmarks.cs` retains seven benchmark methods covering PublishInterestSet (empty, full-visible, none-visible, mixed) and DrainQueueWithLivenessCheck (FIFO fallback, visible-promoted priority, and fast-scroll stress). Rendering diagnostics now classify resident fallback, useful current completions, and stale discarded completions. Focused rendering tests pass 18/18. The method count is seven; parameterized cases are not additional methods.

## Notes

Coordinate stage counters with ICW-132 and benchmark structure with ICW-133. Do not claim a percentage improvement from the existing one-iteration benchmark artifacts.

## Related Tasks

- ICW-141: parent scheduling plan
- ICW-142: bounded materialization
- ICW-143: culling and priority

## Wave Z Update, 2026-08-07

The diagnostics boundary now reports `ResidentFallback`, `Useful`, and `Stale` per mip. Native and mip coordinator callbacks classify publication outcomes, and resident fallback scans classify non-exact payload reuse. Existing `Reused`, `Generated`, `Rejected`, `Failed`, and `Evicted` counters remain unchanged. ICW-144 stays In Review until repeated hardware benchmark evidence exists.

## Council Update, 2026-08-03

Add evidence gates for queue scan allocations, callback exception diagnostics, queued-work eviction, and repeated benchmark measurements. Keep the seven-method count. Do not claim performance improvement from one-iteration smoke output.
