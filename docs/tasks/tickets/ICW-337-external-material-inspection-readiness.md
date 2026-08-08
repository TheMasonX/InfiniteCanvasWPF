---
id: ICW-337
author: Copilot
key: ICW-337
title: Coordinate external material inspection viewport readiness
status: Proposed
type: Epic
priority: P0
tags:
  - external-viewport
  - material-inspection
  - readiness
  - architecture
dependsOn: []
related:
  - ICW-076
  - ICW-312
  - ICW-316A
  - ICW-328
  - ICW-144
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Coordinate the remaining work required before the reusable canvas can replace an external material inspection viewport.
The current foundation needs semantic source identity, atomic layer publication, complete materializer integration, immutable boundary ownership, and application-like runtime evidence.

## Scope

- Coordinate ICW-338 through ICW-341.
- Extend ICW-076 without creating a second materializer task.
- Preserve completed selection, tooltip, buffer fencing, and integer stale-frame work.
- Keep reusable contracts neutral to the external application's domain types.

## Acceptance Criteria

- All child tasks have implementation or validation evidence.
- One external host can publish a source-qualified material frame and its ordered layer plan.
- Stale source or layer revisions cannot update any visual layer.
- The active tile path uses the source-neutral materializer.
- Windows host stress evidence covers navigation, resize, regeneration, failure, and close.

## Validation

- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`.
- Result: Pending implementation of child tasks. This audit records the current readiness gap.

## Notes

Existing master audit findings already identify source contracts, layer parity, semantic revisions, and runtime validation as replacement blockers. This epic converts those findings into current-source acceptance work.

## Related Tasks

- ICW-338
- ICW-339
- ICW-340
- ICW-341
- ICW-076