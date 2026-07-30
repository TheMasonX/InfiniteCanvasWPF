# InfiniteCanvasWPF — Audit Pass 12 (Same HEAD, Deep Re-Read of Large Files)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since pass 6; verified before writing).
**Scope this pass:** exhaustive re-read of the sections of `MainWindow.xaml.cs` (1676 lines) and `SampleImageGenerator.cs` (720 lines) that earlier passes had only partially covered in chunks — specifically lines 550–734, 865–1235, 1331–1500 of `MainWindow.xaml.cs`, and 230–400 of `SampleImageGenerator.cs`.

One finding that directly confirms and strengthens pass 9's open question with concrete code (§1), one genuinely new architectural concern surfaced only by reading code that predates this whole audit series but had never been read start-to-finish before (§2), and two minor items (§3–4).

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **Confirms and closes pass 9's open question, with direct evidence.** `SaveSettings()`'s fallback path (`MainWindow.xaml.cs:1458-1466`) — used when the constructed settings fail `IsValid` — restores `ObjectsPerTile` from the in-memory `_objectsPerTile` field, the *same* field pass 9 showed gets no upper-bound check on load. Since `IsValid` never checks `ObjectsPerTile`'s upper bound in the first place (pass 9's core finding), this fallback branch doesn't even need to trigger for the bad value to round-trip — confirms a bad `ObjectsPerTile` loaded once will keep saving itself back out indefinitely, exactly as pass 9 predicted. Also resolves the other half of that open question: `ApplyGenerationControlsToUi()` populates `ObjectsPerTileTextBox` from `_objectsPerTile` unconditionally, *before* `RegenerateSceneAsync` runs — so the textbox is visible and holds the bad value even after "INITIALIZATION FAILED," meaning the user very likely *can* self-recover by typing a corrected value and clicking Generate (which does go through the fully-bounded `TryReadGenerationOptions` path). Net effect: confirmed real, but confirmed recoverable — upgrades pass 9's confidence, doesn't change its severity. | Medium (confirms pass 9) | 90% |
| 2 | **New: tile generation now performs concurrent GDI+ work on up to 4 background threads simultaneously, a concurrency exposure that predates and wasn't specifically exercised by the `TileWorkCoordinator` stability sprint.** `SampleImageGenerator.ApplyMipDetails` → `ApplyDetailsWithGdiPlus` (`#if WINDOWS` path) creates a `Bitmap`, `Graphics`, and `SolidBrush` and calls `FillEllipse`/`LockBits` whenever `circleCount > 0` — which is the default (`GeneratorOptions.CircleCount = 3`). This runs inside the factory closures `SampleImageTile.cs` hands to `TileWorkCoordinator.Request`, which the coordinator executes via `Task.Run` with up to `DefaultMaxConcurrency = 4` running at once. Each call uses fully independent local GDI+ objects (no shared static state at the C# level), which is generally the safe way to use GDI+ across threads — but GDI+ has a documented history of subtle instability under sustained heavy concurrent use, and this exposure is architecturally new: before the coordinator existed, nothing in this codebase ran GDI+ operations from multiple threads at once during generation. The five-hotfix stability sprint (passes 6-ish) was entirely about queue/claimant/dispose correctness — nothing in the sprint's own test suite or handoff doc targets this specific "N concurrent GDI+ Bitmap/Graphics instances" scenario. | Medium | 65% |
| 3 | `AnnotationToolTip` creation (`CreateAnnotationToolTip`, `MainWindow.xaml.cs:724-732`) does direct dictionary-indexer access (`annotation.Features["Confidence"]`, `["Severity"]`) rather than `TryGetValue`. Currently safe only because `AnnotationGenerator` (confirmed in an earlier pass) always populates exactly these two keys for every annotation it produces — an implicit contract with no compile-time or defensive runtime enforcement. This call runs synchronously inside frame construction (which must run on the UI/dispatcher thread, since it builds live WPF `ToolTip`/`Border` objects), triggered by nothing more than a mouse hover — and per pass 11, a `KeyNotFoundException` here would be silently absorbed by the global dispatcher-exception handler with zero user-visible signal. Not a new root cause (the string-keyed `Features` dictionary is already a known, tracked pattern), but this is a concrete, previously-unexamined call site that would actually crash-and-silently-recover if the pattern were ever violated. | Low | 80% |
| 4 | `ApplyScaleWithUniformFirst`'s `fallbackScaleY` parameter (`MainWindow.xaml.cs:1203`) is always redundant: the method immediately recomputes `minimumScaleY` internally via `ComputeMinimumZoom`, and both call sites (`ApplyFitToWidthZoom`, `ApplyFitToHeightZoom`) always pass exactly that same recomputed value as the argument. No correctness impact — `Math.Max(minimumScaleY, fallbackScaleY)` always simplifies to `minimumScaleY` — but it's a needless parameter that duplicates a computation the callee already performs, worth dropping the next time this method is touched. | Trivial | 90% |

