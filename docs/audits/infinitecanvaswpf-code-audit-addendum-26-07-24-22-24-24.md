# InfiniteCanvasWPF — Audit Addendum (Delta Review)

**Supersedes/extends:** `infinitecanvaswpf-code-audit-26-07-24-13-10-55.md` (audited commit `84ddba2`)
**New commit audited:** `43bfd55bbae7e14a590784f7831e5261eecfd69b` — *"feat: add zoom presets, cache debug controls, and class-colored defects"* (ICW-013)
**Method:** Downloaded `main` HEAD tarball via the public repository archive, diffed byte-for-byte against the previously audited tree to isolate the exact changed lines (`diff -rq`, then per-file `diff -u`), then reviewed every changed hunk in full file context. 9 files touched: `MainWindow.xaml`, `MainWindow.xaml.cs`, `SampleImageGenerator.cs`, `SampleImageTile.cs`, `ZeroCopyBitmapFactory.Windows.cs`, `SampleImageGeneratorTests.cs`, `active-tasks.md`, `task-tracker.md`, plus new file `ICW-013-zoom-presets-and-cache-debug.md`. All other files in the tree are byte-identical to the previously audited commit and are not re-litigated here.

---

## 1. Executive Summary

ICW-013 is a solid feature slice: zoom presets, a busy indicator, class-colored defects, and — genuinely well-implemented — a hand-rolled thread-safe resettable pixel cache that correctly replaces `Lazy<T>` to support the new debug cache-reset control (verified: correct double-checked-locking with `Volatile.Read`/lock, matches the canonical .NET pattern). The ticket also explicitly claims to fix the exact issue this audit flagged as **F-07** (pixelometer bypassing the spatial index) — confirmed true; `TryReadPixelValue` now calls `_spatialIndex.Query(...)` instead of scanning all annotations.

However, that same commit **introduces a live regression that is the direct, predicted consequence of a finding from the previous audit (F-06)**: the rendering-side blend formula was upgraded to support class-color tinting, but the *duplicate* copy of that formula living in `MainWindow.xaml.cs` (called out by name in F-06 as a divergence risk) was not updated. The pixelometer's on-screen reading and the actual rendered pixel color are now computed by two different formulas. This is the single most important finding in this addendum — see **A-01**.

Of the 8 high-severity findings in the original report, **1 is now resolved (F-07)**, **1 is escalated (F-03 — unbounded input now touches ~44× larger worst-case allocations)**, and the rest are unchanged (no code in those areas was touched). No findings from the original report were invalidated.

### New findings this delta

| ID | Severity | Confidence | One-line |
|---|---|---|---|
| A-01 | **High** | 90% | `BlendDefect` duplicate not updated → pixelometer reading now diverges from rendered pixel color |
| A-02 | Medium | 85% | Classification metadata (name/color/aspect-ratio) is scattered across 3 independently-maintained collections with no compile-time sync guarantee; a 5th class throws `KeyNotFoundException` |
| A-03 | Medium | 80% | Zoom preset dropdown is wired to the handler purely by numeric `SelectedIndex`, with no shared source of truth between XAML item order and the C# `switch` |
| A-04 | Low | 90% | New `ResetImageCache`/cache-invalidation mechanism has zero test coverage |
| A-05 | Low | 55% | `ClassificationColors` BGRA-order literals are unverified against intended visual hues (flagging the ergonomic risk, not asserting a concrete color is wrong) |
| A-06 | Low | 60% | `ApplyScaleWithUniformFirst` recomputes `ComputeMinimumZoom` redundantly; two of its four parameters are partially redundant with that recomputation |
| A-07 | Low | 70% | `BeginBusyOperation`'s `Dispatcher.Invoke` can throw during shutdown, leaking the busy counter at 1 (harmless in practice, but an unhandled-exception surface) |

### Status of original findings after this delta

| ID | Status | Note |
|---|---|---|
| F-07 (pixelometer O(n) scan) | ✅ **Resolved** | Now uses `_spatialIndex.Query(new SpatialBounds(worldX, worldY, 0.01, 0.01))`. See §3. |
| F-03 (unbounded `objectsPerTile`) | ⚠️ **Escalated** | Still no upper bound; per-annotation worst-case defect-raster size grew ~44× in this commit. See §3. |
| F-06 (duplicated `BlendDefect`) | 🔴 **Materialized** | The predicted divergence happened — see A-01. |
| F-15 (`double.Epsilon` misuse) | ➖ Unchanged | Persists verbatim in renamed `TryComputeUniformZoomDelta`. |
| F-01, F-02, F-04, F-05, F-08–F-14, F-16–F-26 | ➖ Unchanged | No code in scope of this commit touches these areas. |

