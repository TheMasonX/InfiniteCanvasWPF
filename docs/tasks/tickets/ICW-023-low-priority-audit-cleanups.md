# ICW-023: Low-Priority Audit Cleanup Batch

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Batch approved low-risk cleanup items from audit findings into one controlled, regression-safe pass.

## Scope

- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- src/InfiniteCanvas.Core/CameraTransform.cs
- src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
- src/InfiniteCanvas.App/MainWindow.xaml
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Findings

- Cross-validated findings include duplicated formulas/helpers, duplicated defaults, magic epsilon naming, and finalizer lock pattern cleanup.
- Additional net-new nits to include in this batch: `UnmapViewOfFile` return value is ignored in disposal, integer parsing culture invariance is inconsistent in generation controls, and degenerate bounds in render sampling loops rely on implicit clamp behavior rather than explicit guards.
- `Bgra32BufferLayout.GetPixelOffset` uses a combined guard that always attributes failures to `x`, mirroring the same parameter-attribution anti-pattern already tracked elsewhere.

## Next Step

- Triage and sequence quick wins with zero behavior change expectations and targeted tests.
