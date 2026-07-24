# ICW-035: Renderer And Pixelometer Blend Contract

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent
- Priority: P1

## Summary

Unify defect blending and sampling semantics between the raster renderer and pixelometer readout to remove user-visible value mismatches over annotated defect pixels.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- tests/InfiniteCanvas.Tests
- tests/InfiniteCanvas.Windows.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- Renderer applies class-tinted blend channels in `ZeroCopyBitmapFactory`, while pixelometer still uses legacy grayscale subtraction in `MainWindow`.
- Divergence creates systematic mismatch between on-screen pixel color and reported pixelometer value.
- Blend and world-to-pixel sampling logic remain duplicated across multiple classes, increasing future drift risk.

## Next Step

- Extract one shared blend/sampling helper consumed by renderer and pixelometer, then add parity tests proving sampled defect values match rendered output expectations.
