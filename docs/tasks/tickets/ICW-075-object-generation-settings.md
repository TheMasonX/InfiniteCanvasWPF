---
id: ICW-075-object-generation-settings
key: ICW
title: Global Object Generation Settings
status: Proposed
type: Task
priority: P3
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

# ICW-075 Object generation global settings

## Summary

Move object generation parameters to global settings rather than per-tile, support objects spanning background tiles, add margin configuration, and add controls in the debug property editor for tuning generation (count, size, distribution).

## Scope

- Introduce a global configuration object for generator parameters.
- Update tile/object generator to consult global settings and honor margins for objects that span tiles.
- Expose controls in the debug property editor for: object count, size range, distribution mode, and margin values.

## Acceptance Criteria

- Objects are generated using global settings and can span tile boundaries when within margins.
- Debug property editor exposes tuning controls and changes take effect at runtime.

## Validation

- Manual: Toggle parameters and verify generated objects across tile boundaries.
- Add unit tests for margin handling and cross-tile placement.

## Notes

- Ensure backward compatibility with existing per-tile data where feasible.
