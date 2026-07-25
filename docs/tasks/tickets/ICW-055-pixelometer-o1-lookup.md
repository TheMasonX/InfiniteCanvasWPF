---
status: done
summary: Replace pixelometer linear tile scan with O(1) grid lookup
scope:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - tests/InfiniteCanvas.Tests/*
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter FullyQualifiedName~TryReadPixelValue
findings_evidence: |
  - Added `TileGridIndexLookup.TryGetTileIndex` to compute row-major tile indices from world coordinates.
  - Updated `MainWindow.TryReadPixelValue` to use direct index lookup instead of scanning every tile.
  - Added `TileGridIndexLookupTests` covering normal lookup, half-open edges, and invalid inputs.
next_steps:
  - None. Keep closed; fold any future pixelometer/render parity work into ICW-035.
