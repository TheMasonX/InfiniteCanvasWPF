---
status: proposed
title: Harden async event handlers and introduce SafeFireAndForget
created: 2026-07-25
owner: TBD
priority: P1
scope: src/InfiniteCanvas.App/**/*.cs, src/InfiniteCanvas.ViewModels/**/*.cs
validation-command: dotnet build

summary: |
  Many UI event handlers are implemented as `async void` and directly await long-running tasks.
  This leads to unobserved exceptions, fragile shutdown behavior, and cancellation handling gaps.

finding: |
  Event handlers such as `MainWindow.OnLoaded`, `OnAnnotationMouseLeftButtonDown`, `OnViewportMouseMove`,
  `OnAnchorPanTick`, and many control change handlers are `async void` and await work that can throw or
  be in-flight during shutdown.

evidence:
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L80-L96)
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L430-L439)
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L700-L760)
  - [src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs](src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs#L1-L120)

root_cause: |
  WPF event handlers are `void` signatures; developers used `async void` for convenience without a
  centralized fire-and-forget helper to observe/capture exceptions and tie cancellation to application lifetime.

proposed_change: |
  - Add `TaskExtensions.SafeFireAndForget(this Task task, Action<Exception>? onException = null)` helper.
  - Convert long-running `async void` handlers to thin `void` handlers that call an `async Task` worker via `SafeFireAndForget`.
  - Ensure `SafeFireAndForget` reports to `Serilog` and links to the main `_lifetime` cancellation token when available.

risks: |
  Low: behavior preserved; risk is missing a conversion site and leaving a handler unchanged.

validation_steps: |
  - `dotnet build`
  - Run app, exercise load/regenerate/drag/zoom flows.
  - Inject a temporary exception inside a background path and confirm it's logged but does not crash the process.

next_steps: |
  1. Implement helper at `src/InfiniteCanvas.App/Utilities/TaskExtensions.cs`.
  2. Update handlers in `MainWindow.xaml.cs` to call `SafeFireAndForget`.
  3. Add unit tests for `SafeFireAndForget` logging behavior.
