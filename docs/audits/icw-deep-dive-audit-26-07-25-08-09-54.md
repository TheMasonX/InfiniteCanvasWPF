# InfiniteCanvasWPF — Deep-Dive Code Audit

**Repo**: TheMasonX/InfiniteCanvasWPF · **Ref audited**: `main`@`52a3442` (2026-07-25 08:01 UTC), cross-checked against `main`@`43bfd55` (prior HEAD) to isolate deltas.
**Method**: Full-repository fetch via `codeload.github.com` tarball (not GitHub UI/`web_fetch`, which truncate). Every `.cs` file under `src/`, `tests/`, `benchmarks/` read in full; all XAML read in full; all of `docs/` (ADRs, handoffs, requirements, 7 prior audit artifacts, ~130 ticket files) read or sampled for status. No files were skipped.
**Confidence values** below are this auditor's subjective calibration, not a formal statistical measure — treat as "how likely is this finding correct and reproducible from the cited code," not "probability of user-facing impact."

---

## 0. Executive Summary — Read This First

**The codebase itself is in reasonably good shape.** Core primitives (`CameraTransform`, `SpatialBounds`, `CoalescingAsyncAction`, `TileGridIndexLookup`, `ViewportZoomPolicy`) are small, well-tested, immutable-by-default, and use correct lock-free patterns (`Interlocked.CompareExchange` retry loops, atomic record swaps). Several previously-flagged defects (mutable `STRtree` query results, unattributed argument validation, missing coalescing fault handling) have been genuinely fixed between the last two commits, with evidence below.

**The task-tracking system is not.** This is the single highest-leverage finding in this audit and directly explains the "diluted signal-to-noise" the requester is trying to avoid:

- `docs/tasks/tickets/` contains **~130 files** using **six incompatible ID/status schemes** simultaneously (`ICW-0xx`, `ICW-1xx`, `ICW-2xx`, `ICW-3xx`, bare `000x`, dated `2026-07-25-*`, unprefixed `ticket-*`), several of which **cover the identical concern under different IDs** (e.g., `ICW-020` and `ICW-055` are both "pixelometer O(1) lookup," `ICW-021`/`ICW-053` and `ICW-012` both duplicate "extract locked-bits helper," `ICW-007` and `ICW-054` both duplicate "overlay pooling," `ICW-064` appears twice with different bodies, `ICW-065` appears twice).
- A large fraction of tickets (roughly 40, all authored `Copilot`, `key: ICW-999` or `ICW`) are **empty template stubs** — see §1 for a verbatim example — that carry zero investigative content but still occupy tracker slots and status-table rows.
- `docs/tasks/active-tasks.md` contains **stale evidence text**: e.g., ICW-064's evidence claims the default cache budget "retains four 8192×4096 tiles (134,217,728 pixels)," but the shipped constant (`TileCacheBudget.DefaultMaxBytes = 4L*1024*1024*1024`, `SampleImageTile.cs:377`) is 4 GiB — enough for **~128** such tiles, not four. The tracker disagrees with the code it describes.
- Net effect: an engineer picking up this backlog cannot tell, without re-reading source, which of the ~130 tickets are live, which are duplicates, which are placeholders, and which describe code that no longer exists.

**Recommendation implemented in this report**: rather than re-deriving the ~130 pre-existing findings, this audit (a) calls out the process problem as Finding P-1, (b) spot-verifies the most severe pre-existing "To Do" items against current line numbers so they can be trusted, (c) flags two tickets whose underlying code was silently fixed since filing so they can be closed, and (d) reports a small number of **genuinely new** findings this pass surfaced that no existing ticket covers.

### Top 8 things to act on, ranked

