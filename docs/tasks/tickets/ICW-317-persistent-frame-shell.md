---
id: ICW-317-persistent-frame-shell
author: InfiniteCanvas Agent
key: ICW-317
title: Use a persistent frame shell to stop per-frame Viewbox teardown flashes
status: Done
type: Bug
priority: P1
tags:
  - rendering
  - compositor
  - flicker
  - frame-shell
dependsOn:
  - ICW-P0-BUFFER-REUSE-SYNC
related:
  - ICW-007
  - ADR-0004
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Tests/FrameShellWiringTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-317 — Persistent frame shell to stop per-frame Viewbox teardown flashes

## Summary

The canvas still flashed occasionally while scrolling after the triple-buffer fix (ICW-P0-BUFFER-REUSE-SYNC) landed. The remaining flash came from a second mechanism: `PublishFrame` replaced the whole `Viewbox` child on every frame.

Each publish built a fresh `Grid` with a new `Image`, a new tile-grid `Canvas`, and a new annotation `Canvas`, then assigned it as `FramePresenter.Child`. Replacing the Viewbox child tears down the old visual tree and builds the new one. Between the detach and the first render of the new tree, the Viewbox shows its dark background. That gap is the occasional black flash.

## Root Cause

`BuildFrameVisual` returned a new element tree per frame. `PublishFrame` assigned it to the Viewbox every publish, even when the camera barely moved and a tile-completion event triggered the render.

## Fix

Replace per-frame tree building with a persistent frame shell:

- `EnsureFrameShell` creates the `Grid`, `Image`, tile-grid `Canvas`, and annotation `Canvas` once and attaches the shell to the Viewbox once.
- `PublishFrame` updates the shell in place: swap `Image.Source`, repopulate the tile-grid and annotation canvases, and set the shell size.
- The overlay canvases are cleared and repopulated in the same UI-thread pass, so WPF composes one frame with no intermediate empty state.
- The `Image` element is stable; only its `Source` changes. A source swap on a stable element is atomic in the render pass.
- `FramePresenter.Child` is now assigned exactly twice: the shell attach in `EnsureFrameShell` and the detach in `OnClosed`.

This is the first step of the ICW-007 direction (retained frame shell). Per-frame annotation element allocation remains; pooling the overlay elements is the follow-up.

## Files Changed

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Persistent shell fields, `EnsureFrameShell`, `UpdateTileGridLayer`, `UpdateAnnotationLayer`; `PublishFrame` updated; `BuildFrameVisual` and `BuildTileGridLayer` removed |
| `tests/InfiniteCanvas.Tests/FrameShellWiringTests.cs` | New source-wiring regression tests: shell exists, Viewbox child assigned exactly twice |

## Validation

- `dotnet test tests/InfiniteCanvas.Tests --configuration Release`: 156/156 pass (2 new wiring tests).
- `dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release`: 18/18 pass.
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`: 0 errors.
- Manual: fast-scroll verification is pending user confirmation.

## Next Step

User verifies the remaining occasional flash is gone. If any flash remains, the next lever is ICW-007 (pool the annotation overlay elements and persist the selection shape so the selection animation does not restart per frame).
