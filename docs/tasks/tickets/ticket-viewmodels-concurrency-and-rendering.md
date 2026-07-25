---
status: todo
summary: Harden ViewModels and App-level concurrency, lifecycle, and rendering resource ownership
scope:
  - Fix async event handler patterns in `src/InfiniteCanvas.App/MainWindow.xaml.cs`
  - Avoid UI-thread deadlocks during shutdown and render coalescing
  - Stabilize `ZeroCopyBitmapFactory` ownership and delayed disposal
  - Reduce per-frame UI allocation pressure in `BuildFrameVisual`
validation-command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
  dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release
  (Manual) Run the MVP and verify: open window, regenerate scene, pan/zoom, then close repeatedly without hangs or crash
next-step: |
  1. Implement the minimal changes described in the audit ticket 'viewmodels-concurrency-and-rendering' as protective patches (try/observe, delayed dispose, limited UI allocations).
  2. Add unit tests that exercise `OnClosed` disposal ordering and `RenderFrameAsync` dispatch flow.
  3. Run the validation commands and iterate on any failures.
---

Description
-----------

This ticket captures a small, high-impact set of fixes to harden the WPF application lifecycle and viewmodel-related rendering logic. Addressing these points prevents subtle deadlocks, access violations from disposing unmanaged image buffers prematurely, and high GC/alloc pressure from per-frame element construction.
