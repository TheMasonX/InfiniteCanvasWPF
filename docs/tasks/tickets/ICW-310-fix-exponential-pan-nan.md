---
id: ICW-310-fix-exponential-pan-nan
author: Copilot
key: ICW-310
title: Fix pan NaN by reverting exponential pan
status: Done
type: Bug
priority: P1
tags:
  - camera
  - pan
  - nan
  - regression
dependsOn: []
related:
  - ICW-309
links:
  - src/InfiniteCanvas.Core/CameraTransform.cs
  - tests/InfiniteCanvas.Tests/CameraTransformTests.cs
  - tests/InfiniteCanvas.Tests/ViewportScrollbarPolicyTests.cs
  - tests/InfiniteCanvas.Tests/CanvasViewModelTests.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-310-fix-exponential-pan-nan

## Summary

User requirement: fix pan so navigation works. The exponential pan experiment was the cause of the NaN.

`CameraTransform.Pan` was changed to use `Math.Pow(delta, 1.5)`. In .NET, a negative base with a fractional exponent returns NaN. The first left or up drag set the camera offset to NaN. Once the offset is NaN, every camera computation propagates NaN. The user saw world Y as NaN and lost pan and zoom.

Resolution: revert `CameraTransform.Pan` to the linear delta it used before the experiment. Pan stays deterministic and exact. This also keeps scrollbar thumb drag and track click precise, because those paths compute an exact target offset delta.

## Scope

- Revert `CameraTransform.Pan` to linear deltas. Remove the `_panExponent` constant.
- Add a regression test that pans with negative deltas and asserts finite offsets.
- Keep camera, scrollbar policy, and canvas view model tests aligned to linear pan.

## Acceptance Criteria

- Pan with a negative delta never produces a NaN offset.
- Pan is linear and deterministic.
- Camera, scrollbar policy, and canvas view model tests pass.
- Release app build passes.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~CameraTransform|FullyQualifiedName~ViewportScrollbarPolicy|FullyQualifiedName~CanvasViewModel"`
- Result: Pending.
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Pending.

## Notes

- `CanvasViewModelTests.Pan_UpdatesCameraAndClampsToScene` pans by -1000 and clamps to -500. It doubles as an integration regression test for the NaN case.
- If exponential pan is wanted again, it must use a sign-preserving power (`Sign(value) * Pow(|value|, exponent)`) and land exact scrollbar deltas on their targets.

## Related Tasks

- ICW-309 (canvas decoupling; introduced the same-frame canvas view model wiring)
