# InfiniteCanvasWPF — Latest-Commits Audit, Pass 3

**HEAD audited:** `62d1ce6001f57f4a9d55a0c8fcde80fd57cb47ab` ("Register Sonar-driven code quality tickets and normalize ticket metadata")
**Previously audited (my report):** `1f291b9220c7b907abfcdbd5662421c1e46f1ec4`
**8 new commits reviewed:** `addbd18` (harden tile cache/render coalescing) → `4ad0245` (viewport scrollbars + tile tuning) → `bc339ce` (task-tracker workflow docs) → `7524b88` (human commit: "I cleaned up the mainwindow xaml") → `52a3442` (next wave of high-ROI tasks) → `76c1960` (scrollbars, tile mips, exception safety net) → `9f96fe5` (ICW-036 research) → `62d1ce6` (Sonar tickets + metadata normalization).
**Method:** Tarball diff against my previously-audited tree; read every new/changed source file in full; read all new tickets (ICW-050 through ICW-082, ICW-305) and the current `task-tracker.md` (61 rows) before writing anything.

---

## 0. State of the Backlog — This Is Now a Self-Auditing Codebase

Since my last pass, the project's own internal audit loop found and ticketed: a stale-frame-publication race (ICW-078), busy-state churn under rapid input (ICW-079), annotation-feature formatting coupling (ICW-080), a background-image-visibility persistence gap (ICW-082), an undocumented cache-eviction policy (ICW-305), and — notably — **its own backlog integrity problem**: ICW-081 reports duplicate ticket IDs and orphaned files in `docs/tasks/tickets/`. I independently verified that claim (§3) and it's actually broader than stated. Given this density of self-discovered findings, this pass's value is concentrated in two places: **things the internal audits haven't reached yet** (the brand-new `Logging`/exception-safety code, §1–2) and **verification** of claims already marked "Done" (§4).

---

## 1. Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | The new Serilog `EventLog` sink (`SerilogHost.CreateLogger`) is wired with `manageEventSource: true` and constructed synchronously, unguarded, in `App.OnStartup` — **before** the new global exception handlers are registered. On a non-administrator Windows account with no pre-existing "InfiniteCanvas" event source, this throws a `SecurityException` on first launch, crashing the app the new crash-safety feature was built to prevent | **High** | 85% |
| 2 | `UpdateViewportScrollbars` dereferences `_horizontalScrollbarTrack`/`_horizontalScrollbarThumb`/`_verticalScrollbarTrack`/`_verticalScrollbarThumb` via the null-forgiving `!` operator, with no null-check, on every render frame — sharper, file/line-level evidence for the already-ticketed ICW-077 | **Medium** (supplements ICW-077) | 90% |
| 3 | `OnDispatcherUnhandledException` unconditionally sets `e.Handled = true` for every exception class, with no user-facing indication anything failed — the app now silently absorbs arbitrary failures (including ones that may leave rendering/scene state inconsistent) with only a log line as evidence | **Low-Medium** | 85% |
| 4 | Ticket-corpus duplication (ICW-081) is worse than its own text states: **5** numeric IDs are duplicated across **10** files (061–065), not just ICW-065 | **Low** (process, supplements ICW-081) | 95% |

**Verified — genuinely fixed, not just claimed:** ICW-020 (pixelometer O(1) lookup) is now real — `TileGridIndexLookup.TryGetTileIndex` is correctly wired into `TryReadPixelValue`, replacing the old linear scan. **Verified — still open despite adjacent work landing:** ICW-035 (pixelometer/render blend divergence) — `MainWindow.BlendDefect` is byte-for-byte unchanged from my first report while the renderer-side formula has changed twice now (see my prior report §3); this gap has had three separate opportunities to be fixed incidentally and wasn't.

---

## 2. New Findings — Full Detail

### 2.1 [HIGH] EventLog sink can crash the app on first launch, before the new exception handlers exist to catch it
**Files:** `src/InfiniteCanvas.App/Logging/SerilogHost.cs:31` (new), `src/InfiniteCanvas.App/App.xaml.cs:10-18` (new)
**Confidence: 85%**

```csharp
// SerilogHost.cs
var configuration = new LoggerConfiguration()
    ...
    .WriteTo.EventLog("InfiniteCanvas", manageEventSource: true, restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();
```
```csharp
// App.xaml.cs — OnStartup
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    Log.Logger = SerilogHost.Logger;                                    // <-- constructs the EventLog sink here
    DispatcherUnhandledException += OnDispatcherUnhandledException;      // <-- registered AFTER
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    Log.Information("Application starting");
}
```

`Serilog.Sinks.EventLog`'s own documentation is explicit: *"Applications that run with administrative privileges, and that can therefore create event sources on-the-fly, can opt in by providing `manageEventSource: true`."* Registering a new Windows Event Log source writes to `HKLM\SYSTEM\CurrentControlSet\Services\EventLog\Application`, which requires administrator rights on a standard install; multiple independently-reported issues against this exact sink (e.g. `serilog/serilog-sinks-eventlog#10`) show the failure mode is a `SecurityException` thrown **synchronously from the `EventLogSink` constructor**, i.e. from inside `.WriteTo.EventLog(...)` itself, at logger-construction time — not deferred to first write.

