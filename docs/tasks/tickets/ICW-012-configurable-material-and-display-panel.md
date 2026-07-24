# ICW-012: Configurable Tile Material, Side Panel Display Options, and Regeneration

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Implement the next demo-control and generation slice:

- global annotation display mode choice (`Outline` default, `Fill`, `OutlineAndFill`) from a runtime control
- side panel with display options and generation options
- regenerate button to rebuild demo material at runtime
- default material layout of `2 x 32` tiles (64 total), configurable via controls
- startup camera behavior switched to fit-to-width
- defect blobs generated from a deterministic pool of 64 bitmap-derived templates and sampled into sparse annotation patches
- background tiles generated lazily from bitmap-backed sources so imagery is fetched as navigation reaches new vertical regions

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
- tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
	- Passed: 22/22.
- `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`
	- Passed: 4/4.
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
	- Build succeeded.

## Findings

- Added a right-side display options panel with runtime controls for global annotation mode, outline width, label size, label visibility, tile grid dimensions, objects-per-tile, and regenerate action.
- Startup now regenerates the scene and applies fit-to-width camera initialization so vertical traversal fetches additional tile imagery as needed.
- Tile generation now defaults to 64 tiles (2 x 32) and supports configurable X by Y material dimensions.
- Defect imagery now comes from a deterministic pool of 64 centered, less-feathered bitmap-derived templates that are sampled into sparse per-annotation patch data.
- On Windows targets, background tile grayscale pixels are generated from lazily fetched bitmap sources and converted on first use.

## Next Step

- Evaluate adding cancellable off-screen prefetch and progress reporting for large tile grids during sustained vertical anchor-pan.
