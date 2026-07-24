# ICW-008: Pixelometer Readout and 2x16 Tile Layout

- Status: Done
- Date: 2026-07-23
- Owner: InfiniteCanvas Agent

## Summary

Complete takeover of the in-progress synchronized rendering branch by finishing two requested UX corrections:

1. Default scene layout changed to 32 tiles in a 2-column by 16-row grid.
2. Added a live pixelometer overlay that shows mouse world coordinates with the source image pixel value on the line below.

## Scope

- `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs`
  - Changed default tile layout from 16 columns to 2 columns.
- `src/InfiniteCanvas.Rendering/SampleImageTile.cs`
  - Added world-space pixel sampling API for readout: `TryGetPixelValue(double worldX, double worldY, out byte value)`.
- `src/InfiniteCanvas.App/MainWindow.xaml`
  - Added pixelometer text rows in the viewport overlay.
  - Added viewport `MouseLeave` handling.
  - Updated footer copy to show `(2 x 16)` scene shape.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
  - Added live readout updates on mouse move/wheel.
  - Added readout reset on mouse leave.
  - Added tile pixel sampling + world-space conversion helpers.
  - Kept readout synchronized after render completion.
- `tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs`
  - Updated default layout assertions for 2x16.
  - Added `TryGetPixelValue` behavior test.

## Validation

- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~SampleImageGeneratorTests|FullyQualifiedName~CameraTransformTests"`
  - Passed: 9/9
- `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~ZeroCopyBitmapFactoryTests"`
  - Passed: 4/4
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - Build succeeded

## Findings

- Existing frame publication double-buffering and camera clamp changes were preserved.
- Pixelometer readout is now independent of drag state and updates continuously while the cursor is inside the viewport.
- Pixel sampling uses tile-local coordinate mapping with right/bottom edge exclusivity to avoid boundary ambiguity.

## Next Step

- Address deferred resize overlay behavior noted by user: ensure overlay contents always repaint correctly through rapid resize and during debounce intervals.