Concretely: `Log.Logger = SerilogHost.Logger` (line 13) is the very first line of `OnStartup`, evaluated *before* any of the three new exception handlers are registered on the next three lines. If this machine has never run InfiniteCanvas as an administrator before (the common case for a first-time evaluator of a "greenfield" internal tool), `SerilogHost.Logger`'s lazy getter (`_logger ??= CreateLogger()`) throws `SecurityException` right there, with **nothing yet installed to catch it** — the exact `DispatcherUnhandledException`/`AppDomain.UnhandledException` safety net ICW-014 exists to provide isn't wired up yet at this point in the method. The practical result: on a non-admin machine, the app that was just hardened against unhandled-exception crashes now has a **new, additional, unconditional startup crash** that occurs earlier than anything the hardening work covers.

I could not execute the app in this sandbox to reproduce the exact exception (no Windows/.NET runtime available here), so confidence is 85% rather than higher — it's possible the CI/dev machines this was validated on already have the source registered (e.g. from an earlier admin run) or run elevated, which would mask this in every environment the team has actually tested on so far, which is consistent with the ticket log showing no mention of this failure mode.

**Recommendation:** Either drop `manageEventSource: true` (matching the sink's own v3+ default of `false`, and Windows Event Log is a poor fit for a desktop demo app's routine warning-level logging anyway — the file sink already covers that), or wrap the `SerilogHost.Logger` initialization in a try/catch that falls back to file-only logging on any EventLog-related failure, and do it *before* anything else in `OnStartup` touches the logger. This is a 2-minute fix that directly protects the investment already made in ICW-014.

---

### 2.2 [MEDIUM] Null-forgiving operator on nullable scrollbar fields, dereferenced every frame — sharpens ICW-077
**File:** `src/InfiniteCanvas.App/MainWindow.xaml.cs` (`UpdateViewportScrollbars`, called from `RenderFrameAsync` on every render)
**Confidence: 90%**

```csharp
private Canvas? _viewportScrollbarOverlay;
private Border? _horizontalScrollbarTrack;
private Border? _horizontalScrollbarThumb;
...
UpdateScrollbar(
    ViewportScrollbarAxis.Horizontal,
    ViewportScrollbarPolicy.ComputeMetrics(...),
    _horizontalScrollbarTrack!,     // null-forgiving, no guard
    _horizontalScrollbarThumb!,     // null-forgiving, no guard
    ...);
```

These four fields are populated via runtime `FindName("...")` string lookups in `OnLoaded` rather than compiler-generated `x:Name` fields (itself worth noting as a minor smell — it forfeits compile-time renaming safety for these specific controls while the rest of the window's named elements presumably use the generated fields normally). Every one of the five mouse-interaction handlers I read for these controls (`OnScrollbarTrackMouseLeftButtonDown`, `OnScrollbarThumbMouseLeftButtonDown`, `OnScrollbarThumbMouseMove`) **correctly** null-checks before use. `UpdateViewportScrollbars`/`UpdateScrollbar` — the method that runs on *every single rendered frame* — does not; it suppresses the compiler's nullable warning with `!` instead of checking.

This is precisely the "nullable/initialization hazards" ICW-077 already describes in the abstract ("static analysis reports... realistic source of intermittent UI breakage") — I'm supplying the exact method and the exact mechanism (`!` bypassing a check that exists three call sites away in the same class) so whoever picks up ICW-077 doesn't have to re-derive it. Currently unreachable in practice (the named elements are declared in `MainWindow.xaml` and `FindName` reliably resolves them once the template is applied before first render), so this is latent rather than actively firing — consistent with ICW-077 still being `Proposed` rather than a live bug report.

**Recommendation:** No new ticket needed — this is exactly ICW-077's scope. Suggest the fix mirror the existing guard pattern in the mouse handlers: `if (_horizontalScrollbarTrack is null || _horizontalScrollbarThumb is null || ...) return;` once at the top of `UpdateViewportScrollbars`, removing all four `!` usages.

---

### 2.3 [LOW-MEDIUM] Global exception handler swallows everything unconditionally, with zero user-facing signal
**File:** `src/InfiniteCanvas.App/App.xaml.cs:30-34`
**Confidence: 85%**

```csharp
private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
{
    Log.Error(e.Exception, "Unhandled exception on the WPF dispatcher");
    e.Handled = true;
}
```

This does solve the core problem my first report (§2.1) and the internal audits (F-01) flagged — the app genuinely no longer hard-crashes on an unhandled dispatcher exception, confirmed by direct reading. But `e.Handled = true` is set for **every** exception, unconditionally, with no distinction between something benign/recoverable (e.g. a transient render-frame fault the existing coalescer would naturally retry) and something that's left the application in a corrupted state (e.g. a fault mid-way through `RegenerateSceneAsync`'s multi-step scene swap, which per the still-open ICW-029 isn't atomic). The only trace of a failure is a line in a rolling log file most users will never look at (`%LocalAppData%\InfiniteCanvas\logs\`) or the Windows Event Log (see §2.1's separate finding). A user who triggers a genuine fault gets an app that silently stops behaving correctly with no toast, status-bar message, or dialog — arguably a worse experience for a "greenfield, no legacy debt" project than a clear crash-and-restart, since a silent partial failure is harder to notice, report, and reproduce than a crash.

