---
id: ICW-324-noise-seam-reconciliation
author: InfiniteCanvas Agent
key: ICW-324
title: Reconcile background noise seamlessness and ICW-129 status
status: Proposed
type: Task
priority: P2
tags:
  - noise
  - determinism
  - tile-generation
  - fastnoise
dependsOn:
  - ICW-129
related:
  - ICW-050
  - ICW-128
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - docs/tasks/tickets/ICW-129-fastnoise2-background-noise.md
  - docs/requirements/functional-requirements-and-invariants.md
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-324 — Reconcile background noise seamlessness and ICW-129 status

## Summary

Audit synthesis findings F-010 and F-022. `SampleImageGenerator` seeds each tile's noise with `options.Seed + 3 * tileIndex` (line 187) and normalizes each tile against its local min/max (lines 551-572). Both defeat world-continuous seamless sampling at tile boundaries. The `ICW-129` ticket claims "seamless worldspace sampling" but is status-divergent (Done in active-tasks, In Progress in the ticket, no JIRA row).

## Scope

- Resolve the requirement conflict first. The registry row "Deterministic tile generation" requires independent per-tile streams. ICW-129 claims seamless worldspace sampling. These conflict.
- Either adopt a single world-continuous noise seed with a documented registry change, or document per-tile variance as intended and strike "seamless" from ICW-129 scope.
- If continuous noise is chosen, normalize against a scene-wide or configuration-derived range instead of per-tile local extrema.
- Keep `annotationSeed` per-tile (line 188). It is separate and correct.

## Acceptance Criteria

- One status and one JIRA row for ICW-129.
- Either a world-continuous seed with a registry change, or per-tile variance documented as intended.
- An adjacent-tile boundary test asserts no value discontinuity at the edge (if continuous).
- No change to `annotationSeed` semantics.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "SampleImageGenerator"`
- Command: tracker-status check for ICW-129 across active-tasks, JIRA, and the ticket file

## Notes

- Do not change the seed until the requirement conflict is resolved. A blind single-seed change can break the ICW-050 determinism contract and pixel-exact tests.

## Related Tasks

- ICW-129 (noise delivery, status reconciliation)
- ICW-050 (deterministic tile generation)
