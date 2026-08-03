# InfiniteCanvasWPF — Delta Report: XAML/UI-Layer Findings & Pixelometer Ticket Corrections

**Previous reports:** three prior audits, all now in `docs/audits/` (implementation audit, follow-up audit, delta-findings report).
**This report's commit:** `main` tip — full recursive diff against the exact tree read in the previous session confirms **zero changes since the last report**; nothing to verify this round, so this report is 100% new findings.
**Scope:** this session focused on the one major area not yet examined in depth — `MainWindow.xaml` (markup, never previously read) and the remainder of `MainWindow.xaml.cs` (input/scrollbar handlers, pixelometer readout) — plus targeted follow-up reads of tickets that turned out to be directly relevant. As before, this contains only new findings and corrections; nothing already reported is repeated.

---

## 1. New Findings

### 1.1 The pixelometer readout computes defect values two different ways and can display self-contradictory numbers — corrects `ICW-035`'s completion claim

**This is the most significant finding in this report.** `MainWindow.UpdatePixelometer` (the mouse-hover HUD readout — this is the tool's own diagnostic instrument, and its accuracy is presumably a core value proposition per `AboutDialog`'s "inspection... playground" description) builds its displayed string from **two independently-computed defect values that use two different aggregation rules**:

1. `TryReadPixelValue` (private helper in `MainWindow.xaml.cs`) queries `_spatialIndex` and computes its own `defect` output parameter via a manual loop that takes the **maximum** defect value across every overlapping annotation:
   ```csharp
   for (var index = 0; index < hitAnnotations.Count; index++)
   {
       if (hitAnnotations[index].TryGetDefectValue(worldX, worldY, out var value))
       {
           defect = Math.Max(defect, value);
       }
   }
   ```
2. `ResolveDisplayPixelValue` (a second, separate private helper) is called immediately afterward with the same `worldX`/`worldY`, runs its **own independent** `_spatialIndex.Query(...)`, and delegates to `DefectOverlaySampler.ResolveDisplayValue(byte, IEnumerable<SampleAnnotation>, ...)`, which is a **last-wins fold** (each matching annotation unconditionally overwrites the running value; whichever annotation the spatial backend happens to enumerate last determines the result — order-dependent, not severity-dependent).

Both values land in the same displayed string: `$"PIXEL {finalValue}  ({tileId}) bg {backgroundValue} + defect {defectValue}"` — `finalValue` from path 2 (last-wins), `defectValue` from path 1 (max). **When two annotations overlap the same world point with different defect values, these two numbers do not have to be consistent with each other** — the headline `PIXEL` reading and the `+ defect` breakdown shown right next to it, in the same string, can visibly not add up, because they were computed by two different algorithms.

**This directly corrects an existing ticket's stated completion status.** `ICW-035-renderer-pixelometer-blend-contract.md` ("Unify renderer and pixelometer defect blending and sampling contract," status **In Progress**) has a Notes section stating: *"The initial implementation now routes both paths through a shared sampler in the rendering layer."* That claim is **only true for path 2** (`ResolveDisplayPixelValue`, which does call `DefectOverlaySampler`). It is **not true for path 1** — `TryReadPixelValue`'s own max-based loop is a third, independent implementation that was evidently missed when the "shared sampler" work landed, even though `TryReadPixelValue` lives in exactly the file (`MainWindow.xaml.cs`) `ICW-035`'s own scope names. The ticket's validation command (`SampleImageTileTests`, 9/9 passing) could never have caught this, since it only tests `DefectOverlaySampler` directly — it has no coverage of `MainWindow.TryReadPixelValue`, which is untested app-layer code.