**Recommendation:** Not a blocker — logging + `Handled = true` is a legitimate first increment (and the ticket is explicitly `In Progress`, not `Done`). Before closing ICW-014, consider adding a lightweight, non-blocking status-bar or toast notification ("Something went wrong — check the log" / a "Recovered from an error" indicator) so failures are at least visible to the person using the app, not just to whoever later reads the log file.

---

## 3. Verification: ICW-081's Own Finding Is Undercounted
**Confidence: 95%**

ICW-081 states: *"the inventory found 85 ticket files, a duplicate ICW-065 identity, and ICW-061/ICW-062/ICW-063 ticket IDs absent from the live trackers."* I independently re-ran the inventory rather than trusting this count:

```
$ ls docs/tasks/tickets/ | grep -oE "ICW-[0-9]+" | sort | uniq -c | sort -rn | head -5
      2 ICW-065
      2 ICW-064
      2 ICW-063
      2 ICW-062
      2 ICW-061
```

**Five** numeric IDs are duplicated, not one, across **ten** files covering entirely unrelated concerns sharing the same ID:

| ID | File A | File B |
|---|---|---|
| ICW-061 | `fix-strtree-query-immutability.md` | `spatial-query-count-api.md` |
| ICW-062 | `live-index-publish-hardening.md` | `strtree-immutability-copy-on-query.md` |
| ICW-063 | `boundary-semantics-and-tests.md` | `live-index-publish-hardening.md` |
| ICW-064 | `spatial-boundary-semantics.md` | `tile-cache-capacity-and-materialization-metrics.md` |
| ICW-065 | `spatial-tests-and-docs.md` | `viewport-scrollbars-and-zoom-navigation.md` |

This matters beyond pedantry: `ICW-064` currently has *two* completely different meanings in the ticket directory (spatial boundary semantics vs. tile-cache capacity) while `task-tracker.md`'s live tracker row for `ICW-064` refers to the tile-cache one — meaning the *other* `ICW-064` file (spatial boundary semantics) has no corresponding tracker row at all and is effectively invisible to anyone reading `task-tracker.md` as the source of truth. This is exactly the kind of silent data loss ICW-081 was filed to prevent, and it's larger in scope than ICW-081's own text currently credits.

**Recommendation:** No new ticket — feed this exact five-ID list into ICW-081's reconciliation work directly; it saves that ticket's implementer from re-running the inventory.

---

## 4. Spot-Verification of Additional "Done"/"In Progress" Claims

| Claim | Verified? | Evidence |
|---|---|---|
| ICW-020: "Replace linear tile scan per mouse move with direct grid index arithmetic" | ✅ **True, genuinely fixed** | `TileGridIndexLookup.TryGetTileIndex` (new file, clean O(1) arithmetic with proper bounds/finite checks) is called directly from `TryReadPixelValue`; the old `foreach (var tile in _tiles)` scan is gone. Best-verified claim in this pass. |
| ICW-014: "Registered WPF dispatcher, AppDomain, and unobserved-task exception hooks with Serilog reporting" | ✅ True as stated, but see §2.1/§2.3 for what the ticket text doesn't mention | The three handlers exist and are correctly wired/unwired in `OnStartup`/`OnExit`. The ticket's own status is honestly `In Progress` ("selected async-void handlers remain") — not overclaiming completion, which is good practice worth acknowledging. |
| ICW-030: "`MaxObjectsPerTile` is 256 and is enforced in both the generator and runtime controls" | ✅ True | `SampleImageGenerator.GenerateSet` now validates `objectsPerTile` against a shared upper bound; UI validation in `MainWindow` mirrors it. Not exhaustively re-traced given time budget, but the constant and both call sites are present as described. |
| ICW-034: "queued follow-up requests are preserved and disposal remains cancellation-safe" | Not independently re-traced this pass | Flagging as **unverified by me** rather than assuming true — this was the most structurally subtle finding in the whole audit series (a CAS-loop/task-fault interaction); given time constraints I relied on the ticket's own stated test count (35/35) rather than re-deriving the fix by hand. If another pass has spare budget, this is the highest-value place to spend a full re-trace, given how easy this exact class of bug is to almost-fix. |

---

## 5. Suggested Priority Addition

1. **§2.1** — cheapest, highest-severity fix in this pass; directly undermines the crash-safety investment just made if left as-is.
2. **§2.2** — hand this evidence straight to ICW-077's implementation, no new ticket needed.
3. **§3** — hand this list straight to ICW-081's implementation, no new ticket needed.
4. **§2.3** — fold into ICW-014's remaining scope before marking it `Done`.

