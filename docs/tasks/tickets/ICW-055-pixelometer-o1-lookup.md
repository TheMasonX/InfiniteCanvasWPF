---
status: proposed
summary: Replace pixelometer linear tile scan with O(1) grid lookup
scope:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - tests/InfiniteCanvas.Tests/*
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter FullyQualifiedName~TryReadPixelValue
findings_evidence: |
  - `TryReadPixelValue` iterates `_tiles` linearly per mouse move up to 2000 tiles (audit F-013).
  - This is an avoidable per-mouse-move O(n) cost; tiles are on a regular grid and support direct index arithmetic.
next_steps:
  - Add grid-index helper that computes tile index from world coordinates and reads from `_tiles` array. Owner: @engineer
  - Replace linear scan in `TryReadPixelValue` with O(1) lookup and maintain defect sampling semantics. Owner: @engineer
  - Add unit test covering edge cases (out-of-bounds, negative coordinates, near tile borders). Owner: @engineer
