---
id: ICW-339
author: Copilot
key: ICW-339
title: Add semantic material viewport identity
status: Proposed
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
links:
  - docs/audits/viewport-material-inspection-readiness-delta-2026-08-07.md
  - src/InfiniteCanvas.Core/ICanvasSceneSource.cs
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Define the source and revision identity needed to prove that one accepted frame describes one external material state.
The current integer render revision orders work but does not identify material freshness.

## Scope

- Extend the reusable source contract with source identity, health, and semantic revisions.
- Define scene, layer, display, selection, and render sequence identity.
- Carry the identity through `CanvasFrame` and source change notifications.
- Replace integer-only stale acceptance with semantic identity checks.
- Add consumer-host and contract tests.

## Acceptance Criteria

- An external source can identify its session, scene revision, and layer revisions without exposing application types.
- Each accepted frame carries the source identity and the revisions used to build its raster and interactive state.
- A frame with a stale source or layer revision cannot replace a newer accepted state.
- Display and selection changes have explicit identity semantics.
- The existing integer render sequence remains available as an ordering field, not as the only freshness proof.
- Tests prove out-of-order source, layer, and render completions are rejected consistently.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasSceneSource|FullyQualifiedName~RenderRequestTracker"`; `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~CanvasControlConsumerHost"`.
- Result: Pending implementation. Current source review confirms that `SceneChanged` and `CanvasFrame.Revision` do not carry semantic identity.

## Notes

Use small value types for identity and revisions. Do not place external service identifiers or application data types in `InfiniteCanvas.Core`.

## Related Tasks

- ICW-337
- ICW-312
- ICW-328
- ICW-340
- ICW-076