**Relationship to `ICW-100-overlay-precedence-and-pixelometer`** (a *different* ticket also numbered 100 — yet another instance of the project's known duplicate-ID problem, `key: ICW-100` clashing with the `RenderRequestTracker` ticket from my earlier reports): that ticket already correctly identifies that `DefectOverlaySampler`'s last-wins behavior is order-dependent and proposes defining an explicit precedence rule (z-index / max-severity / first-hit). **This finding doesn't duplicate that** — it's a layer below: even after `ICW-100` picks a precedence rule and fixes `DefectOverlaySampler`, `TryReadPixelValue`'s separate max-based loop will still exist as a second, un-migrated implementation unless it's explicitly included in that fix's scope. Recommend adding `MainWindow.TryReadPixelValue`'s defect-loop to `ICW-100`'s file list explicitly, since it currently only lists `DefectOverlaySampler.cs`, `MainWindow.xaml.cs` (generically), and `SampleImageTile.cs`.

**Confidence:** 95% (both code paths read in full, both call sites and their exact aggregation logic confirmed, the ticket's contradicting claim read directly from its own file).

### 1.2 The same pixelometer update runs the spatial-index query twice per mouse-move — concrete content for an empty ticket stub

**Finding:** as a direct consequence of §1.1's two-path structure, every single `UpdatePixelometer` call — which fires on every `OnViewportMouseMove` event, i.e. continuously while the mouse moves over the canvas — executes **two separate `_spatialIndex.Query(sampleArea)` calls** for the exact same `SpatialBounds` (both constructed as `new SpatialBounds(worldX, worldY, 0.01, 0.01)`, the `0.01` "point-sample" half-extent duplicated verbatim in both places with no shared constant or helper). One query result feeds the max-based `defect` computation; the other feeds the last-wins `finalValue` computation. This is redundant work on a genuinely hot, high-frequency path (mouse movement, not a discrete user action), and it's a top candidate for exactly what `ICW-055-pixelometer-performance` was created to hold — except that ticket is currently an **empty template** (its Summary reads only "Status: proposed," its Scope only "Review and update the relevant implementation area," with no actual content). This finding gives it real, verified substance.

**Recommendation:** once §1.1's dual-algorithm problem is fixed (by having `TryReadPixelValue` call the same shared sampler `ResolveDisplayPixelValue` uses, or vice versa), the double-query collapses naturally to one — so this is a corollary fix that comes for free with §1.1's correction, not a separate piece of work. Recommend `ICW-055-pixelometer-performance`'s Scope be filled in with exactly this: "Eliminate the duplicate `_spatialIndex.Query` call in `UpdatePixelometer`/`TryReadPixelValue`/`ResolveDisplayPixelValue` by consolidating to a single spatial query per pixelometer update, as a side effect of `ICW-035`'s/`ICW-100`'s defect-resolution unification."

**Confidence:** 90% (both query call sites read directly; "hot path" characterization based on `OnViewportMouseMove` firing frequency, not independently profiled/measured).

### 1.3 `MainWindow` exposes a bindable property with no change notification — the XAML binding to it is permanently dead, overridden by direct code-behind manipulation

**Finding:** `MainWindow.xaml` binds the annotation feature grid via:
```xml
<DataGrid ... ItemsSource="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=SelectedAnnotationFeatures}">
```
`SelectedAnnotationFeatures` is a plain, get-only CLR property on `MainWindow` (`public IReadOnlyList<FeatureDisplayItem> SelectedAnnotationFeatures => _selectedAnnotationFeatures;`). `MainWindow` **does not implement `INotifyPropertyChanged`** (confirmed — no `PropertyChanged` event exists anywhere in the 1741-line file) and never raises one for this property. WPF data bindings rely entirely on `INotifyPropertyChanged` (or a `DependencyProperty`, which this also is not) to know when to re-read a bound source property — without it, **this binding evaluates exactly once**, when the binding is first established (almost certainly showing an empty grid, since `_selectedAnnotationFeatures` starts empty), and then **never updates again through the binding mechanism**.

The actual UI update happens entirely outside the binding: `UpdateSelectedAnnotationFeatures(...)` (the method that changes `_selectedAnnotationFeatures` when the user selects a different annotation) finishes by directly assigning `FeatureDataGrid.ItemsSource = SelectedAnnotationFeatures;` in code-behind — which **overwrites whatever the XAML binding had set**, every time. The net effect: the feature grid works correctly (because code-behind pokes it directly), but the `{Binding ... Path=SelectedAnnotationFeatures}` markup in the XAML is **dead** — it could be deleted from the XAML with zero observable behavior change, since it's unconditionally clobbered before the user could ever see its output.

**Why this matters beyond "harmless dead markup":** it's a concrete architectural inconsistency worth naming. The same window correctly uses real MVVM elsewhere — `DataContext = _mainViewModel` (a proper `ObservableObject`) drives the `VisibleItemCount`/`TotalItemCount`/`TileBackgroundNoiseSettings` bindings at the top of the same XAML file, and those work exactly as WPF binding is meant to. `SelectedAnnotationFeatures` instead reaches around the ViewModel entirely via `RelativeSource AncestorType=Window` to bind directly to code-behind — a pattern that looks like MVVM in the markup but isn't, and silently doesn't function as a binding at all. A future maintainer skimming the XAML would reasonably conclude the grid is reactively bound and could, for example, remove the "redundant-looking" `ItemsSource=` line from code-behind as a cleanup, which would silently break the feature grid (it would freeze at its first, empty state).

**Recommendation:** either (a) move `SelectedAnnotationFeatures` onto `MainViewModel` (or a dedicated view-state ViewModel) as a proper `[ObservableProperty]`, update it from `UpdateSelectedAnnotationFeatures`, and let the existing `DataContext`-based binding actually do the work — deleting the manual `FeatureDataGrid.ItemsSource = ...` assignment entirely; or (b) if keeping it in code-behind is intentional (e.g., to avoid a ViewModel dependency on `FeatureDisplayItem`), delete the dead `{Binding}` from the XAML and set `ItemsSource` only from code-behind, so the markup doesn't claim a capability that doesn't exist. Option (a) is preferable — it's a small, isolated, mechanical fix and removes one of very few remaining direct-to-code-behind bindings in an otherwise-MVVM window.

**Confidence:** 95% (directly confirmed: no `PropertyChanged` event/`INotifyPropertyChanged` anywhere in the file, the get-only property, the unconditional code-behind override, and the contrasting working `DataContext`-based bindings in the same XAML file). **No existing ticket found covering this** (searched for `SelectedAnnotationFeatures`, `FeatureDataGrid` in `docs/tasks/` — no hits). Recommend a small new ticket, e.g. `ICW-FEATUREGRID-BINDING-DEADCODE`, or folding into `ICW-022` (MainWindow decomposition and tests), whose stated goal of decomposing `MainWindow`'s responsibilities would naturally absorb this.

### 1.4 Numeric scene-generation settings use unbounded, unvalidated `TextBox` controls in XAML while comparable settings use bounded `Slider` controls — an inconsistency that compounds the already-known validation duplication

**Finding:** `MainWindow.xaml` uses `<Slider Minimum="..." Maximum="...">` with XAML-declared bounds for `OutlineThicknessSlider` (1–6) and `LabelSizeSlider` (8–20) — the UI control itself prevents out-of-range input. But the scene-generation parameters on the same panel — `TilesXTextBox`, `TilesYTextBox`, `ObjectsPerTileTextBox`, `GenerationSeedTextBox` — are plain `<TextBox Text="...">` controls with **no XAML-level bound, no input mask, no numeric-only restriction** — any text a user types is accepted by the control itself and only checked (inconsistently, per my previous report's finding of 4+ separate duplicate/divergent validation copies) deep in code-behind, well after the fact. This isn't a new bug on its own — it's additional, direct evidence at the UI-markup layer for the `ICW-P1-SETTINGS-VALIDATION` scope-extension already recommended in my prior report: the inconsistency isn't just across backend validation copies, it starts at the control-choice level in the XAML itself. A `Slider` (or at minimum a masked numeric `TextBox`) for `ObjectsPerTileTextBox` bounded to `[0, MaxObjectsPerTile]` would eliminate an entire class of the already-documented validation-duplication risk at its source, rather than trying to keep four backend copies in sync.

**Confidence:** 90% (XAML read directly; this is a design-consistency observation, not a runtime bug in itself — severity is calibrated accordingly, low-priority relative to §1.1–1.3).

### 1.5 Scrollbar mouse-event handlers repeat the same axis-dispatch ternary pattern at least six times across three methods — extends `ICW-077`'s scope

**Finding:** `OnScrollbarTrackMouseLeftButtonDown`, `OnScrollbarThumbMouseLeftButtonDown`, and `OnScrollbarThumbMouseMove` each independently re-derive "given this axis, which track/thumb/length/position do I use" via repeated `axis == ViewportScrollbarAxis.Horizontal ? X : Y` ternaries — six or more such ternaries total across the three methods, each picking between the same pair of fields (`_horizontalScrollbarTrack`/`_verticalScrollbarTrack`, `_horizontalScrollbarThumb`/`_verticalScrollbarThumb`, `.X`/`.Y` of a `Point`, `Canvas.GetLeft`/`Canvas.GetTop`). This is a data-clump/primitive-obsession pattern: "an axis, plus the four things that resolving it means" wants to be a small `ScrollbarAxisContext` record (or a `switch` expression computed once per method, assigned to local variables, rather than re-branching on every individual field access).

`ICW-077-viewport-scrollbar-overlay-hardening` already targets this exact code region, but for a different concern (nullable-safety hardening of track/thumb initialization state, not the axis-dispatch duplication). Recommend adding this as an explicit sub-item to `ICW-077`'s scope, since any hardening pass through this code will already be touching every one of these ternary sites — bundling the consolidation into the same PR is far cheaper than a separate pass later.

**Confidence:** 90% (both handler methods and the helper methods they call read in full; this is a straightforward code-shape observation, not requiring runtime verification).

### 1.6 Confirms and substantially raises confidence on the previously-flagged, previously-unverified accessibility gap (`ICW-037`)

My first report flagged `ICW-037` (accessibility baseline) at only **40% confidence**, explicitly noting *"would require reading all of `MainWindow.xaml`... which was out of this pass's LOC budget."* That budget has now been spent: **`MainWindow.xaml` (276 lines, full file) contains zero `AutomationProperties.Name`, zero `AutomationProperties.HelpText`, and no other accessibility-specific markup anywhere** — every interactive control (`Button`, `ComboBox`, `Slider`, `CheckBox`, `TextBox`) relies solely on its visible `Content`/adjacent `TextBlock` label for identification, which screen readers cannot reliably associate without explicit `AutomationProperties` or `LabeledBy` wiring. **This is a correction, not a new finding: raise `ICW-037`'s confidence from my original report's 40% to 90%.**

**Confidence:** 90% (full XAML file read directly, confirmed absence).

---

## 2. Corrections Summary Table

| Ticket | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-035` (renderer/pixelometer blend contract) | In Progress; Notes claim "both paths route through a shared sampler" | **Correction: claim is incomplete.** `MainWindow.TryReadPixelValue`'s independent max-based defect loop was missed — only `ResolveDisplayPixelValue` actually uses the shared `DefectOverlaySampler`. The two coexist and can produce visibly inconsistent numbers in the same HUD string. | §1.1 |
| `ICW-100` (overlay-precedence-and-pixelometer variant) | To Do | **Extend scope**: explicitly add `MainWindow.TryReadPixelValue`'s defect-loop to the file list — it's a third, unlisted implementation of the same "defect at this point" concept the ticket is trying to unify. | §1.1 |
| `ICW-055-pixelometer-performance` | Proposed, empty template | **Fill in with concrete scope**: eliminate the duplicate `_spatialIndex.Query` call per pixelometer update (one call feeding each of the two divergent defect computations in §1.1). | §1.2 |
| `ICW-037` (accessibility baseline) | Unverified (40% confidence in prior report) | **Raise confidence to 90%** — confirmed via full XAML read: zero `AutomationProperties` anywhere in the file. | §1.6 |
| `ICW-077` (scrollbar overlay hardening) | Proposed | **Extend scope**: consolidate the repeated axis-dispatch ternary pattern in the three scrollbar mouse handlers into a single per-call resolution, while the hardening pass is already touching this code. | §1.5 |
| `ICW-P1-SETTINGS-VALIDATION` | Proposed (already extended twice in prior reports) | **Further extend**: the XAML layer itself contributes to the validation-duplication risk — bounded `Slider` controls exist for some settings but not for the scene-generation `TextBox` fields, which have no UI-level bound at all. | §1.4 |
| *(new, no existing ticket found)* `ICW-FEATUREGRID-BINDING-DEADCODE` (or fold into `ICW-022`) | — | **New finding**: `SelectedAnnotationFeatures`'s XAML binding is permanently dead — `MainWindow` has no `INotifyPropertyChanged`, and code-behind unconditionally overwrites the bound property on every update. | §1.3 |

---

## 3. Assumptions & Open Questions

- §1.1's severity assumes a real-world scenario where two `SampleAnnotation`s spatially overlap with different defect values at the same point — I did not construct a live repro (would require running the WPF app), only traced the code paths that would produce divergent output in that scenario. The scenario itself (overlapping annotations) is plausible given `SampleImageGenerator`'s random placement logic but not confirmed as common in practice.
- §1.3's recommendation (move `SelectedAnnotationFeatures` to a ViewModel) assumes `FeatureDisplayItem` has no dependency that would make it awkward to reference from `InfiniteCanvas.ViewModels` — this wasn't independently checked; if `FeatureDisplayItem` is defined inside `MainWindow.xaml.cs` itself (likely, given the pattern of nested private types like `AnnotationDisplayOptions` at the bottom of the file), moving it would require relocating the type too, a slightly larger change than a one-line property move.
- I did not exhaustively re-verify every one of the ~10 pixelometer-related ticket files found by grep (`ICW-008`, `ICW-020`, `ICW-055-pixelometer-o1-lookup`, `ICW-076`) — only the three most directly relevant to this session's findings were read in full. The duplicate-ID observation (`ICW-055` appearing as two different files, `ICW-100` appearing as at least two different tickets across this and prior sessions) is noted but not separately investigated beyond what's already tracked under `ICW-081`.

---

*Methodology note: this session read `MainWindow.xaml` in full for the first time (previously only the code-behind had been reviewed), read the remaining previously-unread sections of `MainWindow.xaml.cs` (scrollbar handlers, pixelometer readout, tail-of-file nested types), and then specifically searched `docs/tasks/tickets/` for any existing ticket touching the affected files (`DefectOverlaySampler`, pixelometer, scrollbar, feature grid) before writing any recommendation — three tickets were read in full as a direct result of that search and are cited by name above rather than assumed absent.*