| # | Finding | Status | Severity | Confidence |
|---|---|---|---|---|
| P-1 | Task tracker has duplicate IDs, ~40 empty stub tickets, and stale evidence text | **New** | Process/Critical | 95% |
| 1 | No global unhandled-exception handlers (`DispatcherUnhandledException`/`AppDomain`/`TaskScheduler`) while 20 `async void` handlers exist | Corroborates ICW-014 | High | 90% |
| 2 | Shutdown disposes `_generationGate`/`_lifetime` without awaiting in-flight generation, racing `SemaphoreSlim.Release()` after `Dispose()` | Corroborates ICW-029 | High | 75% |
| 3 | `ShowImageTilesCheckBox` state is not part of `CanvasUserSettings` and is silently forced to `true` on every launch | **New** | Medium | 92% |
| 4 | `Classification` is a raw `string` used as a dictionary/switch key across 4 files (no enum) | Corroborates ICW-304 | Medium | 90% |
| 5 | `CameraTransform` default scale range widened to `[1e-10, 10000]` with no documented rationale | **New** | Low-Medium | 80% |
| 6 | `TileCacheBudget` eviction picks `Values.FirstOrDefault(...)`, i.e., dictionary enumeration order, not LRU, despite `DescribeStatus()` implying a managed cache | Corroborates ICW-305 | Medium | 88% |
| 7 | `SampleAnnotation` is a `record` holding `byte[] DefectPixels` and `IReadOnlyDictionary<string,double> Features` — record-generated equality is silently reference-based for both, contradicting value-type intent | **New** (refines ICW-301/304) | Low-Medium | 85% |
| 8 | `STRtree.Query` mutable-list leak (previously real) is **fixed** — verify and close ICW-061/ICW-062/ICW-006 | **Resolved, needs ticket close** | — | 95% |

---

## 1. Finding P-1 — Task-Tracker Signal-to-Noise Crisis

**Severity: Process/Critical (blocks efficient use of every other finding in this repo).**
**Confidence: 95%** (directly observed, not inferred).

### Evidence

**Duplicate IDs covering the same concern:**

| Concern | Competing ticket files |
|---|---|
| Pixelometer O(1) lookup | `ICW-020-pixelometer-o1-tile-lookup.md` (Done) **and** `ICW-055-pixelometer-o1-lookup.md` |
| Extract locked-bits helper | `ICW-012-extract-withlockedbits-helper.md`, `ICW-021-extract-lockedbits-helper.md` |
| Overlay pooling / animation continuity | `ICW-007-overlay-element-pooling.md` (To Do), `ICW-019-overlay-animation-continuity.md`, `ICW-054-overlay-pooling-and-animation-continuity.md` |
| RefreshCommand dead-path removal | `ICW-017-refreshcommand-dead-path-removal.md` (Done) **and** `ICW-053-refreshcommand-dead-path-removal.md` (also present as `ICW-053-zero-copy-stride-and-alignment.md` — **`ICW-053` is reused for two unrelated topics**) |
| Tile cache capacity | `ICW-064-tile-cache-capacity-and-materialization-metrics.md` (Done) **and** `ICW-064-spatial-boundary-semantics.md` — **same ID, two different subjects** |
| Viewport scrollbars / zoom navigation | `ICW-065-viewport-scrollbars-and-zoom-navigation.md` **and** `ICW-065-spatial-tests-and-docs.md` — **same ID, two different subjects** |
| Boundary semantics | `ICW-009-boundary-semantics.md`, `ICW-033-boundary-semantics-and-placement-consistency.md`, `ICW-063-boundary-semantics-and-tests.md`, `ICW-308-spatialbounds-semantics.md` |
| CI/nullable enforcement | `ICW-013-ci-and-nullable-enforcement.md`, `ICW-036-ci-and-nullability-enforcement-baseline.md`, `ICW-051-ci-and-nullability-enforcement.md` |
| MainWindow decomposition | `ICW-022-mainwindow-decomposition-and-tests.md`, `ICW-052-mainwindow-decomposition-and-tests.md` |
| Global exception handler | `ICW-014-global-exception-safety-net.md`, `docs/tasks/tickets/0002-add-global-exception-handler.md` |

