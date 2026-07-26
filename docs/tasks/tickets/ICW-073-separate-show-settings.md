---
id: ICW-073-separate-show-settings
key: ICW-073
title: Separate Show Settings for Labels/Boxes/Images
status: In Review
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

# ICW-073 Separate show settings for labels, boxes, and image layers

## Summary

Add per-layer show/hide settings for labels, boxes, sparse image tiles, and background images. Also fix checkbox label color by using a bound brush to ensure readability in different themes.

## Scope

- Add runtime settings and UI bindings for each display layer.
- Expose controls in the debug property editor (ICW-067) and main UI where appropriate.
- Persist settings across application sessions.
- Replace hard-coded checkbox label colors with a bound brush resource.

## Acceptance Criteria

- Each layer can be shown/hidden independently.
- Checkbox labels use a readable brush in all themes.
- Settings persist and round-trip through the application's settings storage. **Open correction:** the background-image visibility toggle is not yet included in the settings record or save path; see ICW-082.

## Validation

- Manual: Toggle each layer, restart app, verify persisted state.
- Add a unit/integration test for settings serialization if practical.
- Current evidence: labels, boxes, and sparse image tiles have settings fields; background-image visibility remains unverified and is tracked by ICW-082.

## Notes

- Consider adding telemetry or default presets for common combinations.
