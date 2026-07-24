# ICW-039: Startup Responsiveness via Non-Blocking Rendering and Shared Defect Tiles

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Implement the user-mandated startup responsiveness slice:

- remove CPU-heavy per-annotation defect raster resampling
- use a shared sparse defect template pool (default 64) and assign one template per annotation
- keep annotation bounds as logical placeholders while centering sparse defect imagery within each annotation bounds
- use simple GDI+ shape-based defect template generation and fast solid tile background generation
- ensure render never blocks waiting for tile image input by using non-blocking placeholders and asynchronous tile fetch completion rerenders

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
- tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release
  - Passed: 23/23.
- dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release
  - Passed: 4/4.
- dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
  - Build succeeded.

## Findings

- Replaced per-annotation `ResampleTemplate` path with direct reuse of defect template pixels, removing a high-cost bilinear interpolation hotspot from startup generation.
- `SampleImageTile` now exposes a non-blocking pixel path that returns immediate placeholder values and kicks off background image generation once per tile.
- The main window subscribes to tile generation completion and issues coalesced rerenders so imagery upgrades from placeholder to generated content without blocking first-frame render.
- Defect imagery is now centered inside annotation bounds and treated as linked sparse imagery rather than stretched full-bounds overlays, matching placeholder-linked spatial-data demo intent.
- Added focused generator test coverage for accurate per-parameter `ArgumentOutOfRangeException.ParamName` reporting, advancing ICW-015 as a high-ROI side fix.

## Next Step

- Run a targeted startup profiling pass and compare first-frame latency before/after this slice to quantify impact and guide the next optimization phase.
