---
id: ICW-110-detailed
author: External Audit (Integration-1)
key: ICW-110
title: Audit and convert async void handlers to safe wrappers (detailed implementation plan)
status: Done
type: Bug
priority: P1
tags:
  - stability
  - runtime
  - async-void
  - safety
  - app
dependsOn:
  - ICW-014
related:
  - ICW-110
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/App.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-08-06
---

# ICW-110 — Audit and convert `async void` handlers to safe wrappers (detailed plan)

## Summary

**Audit finding (80% confidence):** 21 `async void` handlers exist in `MainWindow.xaml.cs`. None have local try/catch beyond the global dispatcher handler. The global dispatcher handler sets `e.Handled = true` unconditionally with zero user-visible signal — meaning exceptions in these handlers are silently swallowed. This mechanism made a real crash (`BitmapConversionDuration!.Value` NullReferenceException) invisible to users.

Additionally, `SampleImageGenerator`/`AnnotationGenerator` generation paths have no `CancellationToken` parameter or check anywhere in the hot loop (covered by ICW-P1-COOPERATIVE-CANCEL).

## Root Cause

WPF event handlers must return `void` (not `Task`). When marked `async`, any unhandled exception escapes to the dispatcher's unhandled exception handler. The current global handler at `App.xaml.cs` logs the exception but sets `e.Handled = true` without any user-visible feedback — the app appears to do nothing on failure.

## Scope

### Required Changes

1. **Inventory all `async void` handlers** in `src/InfiniteCanvas.App/MainWindow.xaml.cs`:
   - List every handler with its event type, line number, and approximate risk (crash hazard, silent data loss, cosmetic).
   - Prioritize by risk: handlers in render/camera/input paths first.

2. **Replace with `async Task` where WPF wiring permits:**
   - Some WPF events (e.g., `Loaded`, `Closed`) can be wired to `async Task` handlers via `async void` wrappers at the binding site.
   - For pure MVVM commands (`ICommand`), use `AsyncRelayCommand` instead of `async void`.

3. **Implement a `SafeAsyncEventHandler` wrapper** for unavoidable `async void` entry points:
   ```csharp
   public static class SafeAsyncHandler
   {
       public static async void Handle(Func<Task> handler, ILogger logger)
       {
           try
           {
               await handler();
           }
           catch (OperationCanceledException)
           {
               // Expected during shutdown — no user signal needed
           }
           catch (Exception ex)
           {
               logger.Error(ex, "Unhandled exception in async event handler");
               // Show user-visible signal: status bar message or toast
               await ShowStatusBarErrorAsync(ex.Message);
           }
       }
   }
   ```

4. **Add user-visible signal on dispatcher exceptions:**
   - The global dispatcher handler (`App.xaml.cs`) should set a status bar message or show a toast in addition to logging.
   - Without this, all dispatcher exceptions are invisible to users.

5. **Add unit/integration tests** asserting thrown exceptions are caught and logged:
   - Create `SafeAsyncHandlerTests` with representative handlers (mouse, keyboard, timer-based).
   - Assert exception is logged and user-visible signal is produced.

### Acceptance Criteria

- No unchecked `async void` handlers remain except where WPF signature prevents change.
- All remaining `async void` handlers use `SafeAsyncHandler.Handle()` wrapper.
- Global dispatcher handler produces user-visible signal on exception.
- Integration tests assert exception containment for representative handlers.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Convert or wrap all 21 `async void` handlers |
| `src/InfiniteCanvas.App/App.xaml.cs` | Add user-visible signal to global dispatcher handler |
| `src/InfiniteCanvas.App/Helpers/SafeAsyncHandler.cs` (new) | Create safe wrapper class |
| `tests/InfiniteCanvas.Tests/SafeAsyncHandlerTests.cs` (new) | Add exception-containment tests |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "SafeAsyncHandler|AsyncVoid"
```

## Completion Evidence

All MainWindow async event entry points now call `SafeAsyncEventHandler.Handle`. The task bodies use `Task` methods. The wrapper logs cancellation and failures and reports failure text in the status bar. The dispatcher handler reports unhandled UI exceptions through the same status surface. App Release build passes.

## Notes

- Related to ICW-014 (global exception safety net). The global handler exists but lacks user-visible feedback.
- Recommend implementing this after ICW-014 is stable.
