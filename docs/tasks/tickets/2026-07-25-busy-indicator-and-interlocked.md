---
status: proposed
title: Normalize busy indicator state and avoid Dispatcher blocking in Begin/EndBusyOperation
created: 2026-07-25
owner: TBD
priority: P2
scope: src/InfiniteCanvas.App/MainWindow.xaml.cs
validation-command: dotnet build

summary: |
  `BeginBusyOperation` and `EndBusyOperation` use `Dispatcher.Invoke`/`Invoke` indirectly causing potential blocking on UI thread
  when busy operations start/stop from background threads. Use `Dispatcher.BeginInvoke` or `InvokeAsync` and coalesce UI updates.

finding: |
  - `BeginBusyOperation` calls `Dispatcher.Invoke` to show `RenderBusyBar`, and `EndBusyOperation` calls `Dispatcher.Invoke` when collapsing.

evidence:
  - [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L1488-L1502)

root_cause: |
  Use of blocking `Dispatcher.Invoke` in hot code paths that are called from background threads.

proposed_change: |
  - Replace `Dispatcher.Invoke` with `Dispatcher.BeginInvoke`/`InvokeAsync` to avoid blocking the caller thread.
  - Coalesce state transitions to avoid frequent UI updates when many requests happen in short succession.

risks: |
  Low: visual semantics preserved; minor UI timing changes possible.

validation_steps: |
  - `dotnet build`
  - Run app and trigger many rapid RequestRenderAsync calls; verify UI remains responsive and busy indicator reflects state.

next_steps: |
  1. Implement `BeginInvoke` variants for Showing/Collapsing busy UI.
  2. Add unit tests around busy counter logic if possible.
