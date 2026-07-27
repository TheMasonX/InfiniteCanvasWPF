---
id: ICW-143
author: Copilot
key: ICW-143
title: Add viewport culling and relevance-priority tile scheduling
status: To Do
type: Improvement
priority: P1
tags:
  - rendering
  - tiles
  - viewport
  - scheduling
  - fast-pan
dependsOn:
  - ICW-142
  - ICW-078
related:
  - ICW-076
  - ICW-096
  - ICW-065
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Core/RenderRequestTracker.cs
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
created: 2026-07-26
updated: 2026-07-26
---

## Summary

Update tile interest on every captured viewport change so stale queued requests are culled and current visible tiles are generated before optional prefetch work.

## Scope

- Derive a deterministic visible tile set from the immutable camera snapshot and viewport bounds.
- Publish a request epoch plus optional bounded prefetch margin to the tile coordinator.
- Prioritize visible tiles by relevance to the viewport center, then use stable tile ID/mip tie-breakers.
- Treat prefetch as lower priority and cancel it first under pressure.
- Keep request identity source/revision/mip-aware and align pinning with the actually sampled resident variant.
- Preserve `RenderRequestTracker` stale-frame guards and resident-mip fallback during transitions.

## Acceptance Criteria

- A tile outside the current interest set is not started if it is still queued, and running work loses its claim promptly.
- Current visible requests outrank stale or prefetch requests after a rapid pan or zoom.
- Priority ordering is deterministic for equal relevance and has unit coverage at viewport center, edge, and outside bounds.
- The render path remains non-blocking and never awaits tile generation just to compose a frame.
- A rapid sequence of viewport updates cannot publish a frame or completion callback for an obsolete request epoch.

## Validation

Commands: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` and `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Outcome: Pending implementation.

## Notes

Debouncing can reduce churn, but it must not be the primary correctness mechanism. The coordinator must remain correct when viewport updates arrive faster than a debounce interval. Any prefetch margin and concurrency default should be selected from benchmark evidence.

## Related Tasks

- ICW-141: parent scheduling plan
- ICW-142: cancellation ownership
- ICW-144: stress validation
