# InfiniteCanvasWPF — Deep-Dive Code Audit

**Commit audited:** `43bfd55bbae7e14a590784f7831e5261eecfd69b` (main, 2026-07-24, "feat: add zoom presets, cache debug controls, and class-colored defects")
**Scope:** Every file in `src/`, `tests/`, `benchmarks/`, plus all docs (`DesignDoc.md`, ADRs, handoffs, JIRA/tickets) — 70 files, ~3,770 lines of C#/XAML reviewed line-by-line.
**Method:** Full source pulled via the public repository archive tarball at the exact commit (not the repository web UI/web_fetch, to avoid truncation). Cross-checked every finding against `docs/tasks/JIRA.md`, `docs/tasks/active-tasks.md`, `docs/tasks/tickets/*.md`, and both ADRs before writing it up, to avoid re-reporting already-tracked work.
**Confidence values** reflect how certain I am the finding is a real, reproducible issue given only static reading (no compiler/runtime in this environment) — not how severe it is.

---

## 1. Executive Summary

The codebase is small, disciplined, and unusually well-documented for a greenfield project (ADRs, handoffs, per-ticket findings). Lock-free state (`CameraTransform`, `LiveSpatialIndexService`) is implemented correctly with proper CAS loops and immutable snapshots — no torn reads found. `ZeroCopyBitmapFactory` uses a real `SafeHandle`, closing the unmanaged-handle-leak risk called out in `DesignDoc.md`'s original POC code.

The issues found are concentrated in three areas:

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | No global unhandled-exception handler; ~15 `async void` handlers can crash the whole process on any exception the two narrow `catch` clauses don't cover | **High** | 85% |
| 2 | `SampleImageGenerator.GenerateSet` validation always blames `nameof(imageCount)` regardless of which parameter is actually invalid | **Medium** | 95% |
| 3 | `README.md` "Run the MVP" section describes a discontinued live-ingestion demo that contradicts ADR-0002 and no longer exists in code | **Medium** | 95% |
| 4 | `CanvasViewportViewModel.RefreshCommand`/`RefreshAsync` is dead in production (superseded by `ApplyFrame` per ICW-003) but still shipped and tested | **Medium** | 90% |
| 5 | `IRenderer<TScene,TOutput>` + `ViewportRenderRequest` are fully unimplemented, zero-usage abstractions | **Low-Medium** | 95% |
| 6 | Point-based `ZeroCopyBitmapFactory.GenerateFrozenBitmap(IEnumerable<ScreenPoint>, …)` overload is a second, divergent rendering path exercised only by tests/benchmarks, never by the app | **Medium** | 85% |
| 7 | Selection-outline animation restarts every render frame (new `Shape` + new animation clock each frame) instead of persisting — marching-ants/pulse visibly resets during any pan/zoom | **Medium** | 75% |
| 8 | Pixelometer tile lookup (`TryReadPixelValue`) is an O(n) linear scan over up to 2,000 tiles on every mouse-move, despite tiles sitting on a uniform addressable grid | **Medium** | 85% |
| 9 | Back-buffer recycling reuses the same unmanaged memory section for a new frame while WPF's composition thread may still be presenting the previous frame from that same section — no handoff synchronization | **Medium (theoretical)** | 55% |
| 10 | `MainWindow.xaml.cs` (1,047 lines) is a God Object mixing input handling, zoom/pan math, generation validation, and pixelometer logic with zero direct unit-test coverage | **Medium** | 90% |
| 11 | Per-pixel inverse-transform division in `DrawTile`/`DrawDefectPatch` inner loops — not hoisted/incrementalized | **Low** (perf) | 70% |
| 12 | DRY violations: `TryGetPixelValue`/`TryGetDefectValue` near-duplicated; `CameraTransform.GetViewportBounds` duplicated in `CameraSnapshot`; live-index type-check duplicated in `CanvasViewportViewModel` | **Low** | 90% |
| 13 | XAML default control values (`TilesXTextBox="2"`, etc.) hardcoded in markup, duplicated against code-behind field defaults | **Low** | 90% |
| 14 | Magic-number epsilon (`0.0001`) for float scale-equality in `EnforceZoomFloor`; no named constant | **Low** | 80% |
| 15 | Finalizer (`~ZeroCopyBitmapFactory`) takes a lock via `Dispose(false)` — standard anti-pattern, low real-world risk here | **Low** | 60% |

