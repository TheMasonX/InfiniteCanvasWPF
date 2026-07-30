# InfiniteCanvasWPF — Audit Pass 11 (Same HEAD, Startup/Error-Handling Layer)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since pass 6; verified before writing).
**Scope this pass:** `App.xaml.cs`, `Logging/SerilogHost.cs`, and a count/spot-check of every `async void` handler in `MainWindow.xaml.cs` (21 total) for local exception handling, cross-checked against `ICW-014` (status: In Progress) — the ticket that owns this exact area.

This pass's headline finding doesn't uncover a new bug in isolation — it identifies the mechanism that most plausibly let an *already-found* bug (pass 5's `BitmapConversionDuration` null-deref) ship and run silently before anyone noticed. Framed as a concrete argument for widening `ICW-014`'s in-progress scope, backed by evidence from this audit series rather than speculation.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **The global `DispatcherUnhandledException` handler sets `e.Handled = true` unconditionally for every exception, with only a log line — no user-visible signal of any kind.** Verified by brace-matching every handler body programmatically: **18 of the 21 `async void` handlers in `MainWindow.xaml.cs` contain no `try` block at all** (only `OnLoaded`, `OnResizeElapsed`, and `OnClosed` do). `RequestRenderAsync`'s own catch — used by most of the other 18 — only covers `OperationCanceledException`/`ObjectDisposedException`, so any other exception in the render/input pipeline is logged and silently absorbed while the app keeps running in whatever partially-updated state the exception left it in. This is the exact mechanism that would have made pass 5's `BitmapConversionDuration!.Value` crash (confirmed real, confirmed fixed one commit later) invisible to anyone not actively tailing the log file — no crash, no dialog, just quietly-stopped status bar/scrollbar/pixelometer updates. `ICW-014` (In Progress) already tracks "logged centrally," but its acceptance criteria say nothing about surfacing failures to the *user*, and its "Next Step" note (shared async-void wrapper) doesn't mention it either. | High | 88% |
| 2 | Confirmed accurate (unlike `ICW-015`'s claim from pass 9): `ICW-014`'s findings notes say the logging host *"falls back to file-only logging if the Event Log sink cannot be initialized."* Verified directly in `SerilogHost.CreateLogger()` — the `EventLog` sink registration is wrapped in its own try/catch with a `Debug.WriteLine` fallback message, and file/Trace sinks are configured before that try/catch runs, so they're unaffected either way. Noted for balance — not every ticket's self-report in this codebase has turned out to be incomplete. | — (informational) | 90% |

---

## 1. [HIGH] Global exception handling swallows failures with zero user-visible signal — and covers most of the UI pipeline by default

**Confidence: 85%**

```csharp
// App.xaml.cs:30-34
private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
{
    Log.Error(e.Exception, "Unhandled exception on the WPF dispatcher");
    e.Handled = true;      // <-- unconditional; app continues no matter what threw
}
```
No severity check, no exception-type filter, no user-facing dialog, status-bar message, or even a `Debug.Assert` in a debug build — every dispatcher-thread exception, of any kind, from anywhere in the app, is logged and discarded, and execution continues as if nothing happened.

This alone would be a reasonable, deliberate trade-off for a desktop app that shouldn't crash on every minor fault — and `ICW-014`'s acceptance criteria (*"logs failures centrally... without crashing the process silently"*) confirms that's exactly the intent, and it's explicitly tracked, not an oversight. What makes it worth flagging now rather than deferring entirely to that ticket is what it's actually catching in practice:

```
21 async void handlers in MainWindow.xaml.cs, checked programmatically (brace-matched method bodies,
searched for any `try`): 18 have none — OnLoaded, OnResizeElapsed, and OnClosed are the only three
that do. RequestRenderAsync's own handling (called from most of the other 18) only catches
OperationCanceledException and ObjectDisposedException — everything else propagates up through the
async void handler to the dispatcher, i.e., to the handler above.
```
That means the safety net isn't a narrow backstop for truly exceptional conditions — it's the *default* error-handling strategy for essentially the entire mouse/keyboard/timer-driven UI pipeline (pan, zoom, scroll, resize, regenerate-click, debug-dump-click, and more), because none of those handlers have their own handling beyond the two cancellation-related exception types `RequestRenderAsync` already covers.

**This is not hypothetical** — this audit series already found and confirmed a real instance: pass 5's `tile.BitmapConversionDuration!.Value` null-dereference (`InvalidOperationException`, thrown from inside `RenderFrameAsync`, called from `DispatchRenderFrameAsync`, invoked via the render pipeline these handlers all funnel into). Had that bug not been independently fixed one commit later, it would have thrown on literally every render frame after the first tile finished generating — and under the current global-handler design, every one of those exceptions would have been logged and silently swallowed, leaving the zoom/scrollbar/pixelometer updates permanently stalled with no error dialog, no crash, and no visible symptom beyond "some of the UI stopped updating," which is easy to miss in casual testing and easy to misattribute to something else entirely if noticed later.

**Recommendation, scoped to fit inside `ICW-014` rather than as a new ticket:** add one acceptance criterion to `ICW-014` — some minimal, generic, non-blocking user-visible signal on dispatcher-level exceptions (a status-bar message, a transient toast, even just re-purposing the existing `INITIALIZATION FAILED`-style overlay pattern already used in `OnLoaded`'s catch block for a "something went wrong, see logs" message). This doesn't require catching or classifying every exception type — it only requires the *existing* global handler to do one more thing besides logging. Pair with `ICW-014`'s already-planned shared async-void wrapper so individual handlers can opt into more specific recovery where it matters (e.g., render failures could retry) while everything else still falls through to the now-more-visible global net.

---

## 2. Confirmed accurate: `ICW-014`'s EventLog-fallback claim holds up

**Confidence: 90%**

```csharp
// SerilogHost.cs:34-41
try
{
    configuration.WriteTo.EventLog("InfiniteCanvas", manageEventSource: true, restrictedToMinimumLevel: LogEventLevel.Warning);
}
catch (Exception exception)
{
    System.Diagnostics.Debug.WriteLine($"Falling back to file-only logging because the EventLog sink could not be initialized: {exception.Message}");
}
```
File and Trace sinks are added to `configuration` *before* this block, so a failed `EventLog` registration only drops that one sink — logging as a whole degrades gracefully exactly as `ICW-014`'s findings section describes. Included here for balance: this audit series has now checked two tickets' self-reported claims against code (`ICW-015` in pass 9, incomplete; `ICW-014` here, accurate) — worth noting both outcomes rather than only ever reporting the discrepancies.

---

## Suggested Priority

1. **§1** — fold into `ICW-014` (already In Progress, already the right owner) as an added acceptance criterion rather than a new ticket. Cheap relative to the rest of that ticket's scope, and directly addresses a failure mode this audit series has already caught happening once.

## Assumptions & Open Questions

- §1's severity assumes "silent degradation with no user feedback" is worse than the alternative the ticket is guarding against ("crashing the process silently" — presumably meaning an unhandled crash with no log at all, which is worse still). Both readings agree logging is strictly better than nothing; the open question is only whether *user-visible* signaling should be added on top, which is a product/UX call as much as an engineering one — flagging the evidence, not asserting the product decision.
- The 18-of-21 figure is a mechanical `try`-keyword-presence check over brace-matched method bodies, not a semantic read of each handler — it would flag a handler with a stray unrelated `try` (none found) and can't tell whether the three that do have one actually cover the exceptions that matter (`OnLoaded`'s does, confirmed in pass 9; `OnResizeElapsed` and `OnClosed` were not individually re-verified this pass). The overall conclusion — most handlers rely entirely on the global handler — doesn't depend on that nuance.
- As with all prior passes, static source review only; no build or test execution was performed.
