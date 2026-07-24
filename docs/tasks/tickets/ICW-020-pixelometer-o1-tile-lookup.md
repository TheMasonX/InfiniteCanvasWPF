# ICW-020: Pixelometer O(1) Tile Lookup

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Replace per-mouse-move linear tile scan with direct grid index arithmetic while preserving defect-sample correctness.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- tests/InfiniteCanvas.Tests

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: pixelometer sampling currently scales linearly with tile count.

## Next Step

- Add coordinate-to-tile index mapping by tile dimensions and columns, then validate boundary conditions.
