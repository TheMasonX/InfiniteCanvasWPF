# Handoff: viewport scrollbars, zoom-aware scrolling, and configurable background tuning

Date: 2026-07-25

## Summary
The current slice adds two user-facing improvements that were requested in the latest task intake:

- A scrollable viewport host that reflects camera pan/zoom state through the viewport scrollbars.
- Configurable background tile tuning so the synthetic tile noise and defect-circle pattern can be adjusted at runtime and persisted across sessions.

## What changed
- Added a dedicated viewport scroll policy in [src/InfiniteCanvas.Core/ViewportScrollPolicy.cs](../../src/InfiniteCanvas.Core/ViewportScrollPolicy.cs) to compute content size and scroll offsets from camera state.
- Reworked the main viewport host in [src/InfiniteCanvas.App/MainWindow.xaml](../../src/InfiniteCanvas.App/MainWindow.xaml) and [src/InfiniteCanvas.App/MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) to host a scrollable content surface that remains synchronized with pan and zoom.
- Extended the background generator in [src/InfiniteCanvas.Rendering/SampleImageGenerator.cs](../../src/InfiniteCanvas.Rendering/SampleImageGenerator.cs) so noise and defect circles can be configured explicitly instead of being hardcoded.
- Persisted the new tuning values through [src/InfiniteCanvas.Core/CanvasUserSettings.cs](../../src/InfiniteCanvas.Core/CanvasUserSettings.cs) and exposed them in the display controls of [src/InfiniteCanvas.App/MainWindow.xaml](../../src/InfiniteCanvas.App/MainWindow.xaml).
- Added regression coverage in [tests/InfiniteCanvas.Tests/ViewportScrollPolicyTests.cs](../../tests/InfiniteCanvas.Tests/ViewportScrollPolicyTests.cs) and [tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs](../../tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs).

## Validation
The following commands were run successfully:

- dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Debug
- dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release

## Follow-up recommendations
- The next high-value slice is a lightweight debug inspector for generator and viewport settings.
- After that, the priority should shift to exception-safety and shutdown hardening for the app lifecycle.
- The current implementation is intentionally scoped to interaction polish and runtime tuneability; it does not yet add a full property editor or licensing dialog.
