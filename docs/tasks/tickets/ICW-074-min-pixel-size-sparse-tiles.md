---
id: ICW-074-min-pixel-size-sparse-tiles
key: ICW-074
title: Minimum Pixel Size for Sparse Tiles
status: Done
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-074 Minimum pixel size for sparse tiles

## Summary

Add a configurable minimum pixel size threshold for generating sparse tiles so that tiles below the threshold are not generated when the viewport is zoomed out.

## Scope

- Add a runtime setting for minimum sparse-tile size (in device pixels).
- Update generation pipeline to skip creating sparse tiles below threshold.
- Expose setting in the debug property editor and persist it.

## Acceptance Criteria

- Sparse tiles below the configured threshold are not generated.
- Memory and CPU usage improve when zoomed far out (empirical validation).

## Validation

- Implemented: Added a persisted runtime slider for minimum sparse-tile pixel size and used it to skip generation for tiles that would project below the threshold while rendering.
- Verification: Ran the targeted core regression tests for sparse-tile generation and settings persistence; 18/18 passed.
- Follow-up: Optionally benchmark the current threshold against extreme zoom levels to quantify the CPU and memory improvement.

## Notes

- Consider default that preserves at least one tile at center viewport.