---

## 2. New Finding A-01 (Detail) — Pixelometer/render blend divergence

**Files:** `ZeroCopyBitmapFactory.Windows.cs:222-234` (new) vs. `MainWindow.xaml.cs:957-958` (untouched)
**Confidence: 90%**

The renderer's blend function was correctly upgraded to tint by class color and weight by defect intensity:

```csharp
// ZeroCopyBitmapFactory.Windows.cs — NEW, used for actual pixel output
private static byte BlendChannel(byte baseValue, byte overlayValue, double blendWeight)
{
    var weighted = (0.48 * baseValue) + (0.52 * overlayValue);
    var blended = baseValue + ((weighted - baseValue) * blendWeight);
    return (byte)Math.Clamp((int)Math.Round(blended), byte.MinValue, byte.MaxValue);
}
```

But `MainWindow.xaml.cs`'s copy — used for the pixelometer readout at `TryReadPixelValue:921` — is untouched:

```csharp
// MainWindow.xaml.cs — OLD formula, still used for the on-screen pixelometer value
private static byte BlendDefect(byte baseValue, byte defectValue)
    => (byte)Math.Clamp(baseValue - (defectValue / 2), byte.MinValue, byte.MaxValue);
```

**Concrete user-visible symptom:** hover over a defect and the pixelometer readout (`PixelometerValueText`) will report a grayscale-darkened value, while the actual rendered pixel under the cursor is now a class-colored blend (e.g. tinted toward blue/green/orange depending on classification per the new `ClassificationColors`). The two will not agree, and the mismatch is systematic (every defect pixel, not an edge case).

This is called out separately from the original F-06 because it's no longer a *risk* — it's a *shipped defect in the currently audited commit*, directly attributable to the duplication the previous audit flagged.

**Fix:** Exactly the fix recommended in the original F-06 — extract one shared blend/sample helper (suggest `InfiniteCanvas.Rendering.RasterSampling` or similar) that both `ZeroCopyBitmapFactory` and `MainWindow` call, and delete both current copies. Given this has now caused an actual regression, recommend raising this from "opportunity" to "do next sprint."

---

## 3. Verified Changes from the Ticket's Own Claims

Cross-checked each bullet in `ICW-013`'s "Findings" section against the diff, since the audit brief asks not to take claims at face value:

| Ticket claim | Verified? | Evidence |
|---|---|---|
| "Optimized pixelometer defect sampling by querying the spatial index" | ✅ True | `MainWindow.xaml.cs:936-938`: `_spatialIndex.Query(new SpatialBounds(worldX, worldY, 0.01, 0.01))` replaces the old full-`_annotations` loop. Confirms F-07 resolved. |
| "Wheel zoom is now uniform-only" | ✅ True | `OnViewportMouseWheel` no longer reads `Keyboard.Modifiers` for Shift/Ctrl axis-lock; `TryComputeUniformZoomDelta` takes one `requestedScaleDelta`, not two. |
| "Zoom floor behavior is uniform-first" | ✅ True, and correctly implemented | Traced `EnforceZoomFloor`'s new near-equal-scale fast path and `ApplyScaleWithUniformFirst`'s fallback math by hand for the "fit-to-height on a wide viewport" case; the non-uniform fallback correctly reduces to forcing only the axis that actually needs it. No bug found here despite the code being non-obvious on first read. |
| "Defects now render larger with broader size variability" | ✅ True | Object width range widened 70–200 → 160–560; defect raster multiplier changed from fixed 2× to a randomized 2.4×–4.5×. Also **not mentioned in the ticket**: this raises the worst-case per-annotation `ResampleTemplate` cost from ~160K to ~7M pixels — see A-escalation of F-03 above. |
| "Sparse defect raster blending now tints defect imagery by annotation class color" | ✅ True, but see A-01 | Rendering path confirmed; pixelometer path was missed. |
| "Added debug button to dump fetched cache summary and reset tile image cache" | ✅ True, well-implemented | `SampleImageTile` correctly replaced `Lazy<byte[]>` with a hand-rolled double-checked-locking cache (`Volatile.Read` + `lock` + re-check) specifically to support `ResetImageCache()`. This is textbook-correct — matches the canonical .NET DCL pattern (a plain write inside a `lock`, guarded by a prior `Volatile.Read` outside it, is safe because `Monitor.Exit` is a release fence and `Volatile.Read` is an acquire fence). No concurrency bug found. Only gap: no test exercises it (A-04). |
| Not claimed, but observed: labels moved to "above object top-left" | ✅ True | `BuildAnnotationLabel` places a bordered label panel at `topLeft.Y - 22`; reasonable, not tested but low-risk (pure layout). |

