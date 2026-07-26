---
id: ICW-072-zoom-notch-continuity-at-axis-floors
key: ICW-072
title: Icw 072 Preserve Uniform Width Clamped Zoom Round Trip
status: Reverted
type: Bug
priority: P1
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

# ICW-072-zoom-notch-continuity-at-axis-floors

## Summary

Preserve the uniform width-clamped round trip when zooming out to an axis floor and then zooming back in.

## Acceptance Criteria

- The `xIsClamped || yIsClamped` branch remains an OR condition, not XOR.
- A clamped transition must not be replaced with independent per-axis zoom that leaves the viewport no longer clamped to width.
- Existing axis-clamp and uniform-recovery tests continue to pass.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter FullyQualifiedName~ViewportZoomPolicyTests`
- Result: 5/5 passed after reverting the incorrect independent-floor regression.

## Notes

The attempted independent-floor change was incorrect: it made the zoom path anisotropic on zoom-in and broke the uniform width-clamped round trip. The existing `xIsClamped || yIsClamped` policy is intentional and must be preserved.