---

## 1. [MEDIUM, confirms pass 9] `SaveSettings()`'s fallback perpetuates a bad `ObjectsPerTile` — and the recovery path is now confirmed

**Confidence: 90%**

```csharp
// MainWindow.xaml.cs:1431-1466 (SaveSettings)
var settings = new CanvasUserSettings
{
    ...
    ObjectsPerTile = int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile) && objectsPerTile >= 0
        ? objectsPerTile
        : _objectsPerTile,
    ...
};

if (!settings.IsValid)
{
    settings = settings with
    {
        TileColumns = _tileColumns,
        TileRows = _tileRows,
        ObjectsPerTile = _objectsPerTile   // <-- falls back to the same unvalidated in-memory field
    };
}
```
Two things confirmed here that pass 9 could only infer:
- The fallback path explicitly reuses `_objectsPerTile` — the exact field pass 9 showed is assigned straight from `settings.ObjectsPerTile` on load with no upper-bound check. If that field is already bad, this is the mechanism that writes it straight back to disk.
- More importantly: `IsValid` doesn't check `ObjectsPerTile`'s upper bound at all (pass 9's core finding), so a bad value doesn't even need this fallback branch to survive — the *primary* construction above it already reflects whatever's in the textbox or the field, and `IsValid` won't object either way.

And the other half of pass 9's open question — whether the user can self-recover — is now answered:
```csharp
// MainWindow.xaml.cs:136-141 (ApplyGenerationControlsToUi) — runs unconditionally in OnLoaded,
// before RegenerateSceneAsync, regardless of whether generation later succeeds or throws
private void ApplyGenerationControlsToUi()
{
    TilesXTextBox.Text = _tileColumns.ToString();
    TilesYTextBox.Text = _tileRows.ToString();
    ObjectsPerTileTextBox.Text = _objectsPerTile.ToString();
    GenerationSeedTextBox.Text = _generationSeed.ToString();
}
```
The textbox is populated with the bad value *before* the failing `RegenerateSceneAsync` call, so it's visible and (nothing found that would disable it) editable even after `OnLoaded`'s catch shows `INITIALIZATION FAILED`. A user who notices the bad value, corrects it, and clicks Generate would go through `TryReadGenerationOptions` — which *does* enforce the upper bound correctly — and successfully recover. This doesn't reduce the priority of pass 9's fix (the settings-file validation gap is still real and still worth the one-line fix), but it does mean the failure mode is "confusing but recoverable," not "the app is bricked."

**Recommendation:** unchanged from pass 9 — add the upper-bound check to `CanvasUserSettings.IsValid`. This pass just supplies the missing evidence for both halves of that finding's open question.

---

## 2. [MEDIUM] Concurrent GDI+ usage in tile generation, newly exposed by the coordinator's parallelism

**Confidence: 65%** — the mechanism and the concurrency exposure are both confirmed by reading; the actual risk (whether GDI+ reliably tolerates this pattern under this app's load) is a runtime/empirical question this static pass can't settle, hence the moderate rather than high confidence.

```csharp
// SampleImageGenerator.cs:291-293 (GenerateMonochromeMipPixels — runs inside every tile's generation)
if (circleCount > 0 || !string.IsNullOrWhiteSpace(tileLabel))
{
    ApplyMipDetails(pixels, width, height, nativeWidth, nativeHeight, targetValue, circleCount, seed, tileLabel);
}
```
`circleCount > 0` is the default (`GeneratorOptions.CircleCount = 3`), so this branch runs for essentially every tile, every mip level, in normal use. On Windows builds, `ApplyMipDetails` calls `ApplyDetailsWithGdiPlus`:
```csharp
#if WINDOWS
    ApplyDetailsWithGdiPlus(pixels, width, height, circles, tileLabel);
```
which creates a `Bitmap`, a `Graphics`, and per-circle `SolidBrush` instances, calls `FillEllipse`, then `LockBits`/`UnlockBits` to copy the rendered alpha-masked pixels back into the managed `byte[]` buffer. (Confirmed `tileLabel` is always `null` from the production call path — `SampleImageTile.cs` never passes it — so the `Font`/`DrawString` branch inside this method is currently dead in practice; the live surface is `Bitmap` + `Graphics` + `FillEllipse` + `LockBits` only.)

This factory is exactly what `SampleImageTile.cs` hands to `TileWorkCoordinator.Request` (confirmed in passes 5–6), and the coordinator runs up to `DefaultMaxConcurrency = 4` such factories concurrently via separate `Task.Run` calls. That means up to 4 threads can be inside `ApplyDetailsWithGdiPlus` — each constructing and tearing down its own `Bitmap`/`Graphics`/`Brush` — at the same moment.

Each call's GDI+ objects are fully local (no shared statics, no cross-call reuse), which is the generally-recommended safe pattern for multi-threaded GDI+ — this is *not* the same class of bug as passes 5–6's shared-bitmap-pool disposal races, where the same object was genuinely touched from two threads. The reason this is worth flagging rather than dismissing: GDI+ (the native Windows API `System.Drawing` wraps) has a longstanding, documented history of internal instability under sustained heavy concurrent load even when caller code follows the "separate instances per thread" rule, particularly around font/brush allocation and simultaneous `Bitmap` construction — and this is architecturally *new* exposure: before `TileWorkCoordinator` existed, nothing in this codebase ran GDI+ generation work from more than one thread at a time. The sprint that hardened the coordinator (five hotfixes, 19 new unit tests) was entirely about queue/claimant/dispose correctness at the coordinator level — nothing in its test suite or its own handoff doc targets "many tiles generating with `circleCount > 0` concurrently" as a scenario, because that scenario predates the coordinator and was never previously reachable at this concurrency level.

**Recommendation:** this is worth a deliberate, explicit check rather than either dismissing it or treating it as confirmed-broken: run (or add, as a load/stress test alongside `ICW-144`'s already-planned fast-scroll queue stress work) a scenario that forces several tiles with `circleCount > 0` through the coordinator at full `DefaultMaxConcurrency` simultaneously, ideally under the same kind of sustained fast-scroll load that motivated the original stability sprint, and watch specifically for GDI+-originated exceptions (`System.Runtime.InteropServices.ExternalException` is GDI+'s usual failure signature, distinct from the managed exceptions the sprint was chasing). If it's been running fine under real usage already, this can be downgraded to a documented "verified safe under load" note; if not, the fix is likely either capping GDI+-touching work to a smaller concurrency pool than the coordinator's general `DefaultMaxConcurrency`, or serializing just the `ApplyDetailsWithGdiPlus` call behind a dedicated lock (cheap, since circle-stamping is a small fraction of total generation time per tile).

---

## 3. [LOW] Direct dictionary-indexer access in tooltip creation — a concrete instance of an already-tracked pattern

**Confidence: 80%**

```csharp
// MainWindow.xaml.cs:724-732
private static ToolTip CreateAnnotationToolTip(SampleAnnotation annotation)
{
    var confidence = annotation.Features["Confidence"];
    var severity = annotation.Features["Severity"];
    ...
}
```
Currently safe because `AnnotationGenerator` always populates both keys — verified in an earlier pass. Flagging this specific call site because it's a concrete example of where the already-known string-keyed-`Features`-dictionary pattern would actually surface as a crash: a mouse hover, not a click, on the UI thread, inside frame construction — and per pass 11, that failure would be swallowed silently by the global dispatcher handler with no visible symptom. Not asking for a new ticket about the `Features` dictionary design itself (already tracked); just noting this exact line as one of the places a `TryGetValue`-based defensive read would pay for itself if that design is ever revisited.

---

## 4. [TRIVIAL] Redundant parameter in `ApplyScaleWithUniformFirst`

**Confidence: 90%** — see Executive Summary; no further detail needed.

---

## Suggested Priority

1. **§1** — no new work needed beyond pass 9's existing recommendation; this pass just removes the remaining uncertainty about it.
2. **§2** — worth a stress test before the next round of coordinator-concurrency tuning (`DefaultMaxConcurrency`/`DefaultMaxBytes`, per the sprint handoff's own "next steps"), since tuning those numbers without knowing whether GDI+ tolerates the resulting concurrency level would be tuning against an unverified assumption.
3. **§3–4** — no urgency; bundle with any other low-priority cleanup pass.

## Assumptions & Open Questions

- §2's confidence (65%) reflects genuine uncertainty about whether this pattern is actually unsafe in practice on modern .NET/Windows GDI+, not uncertainty about whether the mechanism exists (that part is confirmed by reading). This is the kind of claim that's cheap to settle empirically (a stress test) and expensive to settle by further static reading — flagging it for that reason rather than continuing to reason about GDI+ internals from documentation alone.
- §1 no longer has an open question — both halves pass 9 left unresolved (does the bad value persist? can the user recover?) are now answered directly from code.
- As with all prior passes, static source review only; no build or test execution was performed.
