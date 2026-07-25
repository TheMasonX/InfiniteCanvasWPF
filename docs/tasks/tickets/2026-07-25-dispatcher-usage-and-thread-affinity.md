---
status: proposed
title: Verify Dispatcher usage and UI thread affinity in rendering and view-model updates
created: 2026-07-25
owner: TBD
priority: P1
scope: src/InfiniteCanvas.App/**/*.cs, src/InfiniteCanvas.ViewModels/**/*.cs
validation-command: dotnet build

summary: |
  Several code paths invoke UI operations from background contexts or rely on `Dispatcher.InvokeAsync` without
  clear synchronization. Ensure all UI-affine state changes happen on the Dispatcher and heavy work runs off-thread.

finding: |
  - `DispatchRenderFrameAsync` conditionally calls `RenderFrameAsync` using `Dispatcher.InvokeAsync`.
  - `OnTilePixelsGenerated` uses `Dispatcher.InvokeAsync` to call `RequestRenderAsync`, which itself performs
    busy counting and awaits a coalescing action potentially causing re-entrancy.

evidence:
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L340-L356)
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L210-L230)

root_cause: |
  Mixed synchronous and asynchronous Dispatcher usage and lack of documented dispatcher-affine contracts in helpers.

proposed_change: |
  - Centralize Dispatcher-bound UI updates behind small helper methods like `RunOnUiThread(Action)`.
  - Ensure `RequestRenderAsync` does not rely on being called from any particular thread.
  - Add XML docs to `CoalescingAsyncAction` that it may call back on any thread and callers must marshal to UI thread when mutating UI.

risks: |
  Low: primarily code clarity and avoiding rare re-entrancy bugs.

validation_steps: |
  - `dotnet build`
  - Stress test rapid tile generation and UI interactions; verify no cross-thread exceptions (InvalidOperationException) are thrown.

next_steps: |
  1. Add `RunOnUiThread(Action)` helper and replace inline `Dispatcher.InvokeAsync` usages where appropriate.
  2. Add docs and small assertions for dispatcher-affine operations.
