---
status: proposed
title: Ensure deterministic shutdown order and dispose async resources safely
created: 2026-07-25
owner: TBD
priority: P1
scope: src/InfiniteCanvas.App/**/*.cs
validation-command: dotnet build

summary: |
  Shutdown (`OnClosed`) asynchronously disposes `_renderAction` and cancels `_lifetime`, but other in-flight event subscriptions
  and background tasks may still raise events leading to race conditions during disposal.

finding: |
  - `OnClosed` cancels `_lifetime` and calls `_renderAction.DisposeAsync()` but event handlers like `PixelsGenerated` may still fire.
  - Some dispose paths use synchronous `Dispose()` while others use `DisposeAsync()` leading to mixed async life-cycle handling.

evidence:
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L1360-L1380)
  - [src/InfiniteCanvas.Core/CoalescingAsyncAction.cs](src/InfiniteCanvas.Core/CoalescingAsyncAction.cs#L1-L220)

root_cause: |
  Incomplete shutdown sequencing and missing guards against event callbacks during or after disposal.

proposed_change: |
  - Add `Interlocked` or state-flag guard checks in event handlers to early-return if `_lifetime.IsCancellationRequested` or `_disposed`.
  - Ensure all disposable async resources are awaited in `OnClosed` and unsubscribe events before cancellation where possible.

risks: |
  Medium: incorrect ordering could surface subtle race conditions; changes must be tested thoroughly.

validation_steps: |
  - `dotnet build`
  - Start long-running operations and close the window; ensure no unhandled exceptions during shutdown and process exits normally.

next_steps: |
  1. Add explicit unsubscribe and guards at event entry points.
  2. Harmonize dispose patterns (prefer `DisposeAsync` when resource supports it).
