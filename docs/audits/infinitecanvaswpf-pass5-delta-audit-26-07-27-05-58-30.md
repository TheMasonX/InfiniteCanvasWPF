# InfiniteCanvasWPF — Audit Pass 5 (Delta Only)

**HEAD audited:** `ffe990a98e645759dbc8178613eb4d69d83c2aa0` ("Fix two render-path crashes and consolidate defect-template lifecycle")
**Baseline:** `infinitecanvaswpf-audit-pass4-delta-26-07-26-15-13-15.md` (HEAD `d74dde2`) plus all 21 prior audit docs and the full `docs/tasks/tickets/` corpus (155 files, IDs ICW-001 through ICW-144, AGT-001/002/005, TESTS-001, and the unnumbered `ticket-*.md` files), all read before writing anything below.
**6 new commits since pass 4:** `e1c4e08` (direct Gray8 materialization + doc backfill) → `54659f1` (mip-fallback ordering fix + exception logging) → `e8fb440` (Perlin-style noise rework) → `c5ef215` (WIP checkpoint) → `83d4b68` (SampleImageGenerator refactor: `GeneratorOptions`/`MipOptions`, extracted `AnnotationGenerator`/`DefectTemplateFactory`) → `ffe990a` (fixes ICW-145 mip-index bug + defect-bitmap-pool disposal, adds `Bgra32BufferLayout` overflow guard, adds `MainViewModel`/MVVM for the noise-settings panel).
**Method:** Per-commit patch review (not just HEAD-vs-baseline diff) for every changed file, full re-read of `MainWindow.xaml.cs`, `SampleImageGenerator.cs`, `SampleImageTile.cs`, `ZeroCopyBitmapFactory.Windows.cs`, and every newly-added file, cross-checked against all 155 existing ticket files to exclude duplicates.

This report contains only new findings, a status correction, and one verified-fixed note. Nothing here repeats existing ticket content.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **Every scene regeneration silently discards the user's Background Noise panel settings** (target value, noise, circle count, scale, octaves, lacunarity, gain, amplitude) and resets them to hardcoded defaults — including the automatic regenerate that fires on app startup, which fires *immediately after* the persisted settings were correctly loaded and applied. Root cause: `InitializeSpatialState()` unconditionally does `_mainViewModel = new MainViewModel(); DataContext = _mainViewModel;` and is called both from the constructor **and** from `RegenerateSceneAsync`. | **High** | 95% |
| 2 | The new `SampleImageTile.DisposeDefectTemplatePools(_tiles)` call — added this pass specifically to fix the previously-known "defect bitmap pool is never disposed" leak — disposes the outgoing scene's shared GDI+ `Bitmap`s with no synchronization against an in-flight `RenderFrameAsync` background task that may still be calling `bitmap.LockBits` on the very same bitmaps via `DrawDefectPatch`. This is exactly the hazard `ICW-103` was opened to prevent; the leak is now fixed but the concurrency hazard `ICW-103` describes is now live/reachable rather than theoretical. | **High** | 80% |
| 3 | `GeneratorOptions.ImageCount` defaults to `SampleImageGenerator.DefaultPixelWidth` (8192) — an unrelated constant borrowed only because a record parameter needed *some* default, self-flagged by the author's own inline comment (`// placeholder, overwritten by default usage`). Currently harmless because the single production call site always supplies `ImageCount` explicitly, but it's a live landmine for the next caller (tests, a future preset/profile feature) that trusts the record's own default. | Medium | 90% |
| 4 | **Status correction, still open:** Pass 4's headline finding — `RenderRequestTracker` (`ICW-078`'s stale-frame-publication guard) was implemented then reverted by `9247bff`, while `active-tasks.md`/`JIRA.md` still say `ICW-078` is `Done` — is **still unresolved 6 commits later**. `grep -c "RenderRequestTracker" MainWindow.xaml.cs` is still `0` at current HEAD. A tracking ticket (`ICW-100`, "Re-apply and verify `RenderRequestTracker` wiring") exists and is still `Proposed`. No code change in this window touched this. | High (unchanged from pass 4) | 95% |
| 5 | **Verified fixed, no action needed:** the `averageConversionMilliseconds` computation introduced at commit `e1c4e080` called `tile.BitmapConversionDuration!.Value` on a property that had just been hardcoded to always return `null` (dead conversion-timing code left over from the removed GDI+ pipeline) — that would have thrown `InvalidOperationException` on the first render after any tile finished generating. Commit `ffe990a9` (this same window) independently fixed it by filtering on `.HasValue` first. Flagging only to confirm the fix is complete and to recommend deleting the now-permanently-dead properties (see §5/discussion below). | — (informational) | 95% |
| 6 | The status bar's `Gray8 {X} ms` segment will now permanently display `Gray8 0.0 ms` — `BitmapConversionDuration` and `BitmapGenerationDuration` are dead properties (hardcoded `=> null`) left over from the GDI+-conversion removal in `ICW-097`; nothing populates them anymore. Not a bug, but a permanently-misleading diagnostic readout. | Low | 90% |

