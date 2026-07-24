# ICW-010: Annotation Modes, Defect Detail Layer, Anchor Pan, and Zoom-Out Clamp

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Implement the next UX and data-fidelity slice for the inspection viewport:

- red annotation outlines with a swappable border animation strategy
- object display modes (outline only default with thickness, fill only, and outline plus 25% fill)
- deterministic sparse defect raster layer generated separately from the base tile at 2x native pixel resolution
- right-mouse-button anchor panning with velocity proportional to pointer distance from anchor point
- zoom-out clamp so the viewport never reveals space beyond scene/image bounds

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

- Sample tiles now lazily compose two raster sources: baseline Gray8 texture and a sparse 2x-resolution defect layer.
- Annotation visuals now support mode-per-object rendering: `Outline` (default), `Fill`, and `OutlineAndFill` (25% fill alpha), with configurable outline thickness.
- Selection border animation is now strategy-driven and swappable through a factory (`MarchingDash` and `PulseOpacity` implementations).
- RMB anchor pan is implemented as timer-driven velocity pan with dead zone and visual anchor marker; existing LMB drag pan remains available.
- Zoom-out now applies a viewport-dependent minimum scale floor before clamping offsets, preventing exposure beyond scene edges.

## Next Step

- Add explicit runtime UI controls (toolbar or keyboard) to switch animation strategy and annotation render presets without code edits.
