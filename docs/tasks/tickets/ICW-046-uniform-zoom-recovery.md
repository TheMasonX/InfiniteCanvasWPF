# ICW-046: Uniform Zoom Recovery

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Prefer uniform wheel zoom whenever one common target scale keeps both viewport axes above their respective fit floors. Retain anisotropic zoom only while a uniform target would still clamp one axis.

## Scope

- Correct pure wheel zoom policy behavior for zoom-in after a prior axis clamp.
- Preserve existing independent clamp behavior while zooming out.
- Cover both the retained-clamp and uniform-recovery transitions with deterministic unit tests.

## Validation

- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release --filter FullyQualifiedName~ViewportZoomPolicyTests`
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- The current policy clamps raw per-axis targets independently and therefore never converges an anisotropic camera back to a valid uniform target during zoom-in.
- Zoom-in now holds an axis at its floor while the other axis remains below the common legal target, then applies a shared target scale once that threshold is reached.
- Focused `ViewportZoomPolicyTests` passed 5/5 and the Release app build succeeded.

## Next Step

- Visually verify cursor anchoring across the uniform-recovery transition.