# ICW-013: Zoom Presets, Cache Debug Controls, and Improved Defect Visualization

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Implement the next interaction and visualization slice:

- larger and more varied defect blobs with class-driven color and aspect-ratio variation
- labels positioned above object top-left with class + object-id text
- enforce uniform-first zoom behavior (no direct independent X/Y user control)
- allow non-uniform clamping only when needed to avoid showing outside scene bounds
- add zoom dropdown presets: percentages, fit-to-width, fit-to-height, custom percent input
- show a bottom indeterminate progress bar while render/regeneration is in-flight
- add debug control to dump image cache and force lazy tile image regeneration as tiles come back in range

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
	- Build succeeded.
- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
	- Passed: 22/22.
- `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`
	- Passed: 4/4.

## Findings

- Added zoom controls to side panel: preset dropdown with `Fit To Width`, `Fit To Height`, percent presets, and `Custom` percent with apply.
- Wheel zoom is now uniform-only from user input; independent axis wheel control was removed.
- Zoom floor behavior is uniform-first and only allows non-uniform correction where needed to keep viewport within scene bounds.
- Added a bottom, thin indeterminate progress bar that appears during rendering/regeneration requests.
- Defects now render larger with broader size variability and aspect-ratio variation; generation uses class-specific colors and larger object extents.
- Sparse defect raster blending now tints defect imagery by annotation class color.
- Labels now render above each object at top-left and use class plus object ID text.
- Added debug button to dump fetched cache summary and reset tile image cache so tiles lazily regenerate when they re-enter range.
- Optimized pixelometer defect sampling by querying the spatial index around the sample coordinate rather than scanning all annotations.

## Next Step

- Implement viewport world-space rulers (left and top), with adaptive tick spacing and camera-coupled numeric labels.
