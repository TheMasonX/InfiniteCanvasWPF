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
  - ICW-343
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md
  - docs/audits/external-material-source-annotation-readiness-audit-26-08-08.md
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-08
---

## Summary

Coordinate the remaining work required before the reusable canvas can replace an external material inspection viewport.
The current foundation needs semantic source identity, deterministic overlap composition, external annotation adapters, sample-data isolation, atomic layer publication, complete materializer integration, immutable boundary ownership, and application-like runtime evidence.

## Scope

- Coordinate ICW-338 through ICW-341 and ICW-343.
- Extend ICW-076 without creating a second materializer task.
- Preserve completed selection, tooltip, buffer fencing, and integer stale-frame work.
- Keep reusable contracts neutral to the external application's domain types.
- Keep overlap precedence and camera-column geometry in the accepted material layer plan.
- Keep sample generation outside reusable rendering contracts.

## Acceptance Criteria

- All child tasks have implementation or validation evidence.
- One external host can publish a source-qualified material frame and its ordered layer plan.
- Stale source or layer revisions cannot update any visual layer.
- The active tile path uses the source-neutral materializer.
- An external host can supply overlapping scanner columns and heterogeneous annotation data without sample types.
- Windows host stress evidence covers navigation, resize, regeneration, failure, and close.

## Validation

- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`.
- Result: The Wave AF review confirms partial readiness. The reusable control now carries semantic identity, frozen raster ownership, complete-key resident payloads, and pre-commit layer publication. Item state stability, legacy materialization removal, callback rollback, same-epoch evidence, and WPF stress remain open.

## Notes

Existing audit findings identify source contracts, layer parity, semantic revisions, and runtime validation as replacement blockers. The 2026-08-08 delta audit adds overlap precedence, camera-column validation, heterogeneous annotation input, and sample-data extraction. ICW-343 owns the new adapter boundary.

Complete ICW-338 item ownership and ICW-076 same-epoch evidence. Define ICW-340 rollback behavior, decide ICW-339 source-event ownership, then run ICW-341 evidence.

## Related Tasks

- ICW-338
- ICW-339
- ICW-340
- ICW-341
- ICW-343
- ICW-076

## Latest Audit

- [External material inspection readiness audit, 2026-08-08](../../audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md)