**Already-tracked items (not duplicated below, only supplemented with new evidence where I found it):** ICW-004 (overdraw), ICW-005 (DPI/max-surface policy), ICW-007 (overlay element pooling). See §3.

**No evidence found of:** memory leaks in the Kernel32 interop path (SafeHandle pattern is correct), torn reads in the lock-free camera/spatial state, or STRtree misuse.

---

## 2. New Findings (Not Already Tracked)

### 2.1 [HIGH] No global crash safety net around widespread `async void` handlers
**Confidence: 85%**

`App.xaml.cs` is an empty partial class; `App.xaml` sets no `DispatcherUnhandledException` handler. `MainWindow.xaml.cs` has at least 13 `async void` event handlers (`OnAnnotationMouseLeftButtonDown`, `OnViewportMouseMove`, `OnAnchorPanTick`, `OnViewportMouseWheel`, `OnZoomPresetSelectionChanged`, `OnApplyCustomZoomClicked`, `OnResizeElapsed`, `OnDisplayModeSelectionChanged`, `OnOutlineThicknessChanged`, `OnLabelSizeChanged`, `OnShowLabelsChanged`, `OnRegenerateClicked`, `OnDebugDumpCacheClicked`, `OnClosed`).

`RequestRenderAsync` (lines 156–173) only swallows `OperationCanceledException` and `ObjectDisposedException`, and only when `_lifetime.IsCancellationRequested`. Any other exception raised inside the render pipeline — e.g. an `OverflowException` from `Bgra32BufferLayout`'s `checked` arithmetic on an oversized viewport, an `OutOfMemoryException` allocating an 8192×2048 `byte[]` tile, or a bad annotation bounds causing a negative array index in `DrawTile`/`DrawDefectPatch` — propagates out of the `async void` handler. In WPF, an unhandled exception from an `async void` handler becomes an unhandled `DispatcherUnhandledException`; with no handler registered, the default behavior terminates the process.

Only `OnLoaded` (lines 76–93) and `OnResizeElapsed` (catches `OperationCanceledException` only) have any try/catch at all. Every other handler is unguarded.

**Recommendation:** Add `Application.DispatcherUnhandledException` (and `AppDomain.CurrentDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` for completeness) in `App.xaml.cs`, log, and either recover or fail gracefully with a message instead of a silent crash. This is cheap (a few lines) and directly protects the "no legacy debt" goal — an unhandled crash in a greenfield demo undermines confidence in everything else that's been built.

---

### 2.2 [MEDIUM] Misattributed argument validation in `SampleImageGenerator.GenerateSet`
**Confidence: 95%** (directly verifiable from source, no external dependency)

`src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:33-36`:
```csharp
if (imageCount <= 0 || pixelWidth <= 0 || pixelHeight <= 0 || objectsPerTile < 0 || columns <= 0 || defectPoolSize <= 0)
{
    throw new ArgumentOutOfRangeException(nameof(imageCount));
}
```
Six independent preconditions are OR'd together but the exception always names `imageCount`, even when the actual violation is `pixelWidth`, `pixelHeight`, `objectsPerTile`, `columns`, or `defectPoolSize`. This is misleading during debugging and directly contradicts the `nameof(rows)` pattern used two lines later (line 38-41), which is correctly attributed — so the fix pattern already exists in the same method.

**Recommendation:** Split into individual `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(...)` calls per parameter (the codebase already uses this pattern elsewhere, e.g. `Bgra32BufferLayout.cs:7-8`).

---

### 2.3 [MEDIUM] `README.md` describes a discontinued application behavior
**Confidence: 95%**

`README.md` "Run the MVP" section states: *"The demo starts with 100,000 deterministic spatial records, ingests 250 more every 500 ms, and publishes the hot buffer into a packed STR snapshot every two seconds. Drag to pan and use the mouse wheel to zoom."*

