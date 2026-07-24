# Handoff: Render Request Coalescing

Date: 2026-07-23

## Current state

The repository has a runnable .NET 10 WPF MVP. It loads 100,000 deterministic spatial records, accepts 250 live records every 500 ms, publishes a packed STR snapshot every two seconds, and renders visible points through the Kernel32-backed `ZeroCopyBitmapFactory`.

This slice prevents rapid pan, zoom, resize, and live-update events from creating an unbounded queue of stale frames.

## Implemented in this slice

- Added `CoalescingAsyncAction` in `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs`.
- The action permits one active execution and collapses any number of requests received during it into one follow-up execution.
- The core coalescer is synchronization-context agnostic. `MainWindow.DispatchRenderFrameAsync` explicitly marshals every frame through the WPF dispatcher.
- Disposal cancels the active execution, clears pending work, waits for completion, and rejects later requests.
- Replaced the `SemaphoreSlim` render queue in `src/InfiniteCanvas.App/MainWindow.xaml.cs` with the coalescing action.
- Render cancellation now flows into the background query/projection/bitmap task.
- Window shutdown waits for the active render to stop before clearing the `Image.Source` and disposing the memory-mapped bitmap factory.
- Added focused tests in `tests/InfiniteCanvas.Tests/CoalescingAsyncActionTests.cs` for burst coalescing and disposal behavior.

## Behavioral contract

When the first render is blocked and three more requests arrive, the renderer runs exactly twice: the active frame and one follow-up frame. Every caller receives the shared processing task, so callers awaiting any coalesced request observe completion of the whole active/pending cycle.

`DisposeAsync` is idempotent. It suppresses the expected cancellation internally while the task returned to an earlier `RequestAsync` call remains canceled. Calling `RequestAsync` after disposal throws `ObjectDisposedException`.

## Validation

Run from the repository root:

```powershell
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Debug --filter FullyQualifiedName~CoalescingAsyncActionTests
dotnet test .\InfiniteCanvasWPF.slnx --configuration Release
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
```

Last verified results:

- Focused scheduler tests: 2 passed.
- Full solution tests: 16 passed, 0 failed.
- Release WPF app build: succeeded with 0 warnings and 0 errors.
- Process smoke test: the tracked Release app remained alive and responsive through multiple 500 ms live-update ticks.

The VS Code file-scoped test runner reported "No tests found" for this repository; use `dotnet test` and NUnit filters instead.

## Next recommended slice

Add a repeatable BenchmarkDotNet project rather than timing assertions in NUnit. Measure these paths independently:

1. `StrTreeSpatialIndexService.Query` with 100,000, 1 million, and 10 million uniformly distributed records and viewport selectivities near 0.1%, 1%, and 10%.
2. `LiveSpatialIndexService.Query` with snapshot-only data and with representative hot/publishing buffers.
3. World-to-screen projection plus `ZeroCopyBitmapFactory.GenerateFrozenBitmap` on Windows, recording managed allocations and frame latency.
4. Snapshot rebuild duration and transient managed memory at the expected 2 Hz ingestion cadence.

Do not turn the design's 10,000 queries/second or 16 ms frame targets into ordinary unit-test thresholds; those would be machine-sensitive and flaky. Store benchmark artifacts outside source control unless the repository later adopts a baseline policy.

## Known follow-up risks

- `MainWindow.RenderFrameAsync` queries the spatial index to render and then `CanvasViewportViewModel.RefreshCommand` queries it again to update counts. Return frame statistics from the render operation or add a lightweight view-model update API to remove the duplicate query.
- Do not rely on ambient `SynchronizationContext` inside `CoalescingAsyncAction`. A smoke test caught follow-up frames running off-dispatcher; WPF ownership is now enforced explicitly by `MainWindow.DispatchRenderFrameAsync`.
- The viewport is captured before the background query, but each point currently calls the live `CameraTransform`. Input arriving during a frame can therefore project with a newer transform than the queried viewport. Capture an immutable camera snapshot per frame before deeper performance work.
- `InteropBitmap` references the factory-owned mapping. Keep the factory alive while its bitmap is assigned to WPF, clear `Image.Source` before disposal, and do not reuse a disposed factory.
- Rendering currently overwrites duplicate pixels. At large zoom-out levels, benchmark overdraw before choosing deduplication, accumulation, heatmap rendering, or a GPU path.
- Resize buffers are capped at 4096 pixels per dimension. Review DPI behavior and 4K/5K requirements before treating that as a production limit.

## Working tree guidance

The WPF app, Windows test project, solution entries, README updates, and `DesignDoc.md` were already present as uncommitted work when this slice began. Preserve them. Do not revert unrelated existing changes while continuing the benchmark or camera-snapshot work.
