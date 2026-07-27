---
id: ICW-140
status: Done
summary: Preserve resident mip fallback while native mip0 generates
scope:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - tests/InfiniteCanvas.Windows.Tests
validation-command: dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Debug --no-build
evidence:
  - "Windows tests passed: 10/10"
  - "Observed mip1 remains visible while mip0 generates during zoom-in"
findings: |
  When requesting the native (mip 0) background payload while it was still being
  generated, the renderer previously showed the canvas background because the
  zero-argument non-blocking pixel accessor did not fall back to resident mip
  payloads. The mip-aware accessor did provide fallbacks for non-zero requests,
  but the native path was special-cased and returned a placeholder instead.

  The fix makes the non-blocking native accessor return the nearest available
  resident mip (if present) while starting native generation. This preserves
  visual continuity during zoom transitions.
next-steps: |
  - Visually validate mip transitions during aggressive zooming and panning.
  - Consider extending unit tests to cover render-level mip-fallback transitions.
---
