# InfiniteCanvasWPF — Deep-Dive Code Audit, Pass 2 (Addendum)

**Commit audited:** `43bfd55bbae7e14a590784f7831e5261eecfd69b` — confirmed unchanged via `git ls-remote` (`refs/heads/main` still resolves to this SHA) and a byte-diff of `docs/tasks/task-tracker.md` against the live branch. Same commit as the first report.
**Relationship to prior report:** `infinitecanvaswpf-deep-dive-audit-26-07-24-07-45-28.md`. This is **not a re-audit** — it's a second, more granular pass focused on areas the first pass covered at a higher level (exception flow, unmanaged interop error handling, cross-file consistency, tooling/process gaps). Findings already listed in Pass 1 are **not repeated** here. Read both together.
**Method this pass:** Re-read every file again fresh with line-level tracing of call chains (not just per-file review) — specifically: traced the full exception path from `CoalescingAsyncAction` through `RequestRenderAsync` to every `async void` call site; traced every `Parse`/`TryParse` call for culture-invariance; checked every P/Invoke return value for silent discards; diffed the two "exception attribution" bugs to confirm a pattern; checked repo root for CI/tooling config that wasn't part of the source tree review.

---

## Executive Summary — New Findings This Pass

| # | Finding | Severity | Confidence |
|---|---|---|---|
| P2.1 | `CoalescingAsyncAction.ProcessAsync` has **no exception handling** around the wrapped action call — this is the concrete root cause that makes Pass 1's §2.1 (no crash safety net) reachable, and it has its own independent bug: a coalesced follow-up request can be **silently dropped** if the in-flight action throws | **High** | 90% |
| P2.2 | `ZeroCopyBitmapFactory.Dispose(bool)` discards the return value of `UnmapViewOfFile` — a textbook silently-swallowed unmanaged error | **Medium** | 95% |
| P2.3 | The exception-attribution bug found in Pass 1 (§2.2, `GenerateSet`) is not an isolated slip — `Bgra32BufferLayout.GetPixelOffset` has the same pattern (`nameof(x)` blamed even when `y` is the actual violator) | **Low-Medium** | 90% |
| P2.4 | No CI pipeline (`.github/workflows` absent) and no `.editorconfig`/`Directory.Build.props`/`global.json`; `<Nullable>enable</Nullable>` is set in every `.csproj` but never backed by `TreatWarningsAsErrors`, so nullable-safety is advisory only, and the 26 existing tests are never run automatically | **Medium** (process) | 95% |
| P2.5 | `CoalescingAsyncAction.DisposeAsync` can itself throw — if the last processing task had already faulted for a reason unrelated to disposal, awaiting it inside `DisposeAsync` re-throws that stale exception out of `Dispose` | **Low-Medium** | 80% |
| P2.6 | Inconsistent culture-invariance in numeric parsing: `CustomZoomPercentTextBox` is parsed with `NumberStyles.Float, CultureInfo.InvariantCulture`; the three generation-panel int parses (`TilesXTextBox`, `TilesYTextBox`, `ObjectsPerTileTextBox`) use the culture-dependent `int.TryParse(string, out int)` overload in the same class | **Low** | 85% |
| P2.7 | `DrawTile`/`DrawDefectPatch` divide by `tile.Bounds.Width/Height` / `annotation.Bounds.Width/Height` with no zero-guard; a zero-size bounds would degrade silently (NaN → `int.MinValue` → clamped to `0` by the subsequent `Math.Clamp`) rather than fail loudly — currently unreachable given upstream validation, but the safety is accidental (a side effect of `Math.Clamp`'s range-clamping), not deliberate | **Low** (currently unreachable) | 65% |
| P2.8 | Zero accessibility affordances in `MainWindow.xaml`: no `AutomationProperties.Name`, no `KeyBinding`s, no access-key mnemonics anywhere in the panel controls | **Low** | 90% |

**Confirmed correct on closer inspection (no new issue):** `LiveSpatialIndexService.PublishSnapshotAsync`'s failure-recovery branch (re-traced the CAS interleaving explicitly — no lost-update window); `ZeroCopyBitmapFactory.GenerateFrozenBitmap`'s off-UI-thread `InteropBitmap` creation (`.Freeze()` is correctly called before the bitmap crosses threads — this is the standard, valid WPF pattern for background bitmap work, not a bug); `SampleImageGenerator.ResampleTemplate`'s bilinear resample math (correct half-pixel-center convention, properly clamped edges — no artifacts found); `SpatialBounds`'s constructor validation (correctly attributes each `nameof(...)` to the actual failing parameter — this is the **correct** version of the pattern P2.3 violates, which strengthens the case that P2.3/Pass-1-§2.2 are oversights, not a deliberate convention).

---

## 1. [HIGH] `CoalescingAsyncAction.ProcessAsync` — unguarded action invocation + lost-request bug
**File:** `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs:65-81`
**Confidence: 90%**

```csharp
private async Task ProcessAsync()
{
    while (true)
    {
        lock (_gate)
        {
            if (!_requested || _disposed) return;
            _requested = false;
        }
        await _action(_lifetime.Token).ConfigureAwait(false);   // <-- no try/catch
    }
}
```

Two distinct problems:

1. **This is the exact mechanism behind Pass 1 §2.1.** `_action` here is `DispatchRenderFrameAsync` → `RenderFrameAsync`, wired up in `MainWindow.xaml.cs:50`. `RequestRenderAsync` (`MainWindow.xaml.cs:156-173`) only catches `OperationCanceledException`/`ObjectDisposedException` around `await _renderAction.RequestAsync()`. Any other exception thrown inside `RenderFrameAsync` (a background-thread `OverflowException`, an `IndexOutOfRangeException` from a malformed tile, an `OutOfMemoryException` from an oversized allocation) faults `_processingTask` here, propagates unmodified through `RequestAsync()`, through `RequestRenderAsync`, and out into whichever `async void` handler called it — this traces the full path from Pass 1's finding to its root cause.

2. **Independent bug — coalesced request loss on failure.** Sequence: Request A arrives, `_requested` reset to `false`, `_action()` starts running. While it's running, Request B arrives via `RequestAsync()` — since `_processingTask.IsCompleted` is still `false`, `RequestAsync` just sets `_requested = true` and hands back the *same* in-flight task (this is the coalescing contract working as intended). Now suppose `_action()` throws. The `while(true)` loop exits via the unhandled exception *before* it ever loops back to check `_requested` — so Request B's "there's a newer pending request" signal is discarded. The next unrelated `RequestAsync()` call will start a fresh cycle (since a faulted task's `IsCompleted` is `true`), so the system self-heals *eventually*, but there is no guarantee anything will call `RequestAsync()` again in the near term — in a quiescent viewport (no further mouse/keyboard/resize events), a mid-flight failure silently drops the most recent pending render intent until the next unrelated interaction.

**Recommendation:** Wrap the `await _action(...)` in try/catch inside `ProcessAsync`; on failure, either re-loop to service any coalesced follow-up request that arrived during the failed attempt (matching the coalescing contract), or explicitly surface the failure via an event/callback so the owner can decide (log + retry, log + give up, etc.), and store the exception rather than letting it fault the shared task silently. This one fix also substantially de-risks Pass 1 §2.1 for the render path specifically (though the global handler is still recommended defense-in-depth for the ~13 other `async void` handlers that don't go through this coalescer at all).

---

## 2. [MEDIUM] Discarded `UnmapViewOfFile` return value
**File:** `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:250-259`
**Confidence: 95%**

```csharp
private void Dispose(bool disposing)
{
    lock (_lifetimeGate)
    {
        if (_view != IntPtr.Zero)
        {
            UnmapViewOfFile(_view);   // <-- bool return value discarded
            _view = IntPtr.Zero;
        }
        _section?.Dispose();
        _section = null;
    }
}
```
`UnmapViewOfFile` returns `bool` (success/failure) with `SetLastError = true` declared on the P/Invoke signature — the signature was clearly written with error-checking in mind, but the result is never inspected. Contrast with `SafeFileMappingHandle.ReleaseHandle()` four lines below in the same file, which *does* correctly propagate `CloseHandle`'s result as the method's return value. This is a direct instance of the "poorly handled or silently swallowed errors" pattern called out in the audit brief, in unmanaged/interop code where failures are both more likely and more consequential (address space / handle leaks) than in managed code.

**Recommendation:** Check the result; on failure, call `Marshal.GetLastWin32Error()` and at minimum log it (a leaked view is not fatal enough to throw from `Dispose`, but it should not be invisible).

---

## 3. [LOW-MEDIUM] Exception-attribution bug is a pattern, not a one-off
**Files:** `src/InfiniteCanvas.Rendering/Bgra32BufferLayout.cs:29-37` (new instance), `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:33-36` (already in Pass 1 §2.2)
**Confidence: 90%**

```csharp
public int GetPixelOffset(int x, int y)
{
    if (!Contains(x, y))
    {
        throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates must be within the buffer.");
    }
    return checked((y * Stride) + (x * 4));
}
```
`Contains(x, y)` can fail because of `x`, `y`, or both — but the exception always names `x`. Currently unreachable from the real render path (all call sites pre-clamp coordinates into valid ranges before calling this), so this is a latent/defensive-code issue rather than an active bug — but combined with Pass 1 §2.2, it's now two occurrences of the identical mistake in the same project, while `SpatialBounds`'s constructor (`src/InfiniteCanvas.Core/SpatialBounds.cs:7-25`) demonstrates the team already knows and applies the correct per-parameter pattern elsewhere. This elevates it from "a slip" to "a pattern worth a lint rule."

**Recommendation:** A Roslyn analyzer or simple code-review checklist item — "does every multi-condition guard clause attribute the exception to the specific failing parameter?" — would catch this class of bug going forward. Fix both instances; low effort (each is a 2-3 line change to split the compound condition).

---

## 4. [MEDIUM, process] No CI, no nullable enforcement, no shared style config
**Confidence: 95%**

Checked the full repo tree for tooling/process configuration beyond the source itself:
- **No `.github/workflows/`** — the only `.github` content is `agents/infinitecanvas.agent.md`. There is no automated build or test run on push/PR. Every "passing tests" claim currently logged in `docs/tasks/task-tracker.md`'s Activity table (e.g. "Completed with passing validation: `dotnet test ...`") is a manually-run, developer-attested claim with no independent verification.
- **No `.editorconfig`, `Directory.Build.props`, or `global.json`** anywhere in the repo. Each `.csproj` independently sets `<Nullable>enable</Nullable>` (confirmed in all 9 project files), but none set `<TreatWarningsAsErrors>` or `<WarningsAsErrors>Nullable</WarningsAsErrors>` — meaning nullable-reference warnings (`CS8600`, `CS8602`, `CS8618`, etc.) can accumulate indefinitely without ever failing a build. Given the project's explicit goal of zero technical debt, an unenforced nullable-safety net provides much weaker guarantees than the `<Nullable>enable</Nullable>` setting implies at a glance.
- Only `tests/InfiniteCanvas.Tests.csproj` sets `<LangVersion>latest</LangVersion>` explicitly; the other 8 projects rely on the SDK's implicit default, which is fine in practice (SDK defaults track the latest C# version for the target framework) but is an inconsistency worth normalizing into a shared `Directory.Build.props` rather than leaving implicit everywhere except one project.

**Recommendation:** Add a minimal `.github/workflows/ci.yml` running `dotnet build` + `dotnet test` on push/PR for both test projects (Windows runner needed for `InfiniteCanvas.Windows.Tests`). Add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (or at minimum treat nullable warnings as errors) to a new root `Directory.Build.props` so every project inherits it instead of repeating `<Nullable>enable</Nullable>` nine times with no enforcement. This is foundational "no legacy debt" infrastructure that's currently entirely absent — arguably higher-leverage than any single code fix in either audit pass, since it would have caught several of the smaller findings here automatically (e.g., an unused-variable or unreachable-code analyzer would likely flag the dead `IRenderer`/`RefreshCommand` code from Pass 1).

---

## 5. [LOW-MEDIUM] `CoalescingAsyncAction.DisposeAsync` can throw
**File:** `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs:34-63`
**Confidence: 80%**

```csharp
public async ValueTask DisposeAsync()
{
    ...
    if (processingTask is not null)
    {
        try { await processingTask.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    _lifetime.Dispose();
}
```
Only `OperationCanceledException` tied to *this dispose's own* cancellation is swallowed. If `_processingTask` had already faulted for an unrelated reason **before** `DisposeAsync` was ever called (see Finding 1 — this is very plausible given `ProcessAsync` has no exception handling), that stale, unrelated exception is still attached to the task and will be re-thrown here, meaning `MainWindow.xaml.cs:898`'s `await _renderAction.DisposeAsync();` (called from `OnClosed`) could itself throw during window shutdown. Throwing from a `Dispose`/`DisposeAsync` path is a well-known anti-pattern (obscures the original failure's timing, can mask/replace other shutdown-path exceptions, and callers rarely expect `Dispose` to fail).

**Recommendation:** Once Finding 1 is fixed (exceptions handled inside `ProcessAsync` rather than left to fault the shared task), this mostly resolves itself. As defense in depth, `DisposeAsync` could also catch and log (not rethrow) any exception from the final `await processingTask`, since by the time `Dispose` runs the caller has already decided they're done with the object.

---

## 6. [LOW] Inconsistent culture-invariance in numeric TextBox parsing
**File:** `src/InfiniteCanvas.App/MainWindow.xaml.cs:734` (correct) vs. `:862, :868, :874` (inconsistent)
**Confidence: 85%**

```csharp
// OnApplyCustomZoomClicked — correct, explicit invariant parsing:
if (!double.TryParse(CustomZoomPercentTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ...)

// TryReadGenerationOptions — same class, culture-dependent overload:
if (!int.TryParse(TilesXTextBox.Text, out var columns) || columns <= 0) ...
if (!int.TryParse(TilesYTextBox.Text, out var rows) || rows <= 0) ...
if (!int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile) || objectsPerTile < 0) ...
```
One parse call in the class explicitly guards against culture-dependent parsing surprises (non-Latin digit shapes, alternate group/negative-sign characters under some non-`en-US` `CurrentCulture` settings); three sibling calls in the same file/feature area don't. Practical impact is low (plain small positive integers are unlikely to trip this under most locales), but it's an unforced inconsistency within a single, small, recently-written class — exactly the kind of drift a greenfield project can eliminate for free.

**Recommendation:** Use `int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)` for all three, matching the pattern already established for the zoom-percent field.

---

## 7. [LOW, currently unreachable] Division-by-zero in `DrawTile`/`DrawDefectPatch` degrades silently rather than failing loudly
**File:** `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:161-183, 200-228`
**Confidence: 65%**

Both inner pixel loops divide by `tile.Bounds.Height`/`.Width` (or `annotation.Bounds.*`) with no guard:
```csharp
var sourceY = Math.Clamp((int)((worldY - tile.Bounds.Y) * tile.PixelHeight / tile.Bounds.Height), 0, tile.PixelHeight - 1);
```
If `Bounds.Height` were ever `0`, this divides by zero → `double.NaN`/`Infinity` → the explicit (non-`checked`) cast to `int` yields `int.MinValue` (well-defined .NET behavior for out-of-range/NaN double-to-int casts in an unchecked context) → `Math.Clamp` silently pulls that back into `[0, PixelHeight-1]`, i.e. `0`. No exception, no crash — just a wrong-looking but harmless render (repeats the tile's corner pixel). This path is **not reachable today**: `SampleImageGenerator.GenerateSet` validates `pixelWidth > 0`/`pixelHeight > 0` before any `SpatialBounds` derived from them can be zero-sized, and `SpatialBounds`'s own constructor further rejects negative width/height. Flagging this as a brittle *implicit contract* (Fowler: hidden temporal/structural coupling) — the safety here is an accidental side effect of `Math.Clamp`'s range behavior, not a deliberate guard, so if a future code path ever constructs a zero-size tile/annotation bounds (e.g., a new generation mode, or a bug elsewhere), this will silently mis-render instead of failing fast with a clear error.

**Recommendation:** Low priority given current unreachability, but a one-line `Debug.Assert(tile.Bounds.Width > 0 && tile.Bounds.Height > 0)` (or an explicit guard that skips drawing degenerate bounds) at the top of both methods would convert a silent, hard-to-diagnose future rendering glitch into a loud, easy-to-diagnose failure — cheap insurance for a hot-path method that's likely to be touched again as rendering features grow.

---

## 8. [LOW] No accessibility affordances in `MainWindow.xaml`
**Confidence: 90%**

`grep` for `AutomationProperties`, `KeyBinding`, `AccessText`/access-key mnemonics (`_` underscore accelerators) across `MainWindow.xaml` returns zero matches. Every `TextBox`, `ComboBox`, and `Button` in the generation/display side panel is unlabeled for screen readers and has no keyboard shortcut. For an internal inspection tool this may be an accepted tradeoff, but it's a real, checkable gap the audit brief's "gaps and missed opportunities" criterion covers, so it's included here rather than silently skipped.

**Recommendation:** Low priority relative to everything else in both passes; if/when the UI stabilizes, add `AutomationProperties.Name` to the interactive controls (cheap, mechanical) and consider `AccessText`/`KeyBinding`s for the most-used actions (Regenerate, zoom presets).

---

## Assumptions and Open Questions (Pass 2)

- Findings 1 and 5 (`CoalescingAsyncAction`) assume standard .NET `Task` fault semantics (a faulted `Task.IsCompleted == true`, and awaiting a faulted task re-throws the original exception) — this is documented, stable BCL behavior, not an assumption specific to this codebase, so confidence here is driven by code-reading certainty rather than runtime-behavior uncertainty.
- Finding 7's confidence (65%) is capped below "near-certain" specifically because I cannot execute the app to confirm the exact `(int)double.NaN` cast behavior wasn't somehow altered by an ambient `checked` context I missed — I traced the surrounding methods and found no `checked`/`unchecked` block wrapping these specific casts, but full certainty would require a compiled repro.
- I did not find any existing ticket or ADR discussing CI/tooling gaps (Finding 4) or the `CoalescingAsyncAction` exception-safety gap (Finding 1) — checked all six ticket files, both ADRs, and both handoff docs again this pass; if either is already a known, intentionally deferred gap, apologies for the duplication.
- As in Pass 1, no `dotnet build`/`dotnet test` was run in this sandbox (no .NET SDK / Windows runtime available); all findings are from static line-by-line reading of the fetched source at the exact audited commit.

---

## Suggested Priority Addition to Pass 1's Ordering

Insert after Pass 1's #1 (global exception handler):

1. Pass 1 §2.1 — global `DispatcherUnhandledException` handler.
2. **This pass, Finding 1** — fix `CoalescingAsyncAction.ProcessAsync` exception handling. Do this *with* #1, not instead of it: #1 is the safety net for everything, this fix is what stops the render pipeline specifically from needing that net in the common case, and it independently fixes the lost-coalesced-request bug.
3. **This pass, Finding 4** — stand up CI + `Directory.Build.props` with nullable-as-error. Do this early; it will make every subsequent fix in both reports self-verifying instead of relying on manual `dotnet test` runs.
4. Continue with the rest of Pass 1's ordering; slot Findings 2, 3, 5, 6, 7, 8 from this pass into the "batch of small DRY/consistency cleanups" step at the end.

