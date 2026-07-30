---
id: ICW-144
author: Copilot
key: ICW-144
title: Add fast-scroll tile queue stress telemetry and benchmarks
status: In Progress
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
updated: 2026-07-26
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
- [ ] Diagnostics distinguish canceled, stale, failed, resident-fallback, and useful current-viewport completions.
- [ ] Benchmarks report repeated measurements; one-iteration Dry runs are labeled smoke checks only.
- [ ] The result records whether debounce, priority, cancellation, or concurrency limiting is the dominant improvement and identifies remaining bottlenecks.

## Validation

Commands: `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release` and `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

Outcome: `TileWorkCoordinatorBenchmarks.cs` added with 8 benchmark scenarios covering PublishInterestSet (empty, full-visible, none-visible, mixed) and DrainQueueWithLivenessCheck (FIFO fallback, visible-promoted priority, 3-cycle fast-scroll stress). Build: 0 errors. Tests: 93/93 passing.

## Notes

Coordinate stage counters with ICW-132 and benchmark structure with ICW-133. Do not claim a percentage improvement from the existing one-iteration benchmark artifacts.

## Related Tasks

- ICW-141: parent scheduling plan
- ICW-142: bounded materialization
- ICW-143: culling and priority