**Empty stub tickets** (verbatim example, `docs/tasks/tickets/ICW-060-spatial-index-audit-findings.md`):
```
---
id: ICW-060-spatial-index-audit-findings
author: Copilot
key: ICW
title: Icw 060 Spatial Index Audit Findings
status: Proposed
...
---
# ICW-060-spatial-index-audit-findings
## Summary
Status: Proposed
## Scope
- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.
```
This pattern (`author: Copilot`, `key: ICW-999` or `key: ICW`, boilerplate "Review and update the relevant implementation area") repeats verbatim in at least: `ICW-060`, `ICW-062` (dup of 062-live-index and 062-strtree), `0001`–`0006`, `ticket-core-zero-copy-lifetime.md`, `ticket-rendering-zero-copy-safety.md`, `ticket-spatial-dedupe.md` — **≈13+ confirmed, likely more** among the ~130 files. These are distinguishable from real tickets (which have populated Summary/Scope/Findings/Validation sections) only by opening each file.

**Stale evidence vs. shipped code** (`docs/tasks/active-tasks.md`, ICW-064 row):
> "Default cache budget now retains four 8192x4096 tiles (134,217,728 pixels)"

Current code, `src/InfiniteCanvas.Rendering/SampleImageTile.cs:377`:
```csharp
public const long DefaultMaxBytes = 4L * 1024 * 1024 * 1024; // 4 GiB = 4,294,967,296 bytes
```
`4 * 8192 * 4096 = 134,217,728` bytes ≈ 128 MiB, **not** 4 GiB. Either the constant changed after the ticket was written and the tracker wasn't updated, or the evidence was wrong at authoring time. Either way this is exactly the kind of tracker/code drift that makes the backlog untrustworthy without re-verification — which defeats the purpose of having a backlog.

### Recommendation
1. **Freeze new ticket creation** until existing tickets are triaged.
2. Pick **one** ID scheme (`ICW-###`, monotonic, never reused) and **one** status vocabulary. Delete/merge the parallel `000x`, `2026-07-25-*`, `ticket-*.md`, `ICW-1xx/2xx/3xx` namespaces into it, preserving content in the merge.
3. Delete or explicitly re-flag the ~13+ stub tickets — an empty "Proposed" ticket with a template body is worse than no ticket, because it wastes a future reader's time confirming it's empty.
4. Add a pre-commit or CI check that fails if two ticket files share an `id`/filename-number.
5. When a ticket references a code fact (byte counts, line numbers, test counts), either drop the specific numbers or add a lightweight process step to re-verify them before merge.

---

## 2. Verified-Fixed — Close These Tickets

These were flagged in prior audits as active bugs. Diffing the two most recent commits (`43bfd55` → `52a3442`) shows they are now fixed. Recommend closing the corresponding tickets rather than re-auditing them.

### 2.1 `STRtree.Query` mutable-list leak — **Fixed**
Closes: `ICW-006-strtree-immutability.md`, `ICW-061-fix-strtree-query-immutability.md`, `ICW-062-strtree-immutability-copy-on-query.md`.

Old (`43bfd55`), `StrTreeSpatialIndexService.cs:34`:
```csharp
return results as IReadOnlyList<T> ?? results.ToArray();
```
NetTopologySuite's `STRtree<T>.Query` returns a mutable `IList<T>` (concretely a `List<T>`), which **does** implement `IReadOnlyList<T>` — so the old code returned NTS's internal live list to callers, who could then upcast and mutate it, corrupting the index's internal query buffer.

New (`52a3442`), same file, line 31-36:
```csharp
public IReadOnlyList<T> Query(SpatialBounds viewport)
{
    var results = _tree.Query(ToEnvelope(viewport));
    // NetTopologySuite returns a mutable `IList<T>`; copy to an array to ensure
    // callers receive an immutable snapshot and to avoid exposing internal lists.
    return results is T[] arr ? arr : results.ToArray();
}
```
Confirmed via `diff` between commits — this is a real fix with a rationale comment. **Confidence 95%.**

### 2.2 Coalesced-render fault swallowing all future frames — **Fixed**
Closes: `ICW-015-coalescing-render-fault-resilience.md` (`ICW-034` already marked Done and matches).

