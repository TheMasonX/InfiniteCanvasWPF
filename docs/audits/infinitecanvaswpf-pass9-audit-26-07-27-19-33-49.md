# InfiniteCanvasWPF — Audit Pass 9 (Same HEAD, Settings Persistence)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since pass 6; verified before writing).
**Scope this pass:** `CanvasUserSettings.cs`/`CanvasUserSettingsStore`, traced end-to-end against `SampleImageGenerator`'s own parameter validation and `ICW-015`'s closure notes.

One concrete, reproducible-by-inspection gap this pass, plus a precise correction of a claim in an already-`Done` ticket.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **`CanvasUserSettings.IsValid` has no upper bound on `ObjectsPerTile`**, while `SampleImageGenerator.MaxObjectsPerTile = 256` is enforced by `GenerateSet` as a hard `throw`, not a clamp. A settings.json with `ObjectsPerTile` between 257 and `int.MaxValue` passes `IsValid` (which only checks `>= 0`) and loads successfully; `MainWindow`'s auto-regenerate-on-startup path (`OnLoaded` → `RegenerateSceneAsync`) passes the loaded `_objectsPerTile` straight to `GenerateSet` with **no** re-validation — that validation only exists on the manual "apply from textbox" path. The result is a caught, non-crashing but broken first run: `OnLoaded`'s catch-all shows `INITIALIZATION FAILED` with the exception message, and — because `SaveSettings()` persists whatever `_objectsPerTile` currently holds — a bad value written once can round-trip back out unchanged on every subsequent launch until someone edits the JSON file directly or the in-app textbox is used to overwrite it (unverified whether the UI is left in a state that allows that self-recovery; see Open Questions). | Medium | 85% |
| 2 | **Corrects a claim in `ICW-015` (status: Done):** that ticket's own closure notes say *"`SampleImageGenerator.MaxObjectsPerTile` is the single policy constant used by the generator and mirrored by the MainWindow input validator."* True of the textbox validator (`MainWindow.xaml.cs:1386-1390`, confirmed matches `MaxObjectsPerTile`) — but `CanvasUserSettings.IsValid` is a third gate on the same value, on the settings-file load/save path, and it does not mirror the constant. "Single policy constant... mirrored by" undersells the actual number of independent validation sites (two, not one, plus the generator's own internal check) and the one that matters most here (the file-load path) is the one that doesn't match. | — (documentation accuracy) | 90% |
| 3 | Confirmed, precisely this time: `BackgroundNoiseOctaves` defaults split two ways — `CanvasUserSettings.BackgroundNoiseOctaves` and `MainViewModel`'s `TileBackgroundNoiseSettingsViewModel` both default to `5`; `GeneratorOptions.NoiseOctaves` and `SampleImageGenerator.GenerateSet`'s own `noiseOctaves` parameter both default to `3`. Two consistent camps, not a single stray value — refines pass 6's looser "5 vs 3" note into an exact map of which four call sites land on which side. | Low (refinement, not new) | 90% |

**By contrast, `BackgroundCircleCount` is handled the safer way end-to-end:** `CanvasUserSettings.IsValid` bounds it to `<= 8`, and `SampleImageGenerator`'s internal `Math.Clamp(circleCount, 0, 8)` silently corrects out-of-range values rather than throwing — the two layers agree, and even if they didn't, the consumer wouldn't crash. `ObjectsPerTile`'s hard-throw design is the one place this pattern breaks down, specifically because its two validation layers disagree about where the ceiling is.

---

## 1. [MEDIUM] `ObjectsPerTile` settings-file validation doesn't match the generator's hard limit

**Confidence: 85%**

```csharp
// CanvasUserSettings.cs:55-71 (IsValid)
public bool IsValid =>
    Version == CurrentVersion
    && TileColumns > 0
    && TileRows > 0
    && (long)TileColumns * TileRows <= 2000
    && ObjectsPerTile >= 0                       // <-- no upper bound
    && AnnotationDisplayMode is >= 0 and <= 2
    ...
```
```csharp
// SampleImageGenerator.cs:16, 94/146
public const int MaxObjectsPerTile = 256;
...
if (objectsPerTile is < 0 or > MaxObjectsPerTile)
{
    throw new ArgumentOutOfRangeException(...);   // hard throw, not a clamp
}
```
```csharp
// MainWindow.xaml.cs:148 (ApplySettingsToUi, runs on every startup)
_objectsPerTile = settings.ObjectsPerTile;        // no clamp/validation applied here either
```
```csharp
// MainWindow.xaml.cs:116-129 (OnLoaded)
private async void OnLoaded(object sender, RoutedEventArgs e)
{
    Loaded -= OnLoaded;
    try
    {
        ApplyGenerationControlsToUi();
        ApplyDisplayOptionsFromUi();
        await RegenerateSceneAsync(fitToWidth: true);   // uses _objectsPerTile directly, unvalidated
    }
    catch (OperationCanceledException ex) { Log.Debug(ex, "OnLoaded canceled"); }
    catch (Exception exception)
    {
        LoadingOverlay.Text = "INITIALIZATION FAILED";
        StatusText.Text = exception.Message;
    }
}
```
```csharp
// RegenerateSceneAsync — the auto-startup call path
_tiles = await Task.Run(
    () => SampleImageGenerator.GenerateSet(
        imageCount: tileCount,
        objectsPerTile: _objectsPerTile,   // straight from settings, unclamped, unvalidated
        ...
```
The manual path (editing the textbox and clicking Generate) *is* protected — `MainWindow.xaml.cs:1386-1390` checks `objectsPerTile > SampleImageGenerator.MaxObjectsPerTile` and shows a proper validation error before ever calling `GenerateSet`. The auto-startup path skips that check entirely.

