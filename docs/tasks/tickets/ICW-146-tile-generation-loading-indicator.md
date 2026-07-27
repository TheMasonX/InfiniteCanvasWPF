---
id: ICW-146
author: Copilot
key: ICW-146
title: Show loading indicator during lazy background tile generation
status: Done
type: Bug
priority: P2
tags:
  - rendering
  - tiles
  - ui
  - loading-indicator
  - busy-state
dependsOn: []
related:
  - ICW-079
  - ICW-142
links:
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-07-27
updated: 2026-07-27
---

## Summary

The `RenderBusyBar` progress indicator at the bottom of the viewport is not visible when individual background tiles are being lazily generated, even though multiple on-screen tiles are in need of generation (some have no tile content at all). The user sees blank tile areas without any visual indication that background work is in progress.

## Current behavior

- `BeginBusyOperation` / `EndBusyOperation` track a `_busyOperationCount` that controls `RenderBusyBar.Visibility`.
- This counter is only incremented around `RequestRenderAsync` (per-frame render) and `RegenerateSceneAsync` (full scene rebuild).
- Per-tile background image generation (`EnsurePixelsGenerationStarted` → `Task.Run` in `SampleImageTile.cs`) does **not** participate in the busy counter.
- When tiles are lazily generated (background pixels not yet ready, `TryGetPixelsNonBlocking` returns false or empty), the render path samples a placeholder or fallback mip, but no loading indicator is shown at the bottom of the screen.

## Scope

- Wire per-tile generation state into a visible loading indicator so users can tell that background work is happening.
- Options include:
  - Tying `RenderBusyBar` visibility to the count of tiles with generation in flight (e.g., `IsGenerationQueued` or a per-tile in-flight flag).
  - Adding a dedicated tile-generation progress counter to the diagnostics panel or status bar.
  - Making `LoadingOverlay` (TextBlock) visible while any tile has unfulfilled generation.
- Must not add significant overhead to the hot per-tile generation path.
- Must not race with shutdown or tile reset (generation epoch changes).

## Acceptance Criteria

- The loading indicator (progress bar at bottom or equivalent) is visible while any visible tile has generation in flight.
- The indicator hides automatically when all in-flight generation completes or is canceled.
- Rapid viewport changes that cancel/fire new tile generation do not leave the indicator stuck visible or hidden.
- No measurable allocation or latency overhead in the tile generation path.
- Works correctly during startup (initial scene load), lazy background fetch, and mip generation.

## Validation

Commands: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` and manual visual verification that the bottom progress bar appears while tiles are loading and disappears when generation completes.

Outcome: Pending implementation.

## Notes

This is related to ICW-079 (busy-state coalescing under rapid input) but is a distinct gap: ICW-079 addresses counter churn from rapid input events, while this task addresses the complete absence of any loading signal for per-tile background generation. The viewport-aware tile work scheduling (ICW-142/ICW-143) may change the generation ownership model, which could affect the design of this indicator.

## Integration note

ICW-142 has been implemented and the `TileWorkCoordinator` now exposes `GetCounters()` with `ActiveCount` — the number of concurrently running tile generation operations. This counter is already displayed in the status bar. ICW-146 can use the same counter to drive `RenderBusyBar` visibility: show the bar when `ActiveCount > 0`, hide when `ActiveCount == 0`, debounced to avoid flicker during rapid viewport changes.