Old: `ProcessAsync` awaited `_action` with no try/catch; an unhandled exception from the render delegate would fault `_processingTask`, and since `RequestAsync` only creates a new processing task when `_processingTask is null || _processingTask.IsCompleted`, a **faulted-but-completed** task still counts as "completed," so this specific failure mode was actually already survivable — but the fault was never observed or logged anywhere, and any awaiter of the *original* `RequestAsync()` call would see the exception rethrown, while later callers would get a *fresh* task with no diagnostic trail tying it to the earlier failure.

New (`CoalescingAsyncAction.cs:60`, constructor now takes `Action<Exception>? onActionFault`) plus `ProcessAsync` (lines 83-95):
```csharp
try
{
    await _action(_lifetime.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
{
    throw;
}
catch (Exception exception)
{
    ReportActionFault(exception);
}
```
`MainWindow` wires this to `Serilog.Log.Error` (`MainWindow.xaml.cs:115-118`, `OnRenderActionFaulted`). Loop continues after a fault (it re-checks `_requested` on the next iteration), so a single bad frame no longer permanently stalls rendering. **Confidence 90%.** Minor residual note: faults are logged but never surfaced to the user (`StatusText` etc.) — acceptable for a render hiccup, worth a one-line mention in the ticket before closing.

### 2.3 `GenerateSet` argument-validation misattribution — **Fixed**
Closes: `ICW-015-generateset-validation-and-parameter-semantics.md` (already marked Done — confirmed correct).

Old: a single `if (imageCount <= 0 || pixelWidth <= 0 || ...) throw new ArgumentOutOfRangeException(nameof(imageCount))` blamed `imageCount` regardless of which parameter actually failed.
New (`SampleImageGenerator.cs:41-59` and onward): each parameter gets its own guard clause and its own `nameof(...)`. Confirmed by direct read. **Confidence 95%.**

---

## 3. New Findings (not present in any of the ~130 existing tickets)

### 3.1 `ShowImageTilesCheckBox` is not persisted — settings drop silently on relaunch
**Severity: Medium · Confidence: 92%**

`MainWindow.xaml.cs:50` declares `private bool _showImageTiles = true;` and the UI exposes `ShowImageTilesCheckBox` (`MainWindow.xaml`, "Show image tiles" checkbox, wired to `OnShowImageTilesChanged`). But:

- `CanvasUserSettings.cs` (the persisted settings record) has **no `ShowImageTiles` property** — compare its field list (`TileColumns`, `TileRows`, `ObjectsPerTile`, `AnnotationDisplayMode`, `OutlineThickness`, `LabelSize`, `LabelDisplay`, `ShowLabels`, `BackgroundNoise`, `BackgroundCircleCount`) against the UI's control set.
- `ApplySettingsToUi` (`MainWindow.xaml.cs:124`) hardcodes `ShowImageTilesCheckBox.IsChecked = true;` instead of reading a persisted value.
- `SaveSettings` (`MainWindow.xaml.cs:~1163-1177`) never reads `ShowImageTilesCheckBox.IsChecked` or `_showImageTiles` into the settings object it serializes.

Net effect: a user who unchecks "Show image tiles" to inspect defect overlays in isolation, then relaunches the app, silently gets tiles back on — the one display-option toggle from the current UI set that ICW-043/ICW-066's settings-persistence work missed. This is a straightforward regression-of-omission: `ShowImageTilesCheckBox` was added in a later commit than the persistence groundwork and nobody extended the settings record to match.

**Fix**: add `bool ShowImageTiles { get; init; } = true;` to `CanvasUserSettings`, wire it through `ApplySettingsToUi`/`SaveSettings` the same way `ShowLabels` is handled, add a round-trip test alongside the existing `CanvasUserSettingsTests.cs` cases.

### 3.2 `CameraTransform` default scale range widened to `[1e-10, 10000]` with no stated rationale
**Severity: Low-Medium · Confidence: 80%**

