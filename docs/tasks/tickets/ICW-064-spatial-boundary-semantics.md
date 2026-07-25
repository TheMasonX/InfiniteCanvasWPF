---
id: ICW-064
author: Copilot
key: ICW-064
title: Unify boundary semantics across SpatialBounds, renderer, and index queries
status: Proposed
type: Task
priority: P2
tags:
  - spatial
  - boundaries
  - renderer
dependsOn: []
related: []
links:
  - src/InfiniteCanvas.Core/
  - src/InfiniteCanvas.Rendering/
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Summary
-------

Resolve inconsistencies between closed `SpatialBounds.Intersects` and half-open sampling used by renderer/pixel sampling to avoid off-by-one omissions or double-counting at tile boundaries.

Proposed Change
---------------

- Define the project's canonical boundary semantics in `src/InfiniteCanvas.Core/SpatialBounds.cs` (recommend closed intervals for geometry queries).
- Update renderer sampling to match the canonical policy (or document exceptions with explicit conversions).
- Add tests covering adjacent tiles and point-on-edge membership behavior.

Risk Level
----------

Low — behavioral change limited to edge cases; avoid affecting visual correctness noticeably for most scenes.

Validation Commands
-------------------

```powershell
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter Boundary
```

Minimal Tests
-------------

- `SpatialBounds_Intersects_PointOnEdge` tests for expected inclusion/exclusion.
- `SampleImageTile_Sampling_BorderConsistency` verify sampling produces same inclusion decisions as `SpatialBounds`.
