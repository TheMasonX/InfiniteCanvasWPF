# ICW-009: Keep Overlay and Raster Synchronized During Resize

- Status: Done
- Date: 2026-07-23
- Owner: InfiniteCanvas Agent

## Summary

During window resize, the previous frame remained visible until the debounce timer published a new frame, but annotation bounds were laid out in absolute coordinates while the image stretched with the host. This produced temporary overlay drift.

This task makes frame presentation resize-safe by publishing each frame through a stretch container and pinning the frame visual to the render dimensions so image and annotations scale together between debounced renders.

## Scope

- `src/InfiniteCanvas.App/MainWindow.xaml`
  - Replaced `FramePresenter` from `Border` to `Viewbox` with `Stretch=Fill`.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`
  - Updated frame visual construction to include explicit frame dimensions.
  - Set root frame grid width/height to rendered dimensions so the presenter can scale both layers uniformly.

## Validation

- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - Build succeeded.
- `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release --filter "FullyQualifiedName~ZeroCopyBitmapFactoryTests"`
  - Passed: 4/4.

## Findings

- The resize mismatch was a presentation-layer issue, not a camera math issue.
- Keeping the previous complete frame visible still avoids flicker; scaling the full frame keeps alignment correct while waiting for the new frame.

## Next Step

- Evaluate whether `Stretch.Fill` distortion during aggressive aspect-ratio changes is acceptable for inspection workflows, or if letterboxing (`Stretch.Uniform`) should be offered as an option.
