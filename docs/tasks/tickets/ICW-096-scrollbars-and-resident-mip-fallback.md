---
id: ICW-096
author: Copilot
key: ICW-096
title: Restore viewport scrollbars and preserve resident imagery during mip transitions
status: In Review
type: Bug
priority: P1
tags:
  - wpf
  - viewport
  - scrollbars
  - mipmaps
  - tile-cache
dependsOn:
  - ICW-076
related:
  - ICW-065
  - ICW-077
  - ICW-078
  - ICW-079
links:
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
created: 2026-07-25
updated: 2026-07-25
---

## Summary

The camera-native viewport scrollbar code still looks up named overlay elements, but the current XAML does not declare them. During zoom transitions, `DrawTile` selects the requested mip and paints a placeholder if that mip is not resident, even when an older payload is available. This is the second priority after eliminating full-chain mip regeneration.

## Scope

- Restore the XAML overlay track/thumb elements and keep them outside viewport measurement.
- Add a non-blocking resident-payload fallback for mip transitions with atomic frame selection.
- Verify cache status is sourced from the active cache instance and distinguishes resident variants, queued work, reservations, and resets.
- Add focused source, core, or Windows regression coverage as appropriate.

## Acceptance Criteria

- Horizontal and vertical viewport scrollbar tracks and thumbs are visible when the camera viewport is smaller than the scene.
- Pan, zoom, resize, regeneration, track click, and thumb drag update scrollbar geometry without changing fixed viewport sizing.
- Zooming to an uncached mip keeps the most appropriate resident image visible until the requested mip completes.
- A completed mip replaces the fallback only at a frame boundary; stale or reset generation cannot publish.
- Cache diagnostics identify the active cache state and do not always report one fixed instance.

## Validation

Passed: focused core tests 24/24; Windows tests 5/5; Release application build succeeded. Full core-suite validation remains to be recorded after the surrounding work is complete.

## Notes

Research found the missing bars were a XAML/code-behind wiring regression, not a native `ScrollViewer` problem. The historical overlay was restored. Direct mip generation is the first priority because full-resolution regeneration was the dominant avoidable delay; resident-payload fallback is the next priority and now requests the target asynchronously while sampling the closest resident variant. Cache-variant accounting remains a follow-up.

## Related Tasks

- ICW-065 camera-native viewport scrollbars
- ICW-076 source-agnostic background tile mip levels
- ICW-077 scrollbar overlay hardening