This describes the point-cloud demo that ADR-0002 explicitly superseded: *"Generate the static inspection scene once. Do not run the former periodic point-ingestion timer."* (`docs/ADR/0002-inspection-raster-and-annotation-layers.md`). I confirmed via `grep` that `MainWindow.xaml.cs` contains exactly two `DispatcherTimer` instances — `_resizeTimer` (150ms debounce) and `_anchorPanTimer` (16ms RMB-pan tick) — and no periodic-ingestion timer exists anywhere in the codebase. The actual current default is 64 static tiles (2×32) generated once at startup (`docs/tasks/JIRA.md` ICW-012).

**Recommendation:** Rewrite the "Run the MVP" section to describe the current tile/annotation inspection scene, side panel, zoom presets, and pan/zoom/select interactions. This is a 10-minute fix that removes a materially incorrect onboarding description for anyone new to the repo.

---

### 2.4 [MEDIUM] `CanvasViewportViewModel.RefreshCommand` is dead in production
**Confidence: 90%**

`grep` confirms `RefreshCommand`/`RefreshAsync` (`src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs:41-56`) is invoked only from `tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs`. `MainWindow.xaml.cs` calls `_viewModel.ApplyFrame(...)` directly (line 211) and never touches `RefreshCommand`. JIRA/ICW-003 ("Remove duplicate spatial query per rendered frame... Render statistics reuse the viewport query result") documents exactly the refactor that made `RefreshAsync`'s separate `_spatialIndexService.Query(viewport)` call redundant — but the old command was left in place afterward instead of removed.

This is a textbook case of the instruction's own concern: dead code retained after a refactor, still tested (so CI stays green and gives false confidence), in a project explicitly trying to avoid legacy debt.

**Recommendation:** Delete `RefreshCommand`/`RefreshAsync` and its two tests, or if a manual-refresh affordance is still wanted in the UI, wire an actual button to it and delete `ApplyFrame`'s duplicate-purpose path instead. Don't keep both indefinitely.

---

### 2.5 [LOW-MEDIUM] Fully dead rendering abstraction: `IRenderer<TScene,TOutput>` / `ViewportRenderRequest`
**Confidence: 95%**