---

## 4. New Findings — Full Detail

### A-02 — Classification metadata duplicated across three unsynchronized collections
**File:** `SampleImageGenerator.cs:11-18` (`Classifications`, `ClassificationColors`), `164-175` (`GetClassAspectRange`)
**Confidence: 85%**

```csharp
private static readonly string[] Classifications = ["Scratch", "Inclusion", "Stain", "Edge defect"];
private static readonly IReadOnlyDictionary<string, Bgra32Color> ClassificationColors = new Dictionary<string, Bgra32Color> { ["Scratch"] = ..., ["Inclusion"] = ..., ["Stain"] = ..., ["Edge defect"] = ... };
...
private static (double Min, double Max) GetClassAspectRange(string classification) => classification switch { "Scratch" => ..., "Inclusion" => ..., "Stain" => ..., "Edge defect" => ..., _ => (0.8, 2.0) };
```

Three independent collections, each hand-keyed by the same four string literals, with zero compiler-enforced correspondence. `GetClassAspectRange` has a safe default (`_ => (0.8, 2.0)`), but the color lookup does not:

```csharp
var color = ClassificationColors[classification];   // GenerateAnnotations, line ~144 — plain indexer
```

Add a 5th entry to `Classifications` (a one-line change that looks completely safe) without also adding it to `ClassificationColors`, and every subsequent annotation generation throws `KeyNotFoundException` inside a `Task.Run` on the regenerate path — an unhandled exception feeding directly into **F-01** (no top-level exception handling), i.e., this specific mistake would crash the app via the exact mechanism F-01 already warned about.

**Fix:** Replace all three collections with one: `private static readonly (string Name, Bgra32Color Color, double AspectMin, double AspectMax)[] ClassificationProfiles = [...]`, indexed by `random.Next(ClassificationProfiles.Length)`. Removes the possibility of the collections drifting out of sync entirely.

---

### A-03 — Zoom preset `SelectedIndex` is a magic-number contract between XAML and code-behind
**Files:** `MainWindow.xaml:97-105` (8 `ComboBoxItem`s, positions 0–7), `MainWindow.xaml.cs:614, 631-651`
**Confidence: 80%**

```csharp
var customSelected = ZoomPresetComboBox.SelectedIndex == 7;   // must match the 8th <ComboBoxItem>
...
var mode = ZoomPresetComboBox.SelectedIndex;
switch (mode) { case 0: ApplyFitToWidthZoom(); break; case 1: ApplyFitToHeightZoom(); break; case 2: ... case 6: var percent = mode switch { 2 => 50, 3 => 75, 4 => 100, 5 => 150, _ => 200 }; ... default: return; }
```

Reordering, inserting, or removing a `ComboBoxItem` in the XAML silently breaks this mapping with no compile error — e.g. inserting a new preset between "Fit To Height" and "50%" would silently relabel every percent option one step off, and the app would keep running, just applying the wrong zoom level. This is the same class of issue as F-14 (implicit contracts enforced only by convention), applied to UI/code-behind coupling this time.

**Fix:** Give each `ComboBoxItem` a `Tag` (e.g. `Tag="FitWidth"`, `Tag="Percent:50"`) and switch on `((ComboBoxItem)ZoomPresetComboBox.SelectedItem).Tag`, or bind to an `enum ZoomPreset` via a `ComboBox.ItemsSource`.

---

### A-04 — No test coverage for the new cache-reset mechanism
**Confidence: 90%**

`SampleImageGeneratorTests.cs`'s only change in this commit is loosening two assertions to accommodate the new randomized defect-size ratio (`Is.EqualTo` → `Is.GreaterThan`). No test was added for:
- `SampleImageTile.ResetImageCache()` — does `Pixels` correctly re-invoke the factory after reset? Does `IsBackgroundFetched` correctly flip back to `false`?
- The new `_spatialIndex.Query`-based pixelometer path (no test exercises `TryReadPixelValue` at all, before or after this change — this was already a gap, but the logic it now depends on, `LiveSpatialIndexService.Query`, has grown a new caller with different correctness requirements, e.g. is the `0.01, 0.01` sample-box size actually always in the same coordinate space as annotation bounds? It is — both are "world" units — but this is exactly the kind of assumption a regression test would pin down).

**Fix:** Add `SampleImageTileTests.cs` covering the reset/re-fetch cycle (cheap, no WPF dependency needed for the non-Windows byte[] constructor overload).

