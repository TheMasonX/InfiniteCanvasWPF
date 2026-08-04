---
id: ICW-318-composition-fenced-buffer-handoff
author: InfiniteCanvas Agent
key: ICW-318
title: Fence frame-buffer reuse on WPF composition passes
status: Done
type: Bug
priority: P0
tags:
  - rendering
  - compositor
  - flicker
  - synchronization
dependsOn:
  - ICW-P0-BUFFER-REUSE-SYNC
related:
  - ICW-317
  - ICW-021
  - ADR-0004
links:
  - src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Windows.Tests/FrameBufferPoolTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-318 — Fence frame-buffer reuse on WPF composition passes

## Summary

Black horizontal bands still appeared during fast scrolling after the triple-buffer fix (ICW-P0-BUFFER-REUSE-SYNC) and the persistent frame shell (ICW-317). The user captured the artifact on video: a gray tiled background with black horizontal bands. The placeholder gray is 128, so the bands are not the placeholder path.

The bands are the compositor sampling a buffer section mid-write. `GenerateFrozenBitmap` clears the whole section (black), then draws tiles row by row (gray). If the compositor reads the section during that write, it sees a horizontal boundary between drawn tiles and the cleared black area. The triple-buffer rotation gave only one frame cycle of slack. WPF composition can lag more than one frame when the render loop is saturated, so the race survived.

## Root Cause

`FrameBufferPool` promoted a retired buffer to reusable after a fixed one-frame delay. The delay is probabilistic, not a handoff. Nothing confirmed that the compositor had actually finished the frame that displayed the buffer.

## Fix

Make reuse conditional on a real composition handoff:

- `FrameBufferPool.OnCompositionFrame()` advances a two-stage pipeline. A retired buffer moves `retiring` → `confirmed` on the first composition pass and `confirmed` → `reusable` on the second. It is reused only after two full passes.
- `MainWindow` subscribes to `CompositionTarget.Rendering` and calls `OnCompositionFrame()` once per pass. It unsubscribes in `OnClosed`.
- `AcquireBackBuffer` reuses only buffers from the reusable stage (or the staged back buffer from a stale frame). It disposes reusable buffers whose size no longer matches the viewport.
- All pool members run on the UI thread, so no locking is needed.

Two composition passes give the render thread enough slack to finish the frame that displayed the buffer, even when the render loop is saturated.

## Files Changed

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs` | Two-stage `retiring`/`confirmed`/`reusable` pipeline, `OnCompositionFrame`, reusable-stage acquire with size-mismatch disposal |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | `CompositionTarget.Rendering` subscription and handler; unsubscribe in `OnClosed` |
| `tests/InfiniteCanvas.Windows.Tests/FrameBufferPoolTests.cs` | Rewritten for pipeline semantics; two-pass reusability test, rotation reuse test, size-mismatch disposal test |

## Validation

- `dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release`: 18/18 pass.
- `dotnet test tests/InfiniteCanvas.Tests --configuration Release`: 156/156 pass.
- App Release build: compiles with no CS errors. Relink was blocked only by the running app locking its output DLLs.
- Manual: fast-scroll verification is pending user confirmation.

## Next Step

User closes the app, rebuilds, and fast-scrolls. If black bands still appear, the remaining lever is ICW-007 (pool the annotation overlay elements) plus reducing the per-frame UI-thread visual rebuild cost, which worsens composition lag.