`grep -rn "IRenderer"` and `grep -rn "ViewportRenderRequest"` across the whole repo return only their own declarations (`src/InfiniteCanvas.Rendering/IRenderer.cs`, `ViewportRenderRequest.cs`). No class implements `IRenderer<,>`; no code constructs a `ViewportRenderRequest`. These appear to be a vestige of an earlier, more generic rendering-abstraction plan (consistent with `DesignDoc.md`'s original `SpatialViz.Rendering` namespace sketch) that was superseded by the concrete `ZeroCopyBitmapFactory.GenerateFrozenBitmap(tiles, annotations, camera)` overload actually used today.

**Recommendation:** Delete both files, or if the generic renderer abstraction is still a real future goal, add an ADR describing why it's being kept unimplemented and what will consume it. Currently it's unreferenced surface area with no test, no consumer, and no doc explaining its purpose.

---

### 2.6 [MEDIUM] Divergent, only-test-reachable rendering path: point-based `GenerateFrozenBitmap` overload
**Confidence: 85%**

`ZeroCopyBitmapFactory` has two `GenerateFrozenBitmap` overloads (`ZeroCopyBitmapFactory.Windows.cs:60` and `:109`). `grep` confirms the point-based overload (`IEnumerable<ScreenPoint>, Bgra32Color?`) is called only from `tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs` and `benchmarks/.../ProjectionAndBitmapBenchmarks.Windows.cs`. `MainWindow.xaml.cs:205` exclusively uses the tile+annotation overload.

This means the class carries two independent pixel-composition code paths with different fill logic (single-pixel plot vs. tile+defect blend), only one of which the shipping app ever exercises. The point-based path is fully covered by tests that will keep passing forever even if it silently diverges further from the real render pipeline, because nothing routes production input through it.

**Recommendation:** Either (a) delete the point-based overload and repoint the benchmark/tests at the real overload (adjusting benchmark inputs to a synthetic tile+annotation set), or (b) if it's intentionally kept as a lightweight micro-benchmark harness for raw write throughput, say so explicitly in a comment/ADR so a future reader doesn't mistake it for a maintained production path.

---

### 2.7 [MEDIUM] Selection-outline animation resets every render frame
**Confidence: 75%**

`BuildFrameVisual` (`MainWindow.xaml.cs:255-322`) constructs a brand-new `Rectangle outline` for every visible annotation on every single frame — including frames triggered purely by panning, zooming, resize-debounce, or the live pixelometer render — and, if that annotation is currently selected, calls `_selectionOutlineAnimator.Apply(outline)` (line 320-321) on the **new** `Rectangle` instance.

`MarchingDashSelectionOutlineAnimator.Apply` and `PulseOpacitySelectionOutlineAnimator.Apply` (lines 987-1010) both start a fresh `DoubleAnimation` with `RepeatBehavior.Forever` from a fixed starting value (`0`, or `1`) on that new `Shape` every time. Because the whole overlay — including the selected annotation's outline — is rebuilt from scratch on each frame (there is no persistent `Shape` reused across frames for a selection), the animation restarts from its initial phase every time a new frame is published, rather than continuing smoothly. In practice this means the marching-ants border visibly "jumps" back to its start position on every pan/zoom tick rather than animating continuously, which is the opposite of the intended visual effect.

**Recommendation:** This is a natural companion fix to the already-tracked ICW-007 (retained overlay pooling): if/when annotation overlay elements are pooled and reused across frames instead of rebuilt, the selected item's `Shape` instance should persist across frames (or the elapsed animation clock time should be captured and reapplied) so the animation doesn't reset. Flagging this explicitly because ICW-007's current wording only discusses cost/performance of rebuilding, not the correctness/UX side-effect on animation continuity.

---

### 2.8 [MEDIUM] O(n) linear tile scan for pixelometer lookups
**Confidence: 85%**

`TryReadPixelValue` (`MainWindow.xaml.cs:929-951`) does `foreach (var tile in _tiles) { if (tile.TryGetPixelValue(...)) ... }` — a linear scan over every generated tile (up to 2,000, per the `TryReadGenerationOptions` cap at line ~886) on **every mouse-move event** while hovering the viewport. ICW-013's ticket notes that *defect* sampling was optimized to query the spatial index (`_spatialIndex.Query(sampleArea)`, line 940) instead of scanning all annotations — but the outer *background-tile* lookup that wraps it was not given the same treatment, despite tiles sitting on a perfectly uniform, arithmetic-addressable grid (`tileX = (tileIndex % columns) * pixelWidth`, `tileY = (tileIndex / columns) * pixelHeight` — `SampleImageGenerator.cs:54-55`).

With the default (2×32=64) tiles this is negligible; at the 2,000-tile cap allowed by the UI, this becomes a real per-mouse-move cost (mouse-move fires at high frequency).

**Recommendation:** Compute the tile index directly from world coordinates (`columnIndex = (int)(worldX / pixelWidth)`, `rowIndex = (int)(worldY / pixelHeight)`, then index into a flat array) instead of scanning. This requires exposing `columns`/`pixelWidth`/`pixelHeight` to `MainWindow` (already tracked as fields) or adding a small grid-lookup helper alongside `SampleImageGenerator`.

---

### 2.9 [MEDIUM, theoretical] Back-buffer recycling reuses live unmanaged memory without a compositor handoff wait
**Confidence: 55%** — plausible given documented WPF/InteropBitmap thread-affinity semantics, but I could not execute the app to confirm visually.

`AcquireBackBuffer`/`PublishFrame` (`MainWindow.xaml.cs:218-249`) implement double-buffering by recycling the *previous front* `ZeroCopyBitmapFactory` as the *next back* buffer once its dimensions match. Each `ZeroCopyBitmapFactory` instance owns one Kernel32 file mapping (`_section`/`_view`) for its lifetime; `GenerateFrozenBitmap` calls `Imaging.CreateBitmapSourceFromMemorySection` pointing at that same section every time it's invoked, and `NativeMemory.Clear` + pixel writes happen directly into `_view` (`ZeroCopyBitmapFactory.Windows.cs:70/120`).

Because `Freeze()` only makes the *bitmap object* immutable and cross-thread-safe — it does not, and cannot, guarantee WPF's composition thread has finished sampling the *backing memory* for the previous frame before the next frame's background `Task.Run` starts calling `NativeMemory.Clear` and rewriting that same memory region. `Image.Source`/`FramePresenter.Child` being replaced on the UI thread only detaches the *managed reference*; render/composition happens asynchronously relative to that. The existing handoff doc (`docs/handoffs/2026-07-23-render-coalescing.md`) already flags a *related* but narrower risk ("Keep the factory alive while its bitmap is assigned to WPF... do not reuse a disposed factory") — this finding is about reusing a **live, still-possibly-being-composited** buffer, which is a distinct risk from the already-documented disposal-ordering one.

**Impact if real:** visual tearing/flicker under fast pan/zoom, not a crash or memory-safety violation (the memory itself remains valid and mapped throughout).

**Recommendation:** If tearing is ever observed during interactive testing, consider a 3-buffer rotation (front/back/pending-free) so a buffer is only recycled after at least one additional frame has been presented, or profile actual compositor timing before adding complexity — this may be a non-issue in practice given `Freeze()` + double-buffering is a common WPF interop pattern, hence the lower confidence.

---

### 2.10 [MEDIUM] `MainWindow.xaml.cs` God Object with zero direct unit-test coverage
**Confidence: 90%**

At 1,047 lines, `MainWindow` mixes: mouse/keyboard input handling, zoom-preset math (`TryComputeUniformZoomDelta`, `ApplyScaleWithUniformFirst`, `ApplyFitToWidthZoom`/`ApplyFitToHeightZoom`, `ApplyPercentZoom`, `ComputeMinimumZoom`), anchor-pan velocity math (`ApplyDeadZone`, `OnAnchorPanTick`), scene generation orchestration (`RegenerateSceneAsync`, `TryReadGenerationOptions`), pixelometer sampling (`UpdatePixelometer`, `TryReadPixelValue`, `BlendDefect`), and busy-state tracking (`BeginBusyOperation`/`EndBusyOperation`). None of this logic is unit-tested — the entire `tests/InfiniteCanvas.Tests` and `tests/InfiniteCanvas.Windows.Tests` suites (26 tests total, confirmed by count) cover `Core`, `Spatial`, `Rendering`, and `ViewModels` only. Every piece of the zoom-preset/anchor-pan/generation-validation math above is pure and could be unit-tested in isolation, but it's currently coupled directly to XAML-named elements (`ViewportHost.ActualWidth`, `ZoomPresetComboBox.SelectedIndex`, etc.), making it untestable without a running WPF window.

This isn't unique to this repo (code-behind coupling is a known WPF/MVVM tension), but given the project's stated goal of avoiding technical debt and its otherwise-strong test discipline elsewhere, this is the single largest coverage gap in the codebase by line count.

**Recommendation:** Extract the pure math (zoom presets, dead-zone, min-zoom computation, generation-option validation) into a plain class (e.g. `ViewportZoomCalculator`, `GenerationOptionsValidator`) that takes primitive inputs and returns results, called by `MainWindow` but unit-testable independently of WPF. This is a mechanical, low-risk refactor given the functions are already largely side-effect-free (they read `_camera`/fields but don't need to).

---

## 3. Supplementary Evidence for Already-Tracked Backlog Items

These are **not new tickets** — cross-referenced against `docs/tasks/JIRA.md` and open items to avoid duplication. Listed here only because I found specific line-level evidence that may help whoever picks up the existing ticket.

- **ICW-004 (measure zoomed-out pixel overdraw, To Do):** `DrawTile`/`DrawDefectPatch` (`ZeroCopyBitmapFactory.Windows.cs:161-183`, `200-228`) recompute `worldX`/`worldY` via a **division** for every destination pixel in the inner loop (`(x - camera.OffsetX) / camera.ScaleX`), rather than hoisting a per-row starting value and incrementing by a precomputed step. This compounds whatever overdraw cost ICW-004 measures — worth benchmarking division-vs-increment alongside the overdraw question rather than as a separate pass. Confidence 70%.
- **ICW-005 (DPI-aware resize / max surface policy, To Do):** The `4096` max-render-dimension clamp is duplicated as a literal in two places (`RenderFrameAsync` line ~191, `ClampCameraToScene` line ~397) with no shared named constant. Whoever implements ICW-005 will need to touch both call sites; worth consolidating into one constant as part of that work rather than leaving two literals to keep in sync. Confidence 90%.
- **ICW-007 (pool retained annotation overlay elements, To Do):** Confirmed by direct reading that `BuildFrameVisual` allocates, per visible annotation, per frame: 1-2 `SolidColorBrush` (unfrozen — no `.Freeze()` call anywhere in this method, meaning WPF pays thread-affinity checks on them even though they're never mutated), 1 `Rectangle`, 1 `Grid`, 1 `Border` (+ a second `Border`+`TextBlock` if labels are on), and 1 `ToolTip`. This confirms the ticket's premise is real and quantifies the specific allocation shape for whoever implements pooling. Also see §2.7 above (animation-continuity side effect of the same rebuild pattern) — a distinct concern from the pure performance angle ICW-007 currently describes.

---

## 4. Minor / Low-Priority Findings

| Finding | Location | Confidence |
|---|---|---|
| `SampleImageTile.TryGetPixelValue` and `SampleAnnotation.TryGetDefectValue` are near-identical (bounds check + clamp + sample) with no shared helper | `SampleImageTile.cs:128-152`, `:215-239` | 90% |
| `CameraTransform.GetViewportBounds` (instance) and `CameraSnapshot.GetViewportBounds` duplicate the same inverse-projection formula | `CameraTransform.cs:103-124`, `:206-223` | 90% |
| Live-index type-check (`is LiveSpatialIndexService<T> liveSpatialIndexService`) duplicated between `ApplyFrame` and `RefreshAsync` | `CanvasViewportViewModel.cs:35-38`, `:52-55` | 85% |
| XAML hardcodes default control text (`TilesXTextBox Text="2"`, `TilesYTextBox Text="32"`, `ObjectsPerTileTextBox Text="16"`) duplicating code-behind field defaults (`_tileColumns=2`, `_tileRows=32`, `_objectsPerTile=16`); the two will silently drift if either is changed without the other | `MainWindow.xaml:148-156`, `MainWindow.xaml.cs:39-41` | 90% |
| Magic-number epsilon `0.0001` for float scale-equality comparison, no named constant, arbitrary tolerance | `MainWindow.xaml.cs:411` (`EnforceZoomFloor`) | 80% |
| `~ZeroCopyBitmapFactory()` finalizer calls `Dispose(false)` which takes `lock (_lifetimeGate)` — textbook finalizer-thread lock anti-pattern; low real risk here since the gate isn't held elsewhere during shutdown, but worth the trivial fix (guard with an `Interlocked`-based disposed flag instead) | `ZeroCopyBitmapFactory.Windows.cs:55-58`, `244-257` | 60% |
| `SampleAnnotation` is a `record` with a `byte[] DefectPixels` member and `IReadOnlyDictionary<string,double> Features`; default record `Equals`/`GetHashCode`/`ToString` use reference equality for the array and won't produce meaningful value comparisons if annotations are ever compared/deduplicated by value later | `SampleImageTile.cs:203-213` | 70% |
| `GenerateSet`'s `imageCount` parameter is dual-purpose: it sets tile count only when `rows` is `null`; when `rows` is supplied, `imageCount` is silently ignored in favor of `columns * rows`. The parameter name doesn't communicate this conditional meaning | `SampleImageGenerator.cs:21-44` | 80% |
| `LinearSpatialIndexBuilder<T>` / `ImmutableSpatialIndexService<T>` are exercised only by tests, never by the shipping app (which always uses `StrTreeSpatialIndexBuilder`) — but this is explicitly called out as an intentional extensibility/testing seam in `README.md`, so it's **not** flagged as dead code, just noted for completeness | `LinearSpatialIndexBuilder.cs`, `README.md` "Spatial indexing" section | n/a (by design) |

---

## 5. What I Verified Was *Not* a Bug (to save a future reviewer's time)

- **`#if WINDOWS` symbol with no explicit `<DefineConstants>`:** Initially looked like every Windows-only code path (half the `Rendering` project, the benchmark's Windows suite) might be silently uncompiled dead code. Verified against Microsoft's documented SDK behavior: `net10.0-windows` (any OS-specific TFM) auto-generates the `WINDOWS` preprocessor symbol via the SDK's implicit target-platform symbol generation — no explicit `DefineConstants` entry is needed. **Not a bug.**
- **Lock-free `CameraTransform`/`LiveSpatialIndexService` CAS loops:** Traced every `Interlocked.CompareExchange` retry loop; found no torn-read or lost-update paths. The one failure-recovery branch in `PublishSnapshotAsync` (`LiveSpatialIndexService.cs:98-110`) correctly re-merges `PublishingItems`+`HotItems` back into `HotItems` on a build failure, verified against the existing test `PublishSnapshotAsync_PromotesHotBufferWithoutDroppingNewItems`.
- **`SafeFileMappingHandle`:** Correctly derives from `SafeHandleZeroOrMinusOneIsInvalid` and closes the handle in `ReleaseHandle()`. This resolves the raw-`IntPtr`-leak risk that existed in the original `DesignDoc.md` POC code snippet — a genuine improvement over the design doc's own reference implementation.
- **`EndBusyOperation`/`BeginBusyOperation` counter balance:** Verified all call sites (`RequestRenderAsync`, `RegenerateSceneAsync`) pair Begin/End via `try/finally`, including the nested case where `RegenerateSceneAsync` calls `RequestRenderAsync` internally (counter goes 0→1→2→1→0 correctly). No imbalance found.

---

## 6. Assumptions and Open Questions

- I did not run `dotnet build`/`dotnet test` in this environment (no .NET SDK / Windows runtime available in the sandbox); all findings are from static reading. Line numbers were taken directly from the fetched source and should match the given commit exactly.
- I assumed "every line of every file" includes `tests/` and `benchmarks/`, which I read in full; I did not deep-audit `DesignDoc.md`'s prose beyond confirming code matches or diverges from it, since it's a planning artifact rather than shipped code.
- The tearing risk in §2.9 is the one finding here I could not confirm without actually running the app under a fast pan/zoom stress scenario — flagged at lower confidence accordingly. If someone can reproduce (or rule out) visible tearing during rapid interaction, that would resolve the open question definitively.
- I did not find any existing ticket discussing global exception handling (§2.1) or the README staleness (§2.3) — if either is already known and intentionally deferred, apologies for the duplication; I checked `active-tasks.md`, `JIRA.md`, and all six ticket files and found no reference to either.

---

## 7. Suggested Priority Order for the Implementing Engineer

1. **§2.1** — Add `DispatcherUnhandledException` handler (highest risk-to-effort ratio; a few lines, closes a whole-process-crash exposure).
2. **§2.3** — Fix `README.md` (10 minutes, pure documentation correctness).
3. **§2.2** — Fix `GenerateSet` argument attribution (mechanical, ~10 lines).
4. **§2.4** and **§2.5** — Delete dead `RefreshCommand` and `IRenderer`/`ViewportRenderRequest` (mechanical deletions + test cleanup).
5. **§2.8** — Pixelometer O(n)→O(1) tile lookup (small, contained change, clear perf win at the tile-count ceiling).
6. **§2.10** — Extract pure zoom/generation-validation logic out of `MainWindow` for testability (larger refactor; do this before the codebase grows further, since every additional interaction feature currently adds more untested code-behind).
7. **§2.6, §2.7, §2.9** — Roll into whichever session next touches `ZeroCopyBitmapFactory`/`BuildFrameVisual`/ICW-007, since they're all in the same area of the rendering pipeline.
8. Everything in §4 — low-effort DRY/consistency cleanups, batch them into one pass.
