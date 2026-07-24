# ICW-011: Sparse Object Images, Global Annotation Display Options, and Axis-Clamped Zoom

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Apply requested UX and data-model corrections:

- sparse image data should live inside object bounds (annotation patches), not as a tile-wide fill
- annotation draw style should be controlled by one display option set rather than per-object flags
- display options should include mode, outline width, label size, and label visibility
- anchor pan direction should match scroll intuition (pointer down scrolls down)
- support non-uniform zoom and clamp each axis independently on zoom out

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

- Defect imagery now exists as sparse per-annotation patches (`DefectPixels`) mapped to each annotation bounds, so image detail appears only within areas of interest.
- Annotation draw behavior now uses one global option set (`AnnotationDisplayOptions`) controlling display mode, outline width, label size, and label visibility.
- Right-button anchor pan direction now matches scroll intuition by applying inverse offset deltas from anchor displacement.
- Wheel zoom now supports non-uniform scaling: default uniform, `Shift` for X-only, and `Ctrl` for Y-only.
- Zoom-out clamps per axis independently, allowing one axis to remain fixed at scene-fit minimum while the other keeps zooming out.

## Next Step

- Add runtime controls in the top panel to edit annotation display options live (mode dropdown, thickness slider, label size slider, and labels toggle).
