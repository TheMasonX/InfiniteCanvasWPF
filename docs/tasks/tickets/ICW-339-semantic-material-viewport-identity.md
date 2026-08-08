---
id: ICW-339
author: Copilot
key: ICW-339
title: Add semantic material viewport identity
status: In Progress
type: Story
priority: P0
tags:
  - external-viewport
  - source-identity
  - revisions
  - inspection
dependsOn:
  - ICW-312
  - ICW-328
related:
  - ICW-337
  - ICW-340
  - ICW-076
  - ICW-343
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - docs/audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md
  - docs/audits/external-material-source-annotation-readiness-audit-26-08-08.md
  - src/InfiniteCanvas.Core/ICanvasSceneSource.cs
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-08
---

## Summary

Define the source and revision identity needed to prove that one accepted frame describes one external material state.
The current integer render revision orders work but does not identify material freshness, and the active raster payload map drops complete tile identity before composition.

## Scope

- Extend the reusable source contract with source identity, health, and semantic revisions.
- Define scene, layer, display, selection, and render sequence identity.
- Carry the identity through `CanvasFrame` and source change notifications.
- Preserve source, tile, layer, content revision, and mip identity through accepted frame inputs and raster payload lookup.
- Preserve camera-column identity and overlap precedence through accepted material inputs.
- Replace integer-only stale acceptance with semantic identity checks.
- Add consumer-host, contract, and colliding-tile identity tests.

## Acceptance Criteria

- An external source can identify its session, scene revision, and layer revisions without exposing application types.
- Each accepted frame carries the source identity and the revisions used to build its raster and interactive state.
- A frame with a stale source or layer revision cannot replace a newer accepted state.
- Display and selection changes have explicit identity semantics.
- The existing integer render sequence remains available as an ordering field, not as the only freshness proof.
- The accepted frame and raster payload map retain complete tile identity. Equal tile IDs from different sources, revisions, or mip levels cannot replace each other.
- Tests prove out-of-order source, layer, and render completions are rejected consistently.
- Tests prove colliding tile IDs select the payload for the requested source, revision, and mip.
- Tests prove camera-column and overlap metadata remain attached to the accepted source identity.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasSceneSource|FullyQualifiedName~RenderRequestTracker"`; `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasControlConsumerHost"`.
- Result: `CanvasFrameIdentity`, ordered layer revisions, typed scene-change arguments, source-session replacement, semantic stale rejection, and complete-key raster payload lookup are implemented. Focused source-contract tests pass 5/5, consumer-host tests pass 14/14, and colliding-ID raster tests pass. `CanvasControl` still requires an explicit host render request after `SceneChanged`.

## Notes

Use small value types for identity and revisions. Do not place external service identifiers or application data types in `InfiniteCanvas.Core`. Keep source-session replacement independent from a reset render counter.

## Latest Audit Findings

- [F-001, full tile identity is lost before raster composition](../../audits/external-material-inspection-readiness-audit-26-08-08-12-35-58.md)
- [F-004, active composition is not source agnostic](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)
- [F-005, scanner overlap has no deterministic policy](../../audits/external-material-source-annotation-readiness-audit-26-08-08.md)

## Related Tasks

- ICW-337
- ICW-312
- ICW-328
- ICW-340
- ICW-076
- ICW-343