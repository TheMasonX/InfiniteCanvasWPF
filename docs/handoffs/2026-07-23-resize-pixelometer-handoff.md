# 2026-07-23 Handoff: 2x16 Scene, Pixelometer, and Resize Overlay Sync

## Status

- Branch: `main`
- Scope completed:
  - 32-tile default scene now uses a 2-column x 16-row layout.
  - Pixelometer overlay reports mouse world coordinates and source pixel value.
  - Resize debounce path now keeps raster image and annotation overlay synchronized by scaling the complete last frame while waiting for rerender.

## Implemented Changes

- `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs`
  - Default `columns` changed from `16` to `2`.
- `src/InfiniteCanvas.Rendering/SampleImageTile.cs`
  - Added `TryGetPixelValue(double worldX, double worldY, out byte value)`.
- `src/InfiniteCanvas.App/MainWindow.xaml`
  - Added pixelometer overlay rows.
  - Added `MouseLeave` handler.
  - Changed `FramePresenter` to `Viewbox` with `Stretch=Fill`.
  - Footer updated to `32 TILE INSPECTION SCENE (2 x 16)`.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
  - Added hover tracking and live pixelometer updates.
  - Added reset-on-leave behavior.
  - Updated frame construction to include explicit frame dimensions.
  - Frame visuals are now sized to render dimensions so image+overlay scale together during host resize.
- `tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs`
  - Updated default layout assertions for 2x16.
  - Added pixel sampling behavior test.

## Validation Evidence

- `dotnet test .\InfiniteCanvasWPF.slnx --configuration Release`
  - Passed: 24/24 tests.
- Additional focused checks were also run:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`.
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~ZeroCopyBitmapFactoryTests"`.

## Task Tracking Updates

- `docs/tasks/active-tasks.md`
  - ICW-008 marked done.
  - ICW-009 added and marked done.
- `docs/tasks/JIRA.md`
  - ICW-008 and ICW-009 logged in task table and activity history.
- `docs/tasks/tickets/ICW-008-pixelometer-and-2x16-grid.md`
- `docs/tasks/tickets/ICW-009-resize-overlay-sync.md`

## Open Questions / Next Candidate Tasks

1. Evaluate whether `Stretch.Fill` distortion during aggressive aspect-ratio changes is acceptable for inspection users, or whether an optional letterboxed mode (`Stretch.Uniform`) should be exposed.
2. ICW-005: define explicit DPI-aware resize and max surface policy, especially for high-DPI 4K/5K monitors.
3. ICW-007: profile and potentially pool retained annotation overlay elements if visible density increases.