**Bottom line:** two High findings this pass are UI/data-integrity regressions introduced by genuinely new code (the MVVM wiring and the defect-pool disposal fix), not restatements of old debt. Finding #1 in particular means a recently-shipped feature (`ICW-066`/background-noise tuning, extended this pass with Perlin-style parameters) doesn't actually persist across the one user action — Regenerate — that the feature exists to support.

---

## 1. [HIGH] Background noise settings reset to defaults on every Regenerate
**Confidence: 95%** — traced end-to-end through binding, VM construction, and call graph; not inferred.

**Call graph:**
```
MainWindow()                     → InitializeSpatialState()   // creates MainViewModel #1 (defaults)
                                  → ApplySettingsToUi(Load())  // correctly applies persisted settings onto #1
Loaded → OnLoaded → RegenerateSceneAsync(fitToWidth: true)
                                  → InitializeSpatialState()   // creates MainViewModel #2 (defaults, discards #1)
                                  → CreateBackgroundNoiseSnapshot()  // reads from #2 — defaults, not the user's settings
```
`InitializeSpatialState` (`MainWindow.xaml.cs:95-101`):
```csharp
private void InitializeSpatialState()
{
    _spatialIndex = new LiveSpatialIndexService<SampleAnnotation>(new StrTreeSpatialIndexBuilder<SampleAnnotation>());
    _viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);
    _mainViewModel = new MainViewModel();
    DataContext = _mainViewModel;
}
```
This method's original purpose (per its name and its one other caller) is to reset the *spatial index* at the start of a scene regeneration. The `_mainViewModel`/`DataContext` lines were added alongside it and inherit its two call sites — one of which (`RegenerateSceneAsync`, line 165) fires on every Regenerate click and once automatically at startup.

`TileBackgroundNoiseSettingsView` is bound in XAML as:
```xml
<controls:TileBackgroundNoiseSettingsView
    ViewModel="{Binding DataContext.TileBackgroundNoiseSettings, RelativeSource={RelativeSource AncestorType=Window}}" />
```
so it tracks `Window.DataContext` live. When `DataContext` is replaced, the binding re-resolves to the new `MainViewModel`'s `TileBackgroundNoiseSettings`, which is a fresh `TileBackgroundNoiseSettingsViewModel` with hardcoded field initializers (`targetValue = 128`, `noise = 8`, `circleCount = 3`, `scale = 1`, `octaves = 5`, `lacunarity = 2.5`, `gain = 0.6`, `amplitude = 1`) — not the values the user set, and not the values `ApplySettingsToUi` had just loaded from disk.

