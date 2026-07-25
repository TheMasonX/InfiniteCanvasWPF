---
status: proposed
title: Improve ViewModel command cancellation and error propagation
created: 2026-07-25
owner: TBD
priority: P2
scope: src/InfiniteCanvas.ViewModels/**/*.cs
validation-command: dotnet build

summary: |
  `CanvasViewportViewModel.RefreshAsync` is implemented as a `[RelayCommand]` async Task with a `CancellationToken` parameter,
  but the generated command type and call sites may not pass cancellation tokens or surface errors to the UI.

finding: |
  - `RefreshAsync(CancellationToken)` relies on callers to provide a cancellation token; generated RelayCommand may not.
  - No visible error state is exposed for command failures; callers typically call `RequestRenderAsync` and swallow exceptions.

evidence:
  - [src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs](src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs#L1-L120)

root_cause: |
  Partial use of CommunityToolkit MVVM `RelayCommand` with async patterns but no explicit `IAsyncRelayCommand` usage, nor coordination for cancellation.

proposed_change: |
  - Explicitly expose `IAsyncRelayCommand` for refresh and provide an internal CancellationTokenSource for UI-cancel semantics.
  - Surface `IsRefreshing` and `RefreshError` observable properties for UI feedback.

risks: |
  Low: UI gains better feedback; requires updating any binding consumers that relied on implicit command shape.

validation_steps: |
  - `dotnet build`
  - Attach to UI and call refresh; cancel mid-flight and verify `IsRefreshing` toggles and no orphaned tasks.

next_steps: |
  1. Update `CanvasViewportViewModel` to expose `IAsyncRelayCommand RefreshCommand` and properties `IsRefreshing`, `LastError`.
  2. Update bindings if required and add unit tests for cancellation behavior.
