---
id: ICW-304-primitive-obsession-coordinates
key: ICW-304
title: Reduce primitive obsession: introduce strong types for world vs pixel units
status: Proposed
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

Summary:
Many APIs use raw `double` for positions, extents, and scales across world and pixel coordinate spaces. Introducing small domain types (e.g., `WorldLength`, `PixelLength`, `Scale`) can reduce unit-mixups and improve API clarity.

Audit synthesis (2026-08-04) added: `CanvasViewModel.ComputeMinimumZoom` divides by `SceneBounds` with no guard (findings C1-028 + C2-009, merged). Add a typed scale and a `HasScene`/zero-bounds guard within this ticket's scope. This is a division guard and typed-scale concern, not a boundary-semantics concern (that stays in ICW-308).

Scope:
- Audit `CameraTransform`, `SpatialBounds`, and tile selection code in `src/InfiniteCanvas.Rendering`.

Acceptance criteria:
- Identify the top 3 public APIs most at risk for unit mismatch and add strong-type wrappers or documented conversion helpers.
- Add unit tests demonstrating correct conversions and prevented misuse.

Validation commands:
- `dotnet build ./src/InfiniteCanvas.Core/InfiniteCanvas.Core.csproj --configuration Release`

Estimated effort: Medium
Risk: Low
Suggested owner: @core-team