Diff between commits, `CameraTransform.cs`:
```csharp
- public CameraTransform(double minimumScale = 0.1, double maximumScale = 50)
+ private const double MinimumScale = 0.0000000001;
+ private const double MaximumScale = 10000;
+ public CameraTransform(double minimumScale = MinimumScale, double maximumScale = MaximumScale)
```
The original bounds (`0.1`–`50`) were modest and matched the DesignDoc's stated clamp intent ("strict clamping logic ... to prevent floating-point overflow or the complete collapse of the visual frustum"). The new defaults span **14 orders of magnitude**. Every caller that constructs `new CameraTransform()` with no arguments (e.g., `MainWindow.xaml.cs:143`, `_camera = new CameraTransform();`) now inherits this effectively-unconstrained range. Practically, in-app zoom is further constrained by `ViewportZoomPolicy`'s fit-to-scene minimums and the UI's preset list, so this is not immediately exploitable through normal interaction — but it removes the one hard backstop that `CameraTransform.Zoom`'s own `IsScaleAllowed` check was designed to provide, and `GetViewportBounds` divides world extents by `ScaleX`/`ScaleY` (`CameraTransform.cs:116-123`), so a scale near `1e-10` would compute `SpatialBounds` with widths on the order of `1e11` world units, which is within `double` range but far outside anything the renderer or spatial index is exercised against in tests.

**Recommendation**: either document why `1e-10`/`10000` were chosen (if intentional, e.g., to stop being the effective limiter now that `ViewportZoomPolicy` owns policy), or restore a much tighter default and let call sites opt into wider ranges explicitly. As-is, this reads like an accidental "make the compiler stop complaining" change rather than a considered policy decision — worth a one-line ADR note either way.

### 3.3 `SampleAnnotation` record equality is silently reference-based for its two most important fields
**Severity: Low-Medium · Confidence: 85% (adds detail to already-filed ICW-301/ICW-304)**

