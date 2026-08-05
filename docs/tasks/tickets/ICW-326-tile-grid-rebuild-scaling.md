---
id: ICW-326-tile-grid-rebuild-scaling
author: InfiniteCanvas Agent
key: ICW-326
title: Scale the tile-grid overlay to the visible tile set
status: Proposed
type: Improvement
priority: P2
tags:
  - rendering
  - overlay
  - performance
  - viewport
dependsOn:
  - ICW-317
  - ICW-318
related:
  - ICW-040
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-326 — Scale the tile-grid overlay to the visible tile set

## Summary

Audit synthesis finding F-012. `UpdateTileGridLayer` (`MainWindow.xaml.cs:676-713`) rebuilds the grid from the entire scene's `_tiles` collection on every publish, even though the camera-visible set is already computed. Per-frame cost scales with total scene size in the publish hot path. Carried forward by the ICW-317 persistent-shell rewrite.

## Scope

- Thread the computed `visibleTiles` set into the grid layer instead of iterating the full `_tiles` collection.
- Optionally skip the rebuild when the camera and tile set are unchanged since the last publish.

## Acceptance Criteria

- `UpdateTileGridLayer` no longer touches `_tiles`.
- The grid stays camera-synchronized and non-hit-testable (overlay-layering invariant).

## Validation

- Command: benchmark or timing probe of `OnCanvasFramePublished` with a large scene (16+ tiles) versus the current path
- Command: source assertion that `UpdateTileGridLayer` no longer enumerates `_tiles`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

## Notes

- Host-side follow-up to the frame-shell cluster. Independent of the ICW-316 boundary work.

## Related Tasks

- ICW-317 (persistent frame shell, Done)
- ICW-318 (composition fence, Done)
- ICW-040 (background tile grid overlay)