**Consequence, traced but not run:** a settings.json with, say, `"ObjectsPerTile": 500` (passes `IsValid`, since `IsValid` only checks `>= 0`) loads cleanly, gets assigned to `_objectsPerTile`, and then `GenerateSet`'s own internal guard throws `ArgumentOutOfRangeException` inside the `Task.Run` on the very first automatic regenerate. `OnLoaded`'s `catch (Exception exception)` catches it — so this is not a crash — but it leaves the window showing `INITIALIZATION FAILED` with no tiles ever generated. Whether the user can then fix this from within the running app (by editing the `ObjectsPerTile` textbox and clicking Generate) or whether the failed initialization leaves other UI state uninitialized in a way that blocks that recovery path is not something this static pass can determine — see Open Questions.

**Recommendation:**
- Either (a) add `&& ObjectsPerTile <= SampleImageGenerator.MaxObjectsPerTile` to `CanvasUserSettings.IsValid` — cheapest, and directly closes the gap `ICW-015`'s notes assumed was already closed — or (b) clamp `_objectsPerTile` to `[0, MaxObjectsPerTile]` right where it's assigned from settings (`MainWindow.xaml.cs:148`), matching the safer pattern already used for `circleCount`.
- Whichever fix lands, it's a one-line change to `CanvasUserSettings.cs` (option a) with no new files or types needed, and it would let `IsValid` earn the "single source of truth, mirrored everywhere" description `ICW-015` already claims for it.

---

## 2. [Documentation accuracy] `ICW-015`'s "single policy constant... mirrored" claim is incomplete

**Confidence: 90%**

`ICW-015` (status: `Done`) closure notes: *"`SampleImageGenerator.MaxObjectsPerTile` is the single policy constant used by the generator and mirrored by the MainWindow input validator."* Verified: the *input validator* (textbox path) does mirror it correctly. The claim doesn't account for `CanvasUserSettings.IsValid` as a third checkpoint on the same value — and that third checkpoint is the one this pass found to actually disagree with the constant. Not asking to reopen `ICW-015` itself (the textbox-validator behavior it describes is correct and unaffected) — just noting the closure notes overstate coverage, which is presumably how this gap survived a "Done" ticket that was specifically about validating this exact parameter. Worth a one-line addendum to `ICW-015`'s notes once §1 is fixed, so the next reader doesn't inherit the same incomplete picture.

---

## 3. [Refinement, not new] Exact map of the `NoiseOctaves` default split

**Confidence: 90%**

| Site | Default |
|---|---|
| `CanvasUserSettings.BackgroundNoiseOctaves` | `5` |
| `MainViewModel`'s `TileBackgroundNoiseSettingsViewModel` (`Octaves`) | `5` |
| `GeneratorOptions.NoiseOctaves` | `3` |
| `SampleImageGenerator.GenerateSet`'s `noiseOctaves` parameter | `3` |

Consistent within each camp, inconsistent across them. Pass 6 flagged this loosely as "5 vs 3 drift"; recording the precise four-site map here so whoever picks up pass 6's §1 fix (the `InitializeSpatialState`/`MainViewModel` reset bug) can decide in one pass which default is actually intended and update all four sites together, rather than fixing the reset bug and leaving the underlying default mismatch for someone else to rediscover.

---

## Suggested Priority

1. **§1** — one-line fix, closes a real (if not yet reproduced end-to-end) broken-first-run scenario, and directly fulfills a claim an existing "Done" ticket already made.
2. **§2** — trivial addendum, do alongside §1.
3. **§3** — bundle with pass 6's §1 fix (`InitializeSpatialState`/`MainViewModel` reset), since fixing that bug is what would make the "correct" default value actually matter in practice.

## Assumptions & Open Questions

- Did not determine whether a caught `INITIALIZATION FAILED` startup exception leaves the UI in a state where the user can self-recover by editing the `ObjectsPerTile` textbox and clicking Generate, or whether recovery requires manually editing/deleting the settings JSON file outside the app. This would need an actual run to confirm; flagging as the one piece of §1 that's inferred from control flow rather than directly observed.
- As with all prior passes, static source review only; no build or test execution was performed.
