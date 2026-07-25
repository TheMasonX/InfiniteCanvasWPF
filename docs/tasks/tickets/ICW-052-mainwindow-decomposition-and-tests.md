---
status: proposed
summary: Extract testable logic from MainWindow and add unit tests
scope:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/ViewportZoomCalculator.cs (new)
  - src/InfiniteCanvas.App/GenerationOptionsValidator.cs (new)
  - tests/InfiniteCanvas.Tests/ViewportZoomCalculatorTests.cs (new)
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter FullyQualifiedName~ViewportZoomCalculatorTests
findings_evidence: |
  - `MainWindow.xaml.cs` contains >1000 lines of mixed pure math and UI wiring (audit findings). No unit tests cover `MainWindow` logic.
  - Pure functions: zoom presets, dead-zone math, fit-to-width/height, and generation input parsing are deterministic and safe to extract.
next_steps:
  - Create `ViewportZoomCalculator` with public methods for `ComputeUniformZoomDelta`, `ApplyScaleWithUniformFirst`, and `ComputeMinimumZoom`. Owner: @engineer
  - Create `GenerationOptionsValidator` to parse and validate tile/row/objects inputs with invariant culture parsing. Owner: @engineer
  - Add focused unit tests for each operation. Owner: @engineer
  - Update `MainWindow` to call these helpers; keep behavior identical. Owner: @engineer