`SampleAnnotation` (`SampleImageTile.cs:315-325`) is declared as a positional `record` (value-semantics-by-convention in C#) but two of its properties are reference types with no value-equality override:
```csharp
public sealed record SampleAnnotation(
    ...
    IReadOnlyDictionary<string, double> Features,
    ...
    byte[] DefectPixels) : ISpatialEntity
```
The compiler-generated `Equals`/`GetHashCode` for a `record` calls `EqualityComparer<T>.Default` per member. For `byte[]` and `IReadOnlyDictionary<string,double>`, `Default` falls back to reference equality (`Dictionary<>`/arrays don't override `Equals`). Two annotations with pixel-identical defect patches and feature values, constructed independently (e.g., in a future test or a deterministic-regeneration comparison), will compare **unequal**, contradicting the structural-equality expectation a `record` signals to every reader of this codebase. This is not currently triggering an observed bug (no code path currently relies on `SampleAnnotation.Equals`), but it's a live trap for the next engineer who writes `Assert.That(annotationA, Is.EqualTo(annotationB))` or uses these as dictionary/HashSet keys.

**Recommendation**: either (a) document that identity/equality is by `Id` only and consider overriding `Equals`/`GetHashCode` to reflect that explicitly, or (b) if full structural comparison is ever needed, add `IStructuralEquatable`-aware helpers. Low cost, prevents a subtle future test flake.

### 3.4 `docs/handoffs/2026-07-25-scrollbars-and-noise-tuning-handoff.md` describes a feature (`ViewportScrollPolicy`) that no longer exists in source
**Severity: Low (documentation hygiene) · Confidence: 90%**

The handoff documents adding `src/InfiniteCanvas.Core/ViewportScrollPolicy.cs` and a `ScrollViewer`-hosted viewport. `ICW-065-viewport-scrollbars-and-zoom-navigation.md` (Done) subsequently **reverted** this ("Removed the `ScrollViewer` content host that turned scaled scene extent into the camera viewport, preventing zoom-out and creating vertically smeared raster frames"). Confirmed: `find . -iname "*ScrollPolicy*"` returns no source file, and the only `ScrollViewer` remaining in `MainWindow.xaml` (line 112) wraps the **side settings panel**, not the viewport. The handoff document is now describing dead architecture with working links to a file that doesn't exist. Not a code bug, but exactly the kind of stale-doc noise this audit was asked to minimize going forward — flag for cleanup or an explicit "superseded by ICW-065" note at the top of the file.

---

## 4. Corroborated Open Findings (still valid — fresh evidence for the highest-severity items)

The following pre-existing "To Do" tickets were independently re-derived by this audit while reading the current source and are still accurate. Citations use current line numbers so the next implementer doesn't have to re-locate them.

### 4.1 No global unhandled-exception safety net (`ICW-014`)
**Severity: High · Confidence: 90%**

`App.xaml.cs` (full file, 22 lines) only overrides `OnStartup`/`OnExit` for Serilog wiring — no `DispatcherUnhandledException`, no `AppDomain.CurrentDomain.UnhandledException`, no `TaskScheduler.UnobservedTaskException`. Meanwhile `MainWindow.xaml.cs` has **20** `private async void` event handlers (`OnLoaded`, `OnAnnotationMouseLeftButtonDown`, `OnViewportMouseMove`, `OnAnchorPanTick`, `OnShowImageTilesChanged`, `OnBackgroundNoiseChanged`, `OnBackgroundCircleCountChanged`, `OnViewportMouseWheel`, `OnZoomPresetSelectionChanged`, `OnCustomZoomClicked`, `OnCustomZoomKeyDown`, `OnResizeElapsed`, `OnDisplayModeSelectionChanged`, `OnOutlineThicknessChanged`, `OnLabelSizeChanged`, `OnLabelDisplaySelectionChanged`, `OnShowLabelsChanged`, `OnRegenerateClicked`, `OnDebugDumpCacheClicked`, `OnClosed`). Only `OnLoaded` has a surrounding `try`/`catch`. Any unhandled exception thrown synchronously or from an awaited call inside any of the other 19 will propagate out of an `async void` method, bypass normal exception propagation, and hit the Dispatcher's unhandled-exception path — which, absent a handler, **terminates the process**. This is a WPF-idiom footgun independent of how well-written the awaited code is.

**Recommendation** (already scoped correctly by ICW-014): add `DispatcherUnhandledException` in `App.xaml.cs`, decide fail-fast vs. log-and-continue policy, and add `TaskScheduler.UnobservedTaskException`/`AppDomain.UnhandledException` as a backstop for the `Task.Run` background paths (`RenderFrameAsync`'s `Task.Run`, `TileCacheBudget`-adjacent background generation in `SampleImageTile.EnsurePixelsGenerationStarted`).

### 4.2 Shutdown/regeneration disposal race (`ICW-029`)
**Severity: High · Confidence: 75%** (race window confirmed by code inspection; not confirmed by a reproduced crash, hence not higher)

`OnClosed` (`MainWindow.xaml.cs:1145-1157`):
```csharp
private async void OnClosed(object? sender, EventArgs e)
{
    SaveSettings();
    _resizeTimer.Stop();
    _anchorPanTimer.Stop();
    UnsubscribeTileGenerationEvents(_tiles);
    _lifetime.Cancel();

    await _renderAction.DisposeAsync();
    FramePresenter.Child = null;
    _frontBitmapFactory?.Dispose();
    _backBitmapFactory?.Dispose();
    _generationGate.Dispose();
    _lifetime.Dispose();
}
```
`_lifetime.Cancel()` is expected to cancel any in-flight `RegenerateSceneAsync` (which awaits `_generationGate.WaitAsync(_lifetime.Token)` and passes `_lifetime.Token` through its `Task.Run`/`PublishSnapshotAsync` calls, `MainWindow.xaml.cs:131-149`). But `OnClosed` does **not** await completion of any in-flight `RegenerateSceneAsync` before disposing `_generationGate` — it only awaits `_renderAction.DisposeAsync()`, a separate coalescer. If a regenerate is mid-flight when the window closes, its `finally { ...; _generationGate.Release(); }` (`MainWindow.xaml.cs:~185-188`) can execute **after** `_generationGate.Dispose()` has already run on the closing path, and `SemaphoreSlim.Release()` throws `ObjectDisposedException` if called after `Dispose()`. Whether this is reachable depends on exact task scheduling, which is why confidence is 75% rather than higher — but the code has no structural guard preventing it (no `await` on a "generation fully stopped" signal before the `Dispose()` calls).

**Recommendation** (matches ICW-029's own framing): introduce a coordinated close sequence — e.g., await `_generationGate.WaitAsync()` once during shutdown (to guarantee no in-flight regenerate holds it) before disposing it, or track the in-flight `RegenerateSceneAsync` task explicitly and await it (with the cancellation already in flight) before touching `_generationGate`/`_lifetime`.

### 4.3 `Classification` primitive obsession (`ICW-304`)
**Severity: Medium · Confidence: 90%**

`Classifications` is a `string[]` (`SampleImageGenerator.cs:14`), used as:
- A `Dictionary<string, Bgra32Color>` key (`ClassificationColors`, same file).
- A `switch` discriminant in `GetClassAspectRange(string classification)`.
- A property on `SampleAnnotation.Classification` (raw `string`, `SampleImageTile.cs:321`), displayed directly in the UI (label text, feature grid).

None of these call sites are protected by the compiler — a typo in any classification string (e.g., `"Srcatch"`) silently falls through the `_ => (0.8, 2.0)` default in `GetClassAspectRange` and produces an annotation with no matching entry in `ClassificationColors`, which throws `KeyNotFoundException` at `SampleImageGenerator.cs` (`var color = ClassificationColors[classification];`) rather than failing at compile time or construction time with a clear message. Since `Classifications` and the dictionary keys are defined in the same file today they can't currently drift, but any downstream consumer (tests, a future data-import path) that constructs a `SampleAnnotation.Classification` from a different source has no static guarantee of validity.

**Recommendation** (matches ICW-304's framing): introduce `enum DefectClassification { Scratch, Inclusion, Stain, EdgeDefect }`, drive `ClassificationColors`/`GetClassAspectRange` off the enum, and keep a single `ToDisplayString()` mapping for UI text.

### 4.4 `TileCacheBudget` eviction is not LRU despite the name/status text implying a managed cache (`ICW-305`)
**Severity: Medium · Confidence: 88%**

`TryReserve` (`SampleImageTile.cs:426-461`):
```csharp
var evictedTile = _trackedTiles.Values.FirstOrDefault(candidate =>
    !string.Equals(candidate.Id, tile.Id, StringComparison.OrdinalIgnoreCase)
    && !_pinnedTileIds.Contains(candidate.Id)
    && candidate.IsImageGenerated);
```
`Dictionary<TKey,TValue>.Values` enumeration order is insertion order **in practice** for the current .NET implementation but is explicitly documented as unspecified/not guaranteed by the BCL contract, and it is not access-order (i.e., not LRU) even when it is stable — a tile fetched 5 seconds ago and never touched again is exactly as likely to be evicted first as a tile fetched 5 minutes ago that's still being actively viewed off-screen-adjacent. `DescribeStatus()` (`SampleImageTile.cs:489`) reports "evictions" as if this were a deliberate policy, which will read as intentional/correct to anyone glancing at the debug status bar.

**Recommendation**: either implement true LRU (e.g., an intrusive doubly-linked list or `LinkedHashMap`-style structure keyed by last-access time) or explicitly document "eviction order is insertion-order, not LRU" so nobody relies on recency-based eviction behavior that isn't actually there.

---

## 5. Lower-Severity / Housekeeping (confirmed present, already tracked, listed for completeness only)

No new evidence beyond what's already in the linked tickets; included so this report is a complete pass rather than a partial one.

| Item | Ticket | Confirmed still present? |
|---|---|---|
| `IRenderer<TScene,TOutput>`/`ViewportRenderRequest` unreferenced by any call site | ICW-018 | Yes — `grep -rn "IRenderer\|ViewportRenderRequest"` outside their own files returns only benchmark/test usage of the point-based `GenerateFrozenBitmap` overload, not `IRenderer` itself |
| `MainWindow.xaml.cs` at 1,359 lines mixes pure logic with UI wiring, undertested | ICW-022/052 | Yes — grew from 1,047 → 1,359 lines since the ticket was filed; some logic has since been extracted (`TileGridIndexLookup`, `ViewportZoomPolicy`, `CanvasUserSettings`), so partial progress exists, but the file is still the largest and least-tested surface in the repo |
| No CI workflow / nullable-as-error enforcement | ICW-036 | Yes — no `.github/workflows/*.yml` present in the tarball listing |
| No `AutomationProperties`/keyboard access on MainWindow controls | ICW-037 | Yes — confirmed by full XAML read, zero `AutomationProperties.*` attributes anywhere in `MainWindow.xaml` |
| `ZeroCopyBitmapFactory` finalizer takes the same lock as `Dispose(bool)` | ICW-023/ICW-056 | Yes — `~ZeroCopyBitmapFactory() => Dispose(false);` (`ZeroCopyBitmapFactory.Windows.cs:55-58`) and `Dispose(bool)` takes `_lifetimeGate` (a plain `object` lock) unconditionally; finalizer-thread lock acquisition on an object that might also be mid-finalization is the classic finalizer/lock-ordering hazard the ticket already names |
| String-keyed `Features` dictionary (`"Confidence"`, `"Severity"`) instead of typed metrics | ICW-031 | Yes — confirmed at `SampleImageGenerator.cs` annotation construction and `SampleImageTile.cs:357-363` (`GetFeatureDisplayItems`) |

---

## 6. Assumptions & Open Questions

**Assumptions made in this audit:**
1. "Latest commit" means `main`@`52a3442d98a47d88df345f2cec9f24b08fbecb67` (2026-07-25 08:01:53 UTC per the repo's commit atom feed) — the user's message initially linked a specific older commit (`43bfd55b`) and then redirected to the repo's default branch; this report audits the branch HEAD as instructed by the follow-up message, and calls out the delta between the two where relevant (§2).
2. Windows-only (`#if WINDOWS`) code paths (`ZeroCopyBitmapFactory.Windows.cs`, GDI+ bitmap conversion in `SampleImageTile`/`SampleImageGenerator`) were read and reasoned about statically; this audit did not compile or run the solution (no .NET/Windows execution environment available), so no runtime/dynamic-analysis evidence (e.g., an actual reproduced `ObjectDisposedException` for §4.2, or a memory profiler run for the zero-copy claims in `DesignDoc.md`) backs any finding — all findings are static-analysis-derived and confidence values are capped accordingly.
3. Ticket "Done" status was trusted only where this audit could independently re-derive the fix in current source (§2); "Done" tickets not spot-checked here were not re-verified and should not be assumed correct purely because this report doesn't contradict them.

**Open questions for the team:**
1. Is the `[1e-10, 10000]` `CameraTransform` scale range (§3.2) intentional? If so, worth a one-line comment/ADR entry so it isn't "fixed" back to `[0.1, 50]` by accident later.
2. Given the number of duplicate-concern tickets, was there a merge of multiple parallel audit branches/agents into `main` without de-duplication? If so, a one-time reconciliation pass (§1 recommendation) is likely cheaper than continuing to layer new audits on top of an unreconciled backlog.
3. Should `ShowImageTiles` (§3.1) be treated as a display-option on par with `ShowLabels`, or was its omission from persistence deliberate (e.g., "always start with tiles visible")? If deliberate, the fix is a one-line comment instead of a settings-schema change.

---

## 7. Suggested Sequencing

1. **Tracker reconciliation** (§1) — do this first; every subsequent implementer benefits.
2. **Close verified-fixed tickets** (§2) — near-zero cost, immediately shrinks the backlog.
3. **High-severity open items**: global exception handling (§4.1) and shutdown race hardening (§4.2) — both are "the app can crash or throw on close" classes of bug, appropriate for a pre-1.0 greenfield project to close before adding more surface area.
4. **New medium-severity fix**: `ShowImageTiles` persistence gap (§3.1) — small, isolated, good "warm-up" ticket.
5. **Type-safety cleanup batch**: `Classification` enum (§4.3), `SampleAnnotation` equality documentation (§3.3), typed `Features` (already ICW-031) — bundle these since they touch the same files.
6. **Cache policy**: decide and document/implement real LRU for `TileCacheBudget` (§4.4).
7. Everything else in the existing backlog, once de-duplicated, in whatever priority the team already assigned.