---

### A-05 — `ClassificationColors` literal values not visually verified
**File:** `SampleImageGenerator.cs:12-18`
**Confidence: 55% that it's worth double-checking; not asserting a bug**

`Bgra32Color`'s constructor order is `(Blue, Green, Red, Alpha)` — confirmed by its `readonly record struct` declaration in `Bgra32Color.cs:3`. The previous commit's only other call site (`new Bgra32Color((byte)random.Next(12,56), ..., (byte)random.Next(210,256), 255)`) correctly follows this order to produce red (low B, low G, high R), consistent with the red annotation color used elsewhere at the time — good evidence the team does track this ordering correctly in general. The four new literals (e.g. `["Scratch"] = new(60, 90, 245, 255)` → B=60,G=90,R=245 → red) look plausible and internally consistent (each classification gets a visibly distinct hue), but this was verified only by hand-decoding the positional arguments, not by rendering — worth a 30-second visual sanity check in the running app, since a swapped pair of arguments would silently produce a valid-but-wrong color with no error anywhere.

**Fix (optional, low priority):** Use named arguments at the call sites (`new(Blue: 60, Green: 90, Red: 245, Alpha: 255)`) to make future edits self-checking, given the type's field order doesn't match the common RGB-first convention.

---

### A-06 — Redundant recomputation in `ApplyScaleWithUniformFirst`
**File:** `MainWindow.xaml.cs:668-682`
**Confidence: 60%, cosmetic**

```csharp
private void ApplyFitToWidthZoom()
{
    var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(width, height);
    ApplyScaleWithUniformFirst(minimumScaleX, minimumScaleY, width, height);
}

private void ApplyScaleWithUniformFirst(double preferredUniformScale, double fallbackScaleY, double viewportWidth, double viewportHeight)
{
    var (minimumScaleX, minimumScaleY) = ComputeMinimumZoom(viewportWidth, viewportHeight);   // recomputed, same inputs
    ...
}
```

`ComputeMinimumZoom` is called once by the caller and again, redundantly, inside the callee with the same `viewportWidth`/`viewportHeight`. It's a pure, cheap division, so there's no measurable perf impact — but the parameter list (`preferredUniformScale`, `fallbackScaleY` passed in, then `minimumScaleX`/`minimumScaleY` recomputed anyway) makes the function harder to reason about than necessary. (Verified this doesn't cause a logic bug — see §3's confirmation of the fit-to-height fallback math.)

**Fix:** Either pass `(minimumScaleX, minimumScaleY)` through instead of recomputing, or drop the parameters and have the two `ApplyFitTo*` callers just call `ComputeMinimumZoom` and inline the two-line uniform-first decision themselves.

---

### A-07 — `Dispatcher.Invoke` inside `BeginBusyOperation` can leak the busy counter during shutdown
**File:** `MainWindow.xaml.cs:962-977`
**Confidence: 70%**

```csharp
private void BeginBusyOperation()
{
    if (Interlocked.Increment(ref _busyOperationCount) == 1)
    {
        Dispatcher.Invoke(() => RenderBusyBar.Visibility = Visibility.Visible);   // can throw if dispatcher is shutting down
    }
}
```

The `Interlocked.Increment` always succeeds before the `Dispatcher.Invoke` call. If `Dispatcher.Invoke` throws (e.g. `TaskCanceledException`/`InvalidOperationException` if called while the dispatcher is shutting down, which per **F-08** is a real possibility if a render is requested during window close), the exception propagates out of `BeginBusyOperation` *before* the caller's own `try/finally` begins (in both call sites, `BeginBusyOperation()` is called immediately before the `try` block, not inside it) — so `EndBusyOperation()` is never reached, leaving `_busyOperationCount` incremented forever. Impact is cosmetic (a progress bar stuck visible) and only matters if the window somehow stays alive after this, so severity is low, but it compounds with F-08's existing close-during-operation race.

**Fix:** Wrap the `Dispatcher.Invoke` call in a try/catch (log and ignore), since a failed UI update during shutdown is never worth crashing or leaking state over.

---

## 5. Assumptions Carried Forward / New

1. All assumptions from the original report (§5) still apply.
2. This delta assumes the `main` branch HEAD at fetch time (`43bfd55b...`) is the "new commit" the user meant; the atom feed also shows this is the tip as of the fetch, with no newer commits behind it.
3. A-05's "colors look plausible" judgment is based on decoding BGRA byte values by hand, not on rendering the app — flagged explicitly as unverified rather than asserted.

