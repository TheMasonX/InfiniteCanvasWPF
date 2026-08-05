# InfiniteCanvasWPF — Delta Report: Canvas Extraction Progress Review — A Leaky API Boundary in `CanvasControl`

**Previous reports:** fifteen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip. Substantial work landed since the last session: `CanvasControl`/`CanvasViewModel` now exist (replacing the old `CanvasViewportViewModel`), `ADR-0007` defines the full extraction sequence (`ICW-309` and `ICW-311` Done, `ICW-312` In Review, `ICW-313`–`316` Proposed), plus unrelated bug fixes (`ICW-317`/`318`, frame-shell flicker) landed in the same window.
**Scope, per this session's request:** focused specifically on the canvas-extraction effort, since that's the active priority. This report verifies the sequence so far and identifies one concrete, actionable gap in the current `CanvasControl` boundary that isn't covered by any of the six extraction tickets already filed.

---

## 1. Where the extraction actually stands, verified directly

`ICW-309` (decouple `MainViewModel` from canvas state) and `ICW-311` (move pan/zoom/scrollbar interaction into `CanvasControl`) are marked Done. I read `CanvasControl.xaml.cs` in full to confirm: it correctly owns pan (`OnViewportMouseMove`), anchor-pan (`OnAnchorPanTick`, with a documented sign-preserving exponential curve), mouse-wheel zoom (`OnViewportMouseWheel`), and all scrollbar drag/click handling — matching ADR-0007's Context section's claim about what `CanvasControl` owns today. `ICW-312` (data-source abstraction, In Review) has a four-seat council review already on file that explicitly identifies the exact pixelometer-hover-triggers-generation violation my fifth report found months ago, and plans to close it via a non-blocking resident-pixel-read contract. Good — that finding is being carried forward correctly into the active work, not lost.

---

## 2. New finding: `CanvasControl` exposes seven raw internal WPF elements as public properties, and `MainWindow` reaches through all of them — a boundary leak none of the six extraction tickets currently cover

`CanvasControl` exposes:
```csharp
public Border SurfaceHost => ViewportHost;
public Viewbox FrameHost => FramePresenter;
public TextBlock LoadingText => LoadingOverlay;
public TextBlock WorldReadout => PixelometerWorldText;
public TextBlock TileReadout => PixelometerTileText;
public TextBlock ValueReadout => PixelometerValueText;
public ProgressBar BusyBar => RenderBusyBar;
```
`MainWindow` aliases every one of these through private forwarding properties (`private Border ViewportHost => CanvasSurface.SurfaceHost;`, etc.) and then reaches through them directly — not through any method or bound property, but by mutating the underlying WPF elements' `.Text`, `.Visibility`, and `.Child` from application code: `LoadingOverlay.Text = "..."`, `LoadingOverlay.Visibility = Visibility.Visible/Collapsed` (4 sites), `RenderBusyBar.Visibility = ...` (4 sites, two of them from a background thread via `Dispatcher.Invoke`), and `ViewportHost.ActualWidth`/`ActualHeight` (8+ sites, for layout math).

**This is exactly the "shallow module" pattern this audit series has flagged in other parts of the codebase (`ISpatialIndexService<T>`, reports 4/8/11), now appearing in the newest code.** ADR-0007's stated goal is a "stable, app-agnostic boundary" that "another application" can host — but a control whose public C# surface includes raw `TextBlock`/`Border`/`Viewbox`/`ProgressBar` references requires any consumer to know `CanvasControl`'s internal element names and WPF types to display a loading message or a busy indicator. `PublishFrame(UIElement frame)` already demonstrates the *correct* pattern (a clean method, not an exposed `Viewbox`) — the other six properties don't follow it. None of `ICW-312`–`316` mention this specific set of properties; `ICW-312`'s council review focuses on scene/tile/spatial data sources, and `ICW-314` focuses on selection/tooltip, but nothing in the filed sequence addresses "loading state, busy indicator, and pixelometer text display are exposed as raw elements rather than a method-based API." Since `ICW-316` (assembly extraction) is the final step in the sequence and literally cannot happen cleanly while `MainWindow` depends on reaching into named XAML elements by concrete WPF type, this gap needs to close before that step, not be discovered at that step.

**Confidence:** 95% (every property and every call site read directly, both files).

---

## 3. Sharper sub-finding: `FramePresenter.Child` is set through *two different paths* — the clean method and a direct bypass — and the bypass is what the newest frame-shell fix uses

Grepping every use of the aliased raw elements in `MainWindow.xaml.cs` turned up something more concrete than a general leaky-API observation: `FramePresenter.Child` (i.e., `CanvasControl.FrameHost.Child`) is assigned in **two different places, two different ways**:
- The already-correct path: `CanvasSurface.PublishFrame(frame)` — a real method call.
- A direct bypass: `FramePresenter.Child = shell;` (line 619) and `FramePresenter.Child = null;` (line 1184) — raw property access, skipping `PublishFrame` entirely.