`RegenerateSceneAsync` then immediately does:
```csharp
InitializeSpatialState();                     // MainViewModel #2, defaults, DataContext swapped
...
var backgroundNoiseSettings = _mainViewModel.CreateBackgroundNoiseSnapshot();  // reads #2 — defaults
_tiles = await Task.Run(() => SampleImageGenerator.GenerateSet(..., targetValue: backgroundNoiseSettings.TargetValue, ...));
```
There is no call to `_mainViewModel.ApplySettings(...)` after the reset inside `RegenerateSceneAsync`. Net effect:
- **Startup:** persisted settings load correctly, then are discarded a moment later when the auto-regenerate fires, before the user ever generates a scene with their saved values.
- **Every subsequent Regenerate click:** whatever the user just dialed in on the noise sliders is discarded and replaced with the hardcoded defaults, and the scene that gets generated uses those defaults — silently. The sliders themselves visibly snap back too, since they're bound through the same `DataContext` chain.
- Note `octaves` default is `5` here vs. `3` everywhere else in the codebase (`GeneratorOptions.NoiseOctaves`, `SampleImageGenerator.NoiseSettings.Default`, `GenerateSet`'s own `noiseOctaves` parameter default) — an incidental config-drift side effect of the same bug, worth folding into the fix rather than ticketing separately.

**Why prior tests didn't catch it:** `CanvasUserSettingsTests` covers JSON round-trip of `CanvasUserSettings`, not the runtime `DataContext`/binding path, and there is no existing test that regenerates twice and asserts the noise settings survive. This is the same "green tests hide a real regression" pattern pass 4 flagged for `ICW-078` — a second, independent instance of it in this window.

**Recommendation:**
- Stop constructing `_mainViewModel` inside `InitializeSpatialState()`. Construct it once (constructor only), and have `RegenerateSceneAsync` reset only what it actually needs reset (spatial index / `_viewModel`).
- If a scene-scoped reset of `MainViewModel` is genuinely wanted for some other field on it later, split the method so `_mainViewModel.ApplySettings(currentSettings)` (or an explicit "keep noise settings" path) runs immediately after any future reset — don't rely on nobody adding unrelated state to a method literally named for a different concern.
- Add a regression test: set a non-default value on `TileBackgroundNoiseSettingsViewModel`, call `RegenerateSceneAsync` (or the underlying reset path), assert the value is unchanged. This is a "did DataContext get silently replaced" test in the same spirit as pass 4's suggested reflection/behavioral test for `ICW-078`.
- Fix the `Octaves` default drift (5 → 3) while in this code, or centralize all noise defaults in one place (`SampleImageGenerator.NoiseSettings.Default`) and have the ViewModel read from it instead of re-declaring its own literals.

---

## 2. [HIGH] New defect-template-pool disposal call has no synchronization against in-flight renders
**Confidence: 80%** — mechanism and absence of synchronization are both directly confirmed in code; likelihood of the race actually being hit in a given session is not measured (no repro attempted, hence not 95%+).

This pass's `ffe990a9` commit message states the intent plainly: *"the shared defect template pool was disposed on per-tile cache eviction, invalidating all annotations' DefectBitmap references. Remove DisposePool from eviction path; add SampleImageTile.DisposeDefectTemplatePools() called once at the scene-regeneration boundary."* That is a correct fix for the bug it targets (eviction-time disposal invalidating live bitmaps) and is a genuine improvement — the pool is now disposed at all (closing the leak pass 4 and `ICW-102`/`ICW-103` both flagged), and it's no longer disposed mid-frame from cache eviction.

But the new call site is:
```csharp
// RegenerateSceneAsync, MainWindow.xaml.cs:174
SampleImageTile.DisposeDefectTemplatePools(_tiles);   // disposes OLD scene's bitmaps, on the UI thread
```
run before `_tiles` is reassigned to the new scene (`_tiles = await Task.Run(() => SampleImageGenerator.GenerateSet(...))` a few lines later). Nothing here cancels, awaits, or otherwise fences against a `RenderFrameAsync` invocation whose `Task.Run` body (`ZeroCopyBitmapFactory.GenerateFrozenBitmap` → `DrawDefectPatch` → `bitmap.LockBits(...)`) may already be executing on a thread-pool thread against the *same* old tiles/annotations at the moment `RegenerateSceneAsync` runs. The two paths share no lock:
- `_generationGate` (a `SemaphoreSlim(1,1)`) only serializes `RegenerateSceneAsync` against itself — it is never acquired by `RequestRenderAsync`/`RenderFrameAsync`.
- `RenderFrameAsync` is invoked via the coalescing `_renderAction`, which runs independently of the generation gate; a render triggered by a pan/zoom/hover event immediately before a Regenerate click can still be inside its `Task.Run` when `DisposeDefectTemplatePools` runs.

If that overlap occurs, `Bitmap.Dispose()` racing `LockBits`/`UnlockBits` on the same GDI+ handle from another thread is undefined behavior at the P/Invoke layer — anywhere from a clean `ArgumentException`/`ObjectDisposedException` (which `ffe990a9`'s commit message says it was chasing in the first place) to an unmanaged access violation, depending on timing. This is precisely `ICW-103`'s stated concern (*"Protect DefectBitmap GDI+ usage from concurrent mutation/dispose during background render"*) — that ticket's premise no longer needs the correction pass 4 gave it (§4 of that report, "no dispose call exists yet"), because the dispose call now exists. `ICW-103` should be treated as active/blocking, not background cleanup.

**Recommendation:**
- Do not dispose the outgoing pool until any render that might still be reading it has completed. Cheapest correct fix: have `RegenerateSceneAsync` cancel/await the in-flight render (or take the same gate `RenderFrameAsync` respects) before calling `DisposeDefectTemplatePools`, mirroring how `_generationGate` already serializes regenerate-against-regenerate.
- Alternatively, defer disposal by one generation — dispose the *previous-previous* pool instead of the *previous* one, guaranteeing at least one full render cycle has elapsed. Cheaper to implement, weaker guarantee; only reach for this if the synchronization fix is more invasive than the team wants right now.
- Either way, this should close out `ICW-103` for real rather than leaving it as a parallel, easily-forgotten ticket next to the new disposal code that just landed in the same file.

---

## 3. [MEDIUM] `GeneratorOptions.ImageCount` default is a borrowed, unrelated constant
**Confidence: 90%**

```csharp
public sealed record GeneratorOptions(
    int ImageCount = SampleImageGenerator.DefaultPixelWidth, // placeholder, overwritten by default usage
    int PixelWidth = SampleImageGenerator.DefaultPixelWidth,
    ...
```
`DefaultPixelWidth` is `8192` — a tile's pixel width, with no semantic relationship to "number of tiles." The comment is the author's own admission this isn't a real default. The one production construction site (`SampleImageGenerator.GenerateSet(int imageCount = 64, ...)` forwarding overload) always supplies `ImageCount` explicitly, so this is currently unreachable — but `GeneratorOptions` is a public record; any future direct caller (a settings-profile feature, a test, a benchmark) that does `new GeneratorOptions(Columns: 4)` expecting "sensible defaults, override only what I care about" — which is the entire point of using a positional record with defaults — silently gets `ImageCount = 8192`, and `GenerateSet(GeneratorOptions)` would attempt to build 8192 (or, if `Rows` is also unset, `Math.Ceiling(8192/Columns)` rows) tiles at up to `8192×4096` pixels each. That's not a hang risk today only because nothing calls it that way yet.

**Recommendation:** Give `ImageCount` its own honest default (`64`, matching the forwarding overload's default, which is exactly what it's meant to preserve compatibility with) or make it a required positional parameter with no default at all — a record component with no sane default is a legitimate design, and is safer than borrowing an unrelated one. Do this while `ICW-088`/`ICW-090` (parameter-count/casts cleanup, already in the backlog and touching this exact type) is in flight rather than as a separate ticket.

---

## 4. Status correction: `ICW-078` regression is still live at current HEAD
**Confidence: 95%**

```
$ grep -c "RenderRequestTracker" src/InfiniteCanvas.App/MainWindow.xaml.cs
0
```
Unchanged since pass 4. `ICW-100` ("Re-apply and verify `RenderRequestTracker` wiring (ICW-078 regression)") exists in `active-tasks.md` and is still `Proposed`; `ICW-078` itself and its `JIRA.md` row still both say `Done`. None of the six commits in this window touched `RenderRequestTracker`, `BeginRequest`, or `IsCurrent`. This is not a new finding — it's confirmation that the tracker's own bookkeeping is still wrong about a live correctness bug, four audit passes and six commits after it was first caught. Re-flagging only because leaving it unmentioned in a "what's the current state" pass would itself be misleading.

**Recommendation:** unchanged from pass 4 — re-apply the four reverted lines from `3dc49da`, correct `ICW-078`'s status, add the suggested "did the wiring get removed again" regression test.

---

## 5. Verified fixed: `BitmapConversionDuration` null-dereference (no action needed)
**Confidence: 95%**

Commit `e1c4e080` in this window removed the GDI+-to-Gray8 conversion path and hardcoded `BitmapGenerationDuration`/`BitmapConversionDuration` to always return `null` (there's no conversion step left to time). It left the status-bar computation reading the old shape:
```csharp
var completedTiles = _tiles.Where(tile => tile.GenerationDuration.HasValue).ToArray();
var averageConversionMilliseconds = completedTiles.Length == 0
    ? 0
    : completedTiles.Average(tile => tile.BitmapConversionDuration!.Value.TotalMilliseconds);
```
Since `completedTiles` is filtered on `GenerationDuration`, not `BitmapConversionDuration`, and the latter is now always `null`, `.Value` on the null-forgiven `Nullable<TimeSpan>` would throw `InvalidOperationException` on the first render after any tile finished generating — i.e., almost immediately in normal use, and repeatedly on every subsequent frame, silently truncating `RenderFrameAsync` right after the frame bitmap was already published (so the picture would still render, but zoom%, scrollbar sync, and pixelometer updates downstream of that line would stop firing).

Commit `ffe990a9` (same window, independent of this audit) fixed it correctly:
```csharp
var completedConversionTiles = completedTiles.Where(tile => tile.BitmapConversionDuration.HasValue).ToArray();
var averageConversionMilliseconds = completedConversionTiles.Length == 0
    ? 0
    : completedConversionTiles.Average(tile => tile.BitmapConversionDuration!.Value.TotalMilliseconds);
```
Confirmed correct at current HEAD. No action needed beyond §6 below (the properties this guarded are now permanently dead).

---

## 6. [LOW] Dead diagnostic properties + permanently-misleading status text
**Confidence: 90%**

`BitmapGenerationDuration` and `BitmapConversionDuration` on `SampleImageTile` are now `=> null` unconditionally (the GDI+ conversion step they measured no longer exists after `ICW-097`). `BitmapGenerationDuration` has zero remaining readers anywhere in `src/`. `BitmapConversionDuration` has exactly one reader (§5's now-guarded computation), which will always evaluate to an empty sequence, so `averageConversionMilliseconds` is always `0` and the status bar will permanently show `Gray8 0.0 ms` — a diagnostic readout that can never report anything else again. See discussion below for a concrete redesign; this line item is just "delete the dead surface, or repurpose it" — see the direct answer to that question in the accompanying message rather than duplicating it here.

**Recommendation:** Delete both properties and the `Gray8 {X} ms` status segment, or repurpose the segment (see design discussion). Either way, don't leave a diagnostic that structurally cannot report a non-zero value — that's a worse trap than no diagnostic at all, because a future reader will reasonably assume `0.0 ms` means "fast," not "impossible."

---

## Suggested Priority

1. **§1** — silently breaks a just-extended, user-facing feature (background noise tuning) on the one action (Regenerate) it exists for; cheap, well-understood fix.
2. **§2** — real GDI+ concurrency hazard on a newly-added, reachable code path; closes out `ICW-103` for real once fixed.
3. **§4** — not new, but still the single highest-severity open item in the whole backlog by pass 4's own assessment, and it has now survived six more commits unaddressed.
4. **§3** — cheap, bundle with the in-flight `ICW-088`/`ICW-090` parameter-object cleanup.
5. **§6** — trivial, batch with any other low-priority pass; see design discussion for the preferred direction.

---

## Assumptions & Open Questions

- Assumed `RelativeSource={RelativeSource AncestorType=Window}}` in `TileBackgroundNoiseSettingsView`'s binding resolves to `MainWindow` at runtime (standard WPF behavior for a `UserControl` hosted inside a `Window`); not run against a live build to confirm, since no Windows build/test execution was performed as part of this pass (source-only static audit, consistent with prior passes' methodology).
- §2's severity (High vs. Medium) hinges on how often a render is actually in-flight at the moment Regenerate is clicked in real usage; this wasn't measured. Confidence is 80% on the *mechanism* being real and unguarded, not on a measured hit-rate — treat the severity as a ceiling pending a repro or a deliberate stress test (this would be a natural fit for the already-planned `ICW-144` fast-scroll/queue stress work if extended to cover regenerate-during-render).
- Did not re-verify Release build or run the test suite for this pass (no Windows execution environment available); all findings are from static source reading of the tarball at the stated HEAD SHA.