The variable name `shell` at line 619 and the surrounding context strongly indicate this is the **persistent frame shell mechanism from `ICW-317`** ("Use a persistent frame shell to stop per-frame Viewbox teardown flashes," Done) — meaning a legitimate, already-shipped bug fix was implemented by reaching around the control's existing encapsulated API rather than extending it. This is a concrete instance of exactly the risk report 6's "brittle pathways" framing warned about: two code paths doing the same underlying operation, one clean and one not, where a future change to `PublishFrame`'s internals (e.g., adding validation, logging, or the `CanvasFrame`-boundary migration `ICW-315` already plans) would silently not apply to the bypass path unless someone remembers both exist.

**Recommendation:** extend `PublishFrame` (or add a sibling method, e.g. `InstallFrameShell(UIElement shell)` / `ClearFrame()`) so the frame-shell mechanism goes through the control's real API, then make `FrameHost` `private`. This is a small, mechanical fix and a good first slice of the larger cleanup in §4.

**Confidence:** 90% (both assignment sites read directly; the "this is the frame-shell mechanism" attribution is inferred from the variable name and `ICW-317`'s description, not confirmed via a full read of the surrounding 619 and 1184 context in this session).

---

## 4. Concrete recommendation: a new ticket, sequenced alongside `ICW-314`

Propose `ICW-319` (or fold into `ICW-314`'s scope, since it's the same category of fix — "stop exposing internals, expose behavior"): add to `CanvasControl`'s real public API:
- `SetLoadingState(bool visible, string? message = null)` — replaces the four `LoadingOverlay.Text`/`.Visibility` call sites.
- `SetBusyIndicatorVisible(bool visible)` — replaces the four `RenderBusyBar.Visibility` call sites (including the two background-thread ones — worth confirming this new method marshals to the UI thread internally, rather than requiring every caller to remember `Dispatcher.Invoke`, which is itself a small win for consumers).
- `SetPixelometerReadout(string world, string tile, string value)` / `ClearPixelometerReadout()` — replaces the three `PixelometerXText` properties. This can land independently of `ICW-312`'s data-source work — it's about *how the value crosses the boundary*, not *where the value comes from*; `ICW-312` can keep computing the value in `MainWindow` for now and simply call the new method instead of writing three `.Text` properties directly.
- Either extend `PublishFrame` or add `ClearFrame()`/`InstallFrameShell()` to close §3's bypass.
- A read-only `Size ViewportSize { get; }` (or keep exposing width/height as two doubles if that's simpler) to replace the 8+ raw `ViewportHost.ActualWidth`/`ActualHeight` reads — lower priority than the four above since it's read-only and less actively leaking mutable state, but still blocks a clean assembly boundary.

Once all of these land, `SurfaceHost`, `FrameHost`, `LoadingText`, `WorldReadout`, `TileReadout`, `ValueReadout`, and `BusyBar` can all become `private` (or be deleted if `ApplyViewportState`/internal methods already have direct field access), closing the leak entirely.

---

## 5. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `CanvasControl`'s 7 exposed raw-element properties | Not covered by `ICW-312`–`316` | **New finding**: a boundary leak orthogonal to the already-planned data-source and selection/tooltip work — needs its own ticket or fold-in before `ICW-316` (assembly extraction) is feasible. | §2 |
| `FramePresenter.Child` dual-path assignment | Introduced by `ICW-317` (Done) | **New finding**: the shipped frame-shell fix bypasses the control's existing `PublishFrame` encapsulation rather than extending it. Small, mechanical fix recommended. | §3 |
| Proposed `ICW-319` (or fold into `ICW-314`) | — | **New ticket recommended**: four concrete method additions (`SetLoadingState`, `SetBusyIndicatorVisible`, pixelometer readout methods, frame-clear method) plus a viewport-size property, closing the leak entirely once `MainWindow`'s six raw-element aliases are deleted. | §4 |

---

## 6. Assumptions & Open Questions

- I did not read `CanvasViewModel.cs` (111 lines) or `CanvasControl.xaml` (75 lines) in full this session — time was spent on the higher-value `CanvasControl.xaml.cs` read and the cross-reference against `MainWindow`'s usage. A future session should read both, particularly `CanvasViewModel.cs`, to check whether it has any of the same "exposed internals" pattern or whether it's already clean (my read of `CanvasControl.xaml.cs` suggests the ViewModel itself is properly encapsulated — `ViewModel.Pan(...)`, `ViewModel.Camera.Capture()`, `ViewModel.SceneBounds` all read as reasonable method/property calls, not raw-element leaks — but this wasn't independently confirmed against the ViewModel's own source).
- I did not read `docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md` in full — only grepped it for pixelometer-related mentions to confirm §1's claim. A full read might surface additional context relevant to this report's recommendations, or might already address §2/§3 somewhere I didn't search for.
- §3's attribution of the `FramePresenter.Child = shell` bypass to `ICW-317` specifically is inferred from naming and timing, not confirmed by reading `ICW-317`'s full ticket text or diffing the exact commit that introduced it — worth a quick confirmation in a future session before treating it as certain.

---

*Methodology note: this session read `docs/ADR/0007-canvas-reusable-component-boundary.md` and `CanvasControl.xaml.cs` in full, checked the six sequenced extraction tickets' status and summaries, then specifically grepped every `MainWindow.xaml.cs` usage of `CanvasControl`'s exposed raw-element properties to determine which were genuinely read-only telemetry versus active leaky-write paths — the latter search is what surfaced both the general finding (§2) and the more specific dual-path bug-adjacent smell (§3).*
