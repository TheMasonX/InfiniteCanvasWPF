# InfiniteCanvasWPF — Deep-Dive Code Audit

**Repo:** `TheMasonX/InfiniteCanvasWPF` · **Commit audited:** `84ddba2320296d0bbbe31171bf5b9d91bf8010a8` (branch `main`)
**Scope:** Every file in the tree — `src/`, `tests/`, `benchmarks/`, `docs/`, project/solution files, README, DesignDoc. 4,786 LOC across .cs/.xaml/.md.
**Method:** Full-text retrieval via the public repository archive tarball (bypasses web UI/`web_fetch` truncation), line-by-line manual review, cross-checked against `docs/tasks/JIRA.md`, `docs/tasks/active-tasks.md`, tickets, ADRs, and handoffs to avoid duplicating already-tracked work.

---

## 1. Executive Summary

The codebase is well above average for a "vibe-coded" WPF MVP: immutable camera state with CAS updates, a genuinely atomic hybrid live/snapshot spatial index, real double-buffered zero-copy bitmap presentation, and decent NUnit coverage of the Core/Spatial/Rendering layers. The core architectural pillars from `DesignDoc.md` are faithfully implemented.

The risk is concentrated almost entirely in **`MainWindow.xaml.cs` (885 lines)**, which has grown into a God Class holding UI wiring, scene orchestration, render-to-visual-tree composition, an input state machine, and two private strategy-pattern implementations — none of which are unit-testable in their current location. That file, plus documentation drift and a handful of dead code paths, account for most of the findings below.

**Nothing found rises to "will corrupt data" severity.** The highest-severity items are: (a) no top-level exception handling, so any unexpected exception during render/interaction crashes the app; (b) user interaction is not gated against in-flight scene regeneration, allowing a torn read across three independently-mutated fields; (c) one input field (objects-per-tile) has no upper bound while its sibling (tile count) does, and generation cannot be cancelled once started.

**Already tracked — not duplicated here:** ICW-004 (overdraw measurement), ICW-005 (DPI/max-surface policy), ICW-007 (overlay element pooling), the `Stretch.Fill` vs. letterboxing question, and the animation-mode/annotation-preset runtime toggle follow-up. Where a finding below *touches* one of these tickets, it's cited as supplemental evidence, not a new item.

### Findings by severity

| Severity | Count | Confidence range |
|---|---|---|
| High | 8 | 65–95% |
| Medium | 11 | 55–90% |
| Low / opportunity | 8 | 55–95% |

### Top 5 to fix first (highest impact-to-effort ratio)

1. **F-01** Add `Application.DispatcherUnhandledException` + tighten `RequestRenderAsync` catch scope (crash prevention, ~30 min).
2. **F-06** Extract the duplicated pixel-sampling/blend logic into one shared helper (correctness-drift prevention, ~1–2 hrs).
3. **F-03** Cap `objectsPerTile` in `TryReadGenerationOptions` (5 min, closes an unbounded-hang hole).
4. **F-04** Fix `README.md` "Run the MVP" section — it describes a demo that no longer exists (10 min).
5. **F-07** Route pixelometer hit-testing through `_spatialIndex.Query` instead of a full linear scan (~30 min, real interactive-perf win).

---

## 2. Findings — High Severity

### F-01 — No top-level exception handling; `async void` handlers can crash the app
**Files:** `src/InfiniteCanvas.App/App.xaml.cs` (5 lines, no override), `MainWindow.xaml.cs:78,419,453,498,556,631,644,655,666,677,704,749`
**Confidence: 85%**

`App.xaml.cs` registers no `DispatcherUnhandledException` handler. `RequestRenderAsync` (line 156-168) only catches `OperationCanceledException` and `ObjectDisposedException`:

```csharp
private async Task RequestRenderAsync()
{
    try { await _renderAction.RequestAsync(); }
    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
}
```

It's awaited from 8 different `async void` UI event handlers (mouse move, wheel, anchor-pan tick, resize-elapsed, display-option changes, annotation click). Any other exception surfacing from `RenderFrameAsync` — e.g. an `IndexOutOfRangeException` from the annotation/tile sampling code, or a `Win32Exception` from bitmap remapping on a hostile display configuration — propagates out of an `async void` method, which WPF cannot marshal back to a caller; it becomes an unhandled dispatcher exception and terminates the process.

**Fix:** Register `Application.DispatcherUnhandledException` in `App.xaml.cs` as a last-resort safety net (log + optionally continue), and widen `RequestRenderAsync`'s catch to log-and-swallow *all* exceptions with a status-bar message, matching the existing pattern already used in `OnLoaded` (lines 87-94).

---

### F-02 — Viewport interaction isn't gated during scene regeneration → torn scene state
**Files:** `MainWindow.xaml.cs:104-154` (`RegenerateSceneAsync`), `429-587` (mouse handlers), `620-629` (`OnViewportSizeChanged`)
**Confidence: 75%**

`RegenerateSceneAsync` mutates three independent fields non-atomically, with `await` points between them:

```csharp
InitializeSpatialState();                 // new _spatialIndex, new _viewModel  (line 113)
...
_tiles = await Task.Run(...);             // (line 121)
_annotations = _tiles.SelectMany(...);    // (line 132)
_spatialIndex.AddRange(_annotations);
...
await _spatialIndex.PublishSnapshotAsync(...);
```

`_generationGate` (a `SemaphoreSlim`) only prevents *re-entrant* calls to `RegenerateSceneAsync` itself. `OnViewportSizeChanged` explicitly checks `LoadingOverlay.Visibility` before acting (line 622) — but the mouse handlers (`OnViewportMouseMove`, `OnViewportMouseWheel`, `OnViewportMouseRightButtonDown`/anchor-pan tick) have **no equivalent guard**. A user panning/zooming while a large regenerate is in flight can trigger `RenderFrameAsync`, which reads `_tiles`, `_annotations`, and `_spatialIndex` mid-swap — e.g. the new (empty-until-published) `_spatialIndex` paired with the old `_tiles`, or vice versa — producing a visually inconsistent single frame. Not a crash, but a real, reproducible consistency gap, and notably the codebase already has the *correct* pattern next door: `CameraTransform` bundles its four related doubles into one `TransformState` swapped via `Interlocked.CompareExchange`. The scene fields deserve the same treatment.

**Fix:** Either (a) disable `ViewportHost` mouse/wheel input while `LoadingOverlay` is visible (cheapest, matches the existing resize guard), or (b) wrap `_tiles`/`_annotations`/`_spatialIndex` into one `SceneState` record swapped atomically, consistent with the `CameraTransform` precedent already in this codebase.

---

### F-03 — `objectsPerTile` has no upper bound; generation isn't cancellable once started
**Files:** `MainWindow.xaml.cs:715-747` (`TryReadGenerationOptions`), `SampleImageGenerator.cs:14-78,116-159`
**Confidence: 80%**

```csharp
if (!int.TryParse(ObjectsPerTileTextBox.Text, out var objectsPerTile) || objectsPerTile < 0) { ... }
...
if ((long)columns * rows > 2000) { validationError = "Tile count must be 2000 or less for this demo."; return false; }
```

Tile count is explicitly capped at 2000; `objectsPerTile` is only checked for `>= 0`. A user (or a fuzzed input) entering a very large value (e.g. `1000000`) drives `GenerateAnnotations` (called once per tile, unconditionally, not lazily) to allocate an array of that size and run bilinear resampling (`ResampleTemplate`, O(defectWidth×defectHeight) per annotation) that many times per tile. This runs inside `Task.Run(..., _lifetime.Token)`, but **`GenerateSet` never observes the token** — there's no `CancellationToken` parameter anywhere in `SampleImageGenerator`, so once started, the call cannot be interrupted; `_lifetime.Cancel()` in `OnClosed` only prevents the token from *starting* new work, it does not abort a running synchronous CPU loop. Combined with F-08 below, this can hang the process on close with no way out.

**Fix:** Add a sane upper bound (e.g. 512) alongside the existing tile-count cap, and/or thread a `CancellationToken` into `GenerateSet`/`GenerateAnnotations` with periodic `ThrowIfCancellationRequested()` checks in the per-tile loop.

---

### F-04 — `README.md` describes an application that no longer exists
**File:** `README.md:16-19`
**Confidence: 95%**

> "The demo starts with 100,000 deterministic spatial records, ingests 250 more every 500 ms, and publishes the hot buffer into a packed STR snapshot every two seconds. Drag to pan and use the mouse wheel to zoom."

Confirmed against the actual code: `MainWindow.xaml.cs` only declares two `DispatcherTimer`s (`_resizeTimer`, `_anchorPanTimer`, lines 26-27) — there is no periodic ingestion timer anywhere. `PublishSnapshotAsync` is called exactly once, inside `RegenerateSceneAsync` (line 135), not on a 2-second cadence. This description matches the *pre*-ADR-0002 point-cloud MVP (see `docs/handoffs/2026-07-23-render-coalescing.md`, which correctly describes that earlier state), but ADR-0002 pivoted the app to the static tile+annotation inspection scene. The README was never updated after the pivot. This is exactly the kind of onboarding trap that costs a new contributor real time.

**Fix:** Rewrite the "Run the MVP" section to describe the current tile/annotation inspection scene, regenerate flow, and side-panel controls.

---

### F-05 — `MainWindow.xaml.cs` is a God Class; core domain logic is trapped as private nested types
**File:** `MainWindow.xaml.cs` (entire file, esp. 817-885)
**Confidence: 90%**

One 885-line code-behind file owns: UI event wiring, scene generation orchestration, double-buffer bitmap lifecycle, WPF visual-tree construction for annotations (`BuildFrameVisual`), an anchor-pan input state machine, resize debouncing, pixelometer hit-testing, *and* two full strategy-pattern hierarchies defined as **private nested types**:

- `AnnotationDisplayOptions` (817-828), `AnnotationDisplayMode` (830-835) — pure presentation-rule data/logic
- `ISelectionOutlineAnimator` + `MarchingDashSelectionOutlineAnimator` + `PulseOpacitySelectionOutlineAnimator` + `SelectionOutlineAnimatorFactory` (837-884) — a legitimate Strategy pattern, correctly designed in isolation, but **unreachable by any test project** because it's `private` inside a `Window`.

This directly contradicts the project's own stated architecture — `DesignDoc.md` and `README.md` both describe "a strictly decoupled Model-View-ViewModel paradigm" — yet none of this logic lives in a ViewModel or a standalone class. Confirmed via `grep`: zero references to `AnnotationDisplayOptions`, `ISelectionOutlineAnimator`, or any selection-animator type outside this one file; zero test coverage of any of it.

**Fix:** Extract to `InfiniteCanvas.Rendering` (styling/animation strategy — no WPF dependency needed if animation is expressed as a small interface the View interprets) or `InfiniteCanvas.ViewModels` (display options as observable state). Split `MainWindow.xaml.cs` along its existing seams: a `SceneGenerationCoordinator`, a `FrameCompositor` (bitmap+visual construction), and an `InputController` (pan/zoom/anchor state machine), leaving the code-behind as thin event-forwarding glue.

---

### F-06 — Pixel-sampling and defect-blend logic is duplicated across four locations
**Files:** `SampleImageTile.cs:106-130` (`TryGetPixelValue`), `SampleImageTile.cs:193-217` (`SampleAnnotation.TryGetDefectValue`), `ZeroCopyBitmapFactory.Windows.cs:146-229` (`DrawTile`/`DrawDefectPatch`), `MainWindow.xaml.cs:812-815` vs. `ZeroCopyBitmapFactory.Windows.cs:231-234` (`BlendDefect` — identical private method in two classes)
**Confidence: 95%**

The world→local-pixel clamp math (`Math.Clamp((int)((world - origin) * pixelDim / boundsDim), 0, pixelDim - 1)`) is hand-written four separate times with different field names each time. Worse, `BlendDefect` is **byte-for-byte identical** in `MainWindow.xaml.cs:812-815` and `ZeroCopyBitmapFactory.Windows.cs:231-234`:

```csharp
private static byte BlendDefect(byte baseValue, byte defectValue)
    => (byte)Math.Clamp(baseValue - (defectValue / 2), byte.MinValue, byte.MaxValue);
```

This is Fowler's textbook Duplicated Code smell, and it's the dangerous kind: the two copies will silently diverge the next time someone "fixes" blending in only one place, producing a pixelometer readout that no longer matches what's actually rendered.

**Fix:** One shared internal static class in `InfiniteCanvas.Rendering` (e.g. `RasterSampling`) exposing `TryGetLocalPixel(...)` and `BlendDefect(...)`, consumed by `SampleImageTile`, `SampleAnnotation`, `ZeroCopyBitmapFactory`, and `MainWindow`.

---

### F-07 — Pixelometer hover bypasses the spatial index entirely; full O(n) scan on every mouse move
**File:** `MainWindow.xaml.cs:786-810` (`TryReadPixelValue`)
**Confidence: 85%**

```csharp
foreach (var tile in _tiles) {                     // up to 2000 iterations
    if (tile.TryGetPixelValue(worldX, worldY, out background)) {
        for (var index = 0; index < _annotations.Count; index++)   // ALL annotations, unindexed
            if (_annotations[index].TryGetDefectValue(worldX, worldY, out var value))
                defect = Math.Max(defect, value);
        ...
    }
}
```

This runs on **every** `MouseMove` event (potentially 60+ Hz). The annotation scan is a full linear sweep of `_annotations` — up to `2000 tiles × objectsPerTile` (no upper bound per F-03) items — even though `_spatialIndex` is a live, already-populated STR-tree built for exactly this kind of point/region lookup. This is a direct miss of the codebase's own core value proposition: the entire spatial-indexing subsystem exists to avoid O(n) scans, and the interactive hover feature doesn't use it.

**Fix:** Replace the annotation loop with `_spatialIndex.Query(new SpatialBounds(worldX, worldY, epsilon, epsilon))` (or add a dedicated point-query overload). The tile loop is cheaper (tiles aren't spatially indexed today) but at the 2000-tile ceiling is still worth bounding — either spatially index tiles too, or note it as acceptable given tile counts are orders of magnitude smaller than annotation counts.

---

### F-08 — Closing the window during in-flight regeneration risks `ObjectDisposedException` on a background continuation
**File:** `MainWindow.xaml.cs:104-154` (`RegenerateSceneAsync`), `749-761` (`OnClosed`)
**Confidence: 65%**

`OnClosed` cancels `_lifetime`, awaits `_renderAction.DisposeAsync()` (which *does* properly drain), then disposes `_generationGate` and `_lifetime` — but does **not** wait for an in-flight `RegenerateSceneAsync` to unwind. Per F-03, `GenerateSet` doesn't observe cancellation once running, so a large regenerate keeps executing after the window is closing. When it eventually completes, its `finally` block (`_generationGate.Release()`) runs against an already-disposed `SemaphoreSlim`, throwing `ObjectDisposedException` on a background continuation — and the code preceding it also touches `StatusText.Text` (a control on a window that may already be torn down). This requires a specific timing window (close during a multi-second regenerate) to reproduce, hence the moderate confidence, but it's directly inferable from the code with no mitigating logic found.

**Fix:** Track the in-flight regenerate `Task` and `await` it (with a timeout) in `OnClosed` before disposing `_generationGate`/`_lifetime`, or dispose `_generationGate` lazily only after that task completes.

---

## 3. Findings — Medium Severity

### F-09 — `GenerateSet`: passing `rows` silently makes `imageCount` a no-op
**File:** `SampleImageGenerator.cs:14-37`
**Confidence: 80%**

```csharp
var rowCount = rows ?? Math.Max(1, (int)Math.Ceiling(imageCount / (double)columns));
var tileCount = rows.HasValue ? checked(columns * rowCount) : imageCount;
```

When `rows` is supplied, `tileCount` is derived purely from `columns * rows` and `imageCount` is discarded — yet `imageCount` is still validated (`imageCount <= 0` throws) and still the parameter blamed in every exception in this method. Callers who pass both (as `MainWindow.RegenerateSceneAsync` does: `imageCount: tileCount, ... rows: _tileRows`) get away with it only because they've pre-computed `imageCount = columns * rows` themselves — a fragile, undocumented invariant. Anyone calling `GenerateSet` with inconsistent `imageCount`/`rows` values gets silently wrong tile counts with no warning.

**Fix:** Either drop `imageCount` when `rows` is provided (change the public signature) or validate `imageCount == columns * rows` and throw on mismatch.

---

### F-10 — Dead code: point-based `GenerateFrozenBitmap` overload + `Bgra32Color.OpaqueBlue`
**Files:** `ZeroCopyBitmapFactory.Windows.cs:60-107`, `Bgra32Color.cs:5`
**Confidence: 90%**

`grep` confirms the `IEnumerable<ScreenPoint>` overload of `GenerateFrozenBitmap` is called only from `tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs` and `benchmarks/.../ProjectionAndBitmapBenchmarks.Windows.cs` — **never from `src/InfiniteCanvas.App`**. This is the rendering path from the pre-ADR-0002 point-cloud MVP (see F-04); the shipped app exclusively uses the tiles+annotations overload (line 109). Keeping two parallel rendering pipelines alive — one only exercised by tests/benchmarks — is exactly the "legacy system" accumulation the project brief asks to avoid, and it means `ProjectionAndBitmapBenchmarks` (see F-18) is measuring a code path the product doesn't use.

**Fix:** Either delete the point-based overload and its benchmark/tests, or explicitly document it as a reusable public rendering primitive kept for future point-cloud use cases (if that's the intent, say so in a code comment/ADR — right now it reads as forgotten).

---

### F-11 — Dead code: `CanvasViewportViewModel.RefreshCommand` is unused in production and re-introduces the exact bug ICW-003 fixed
**Files:** `CanvasViewportViewModel.cs:41-56`, `MainWindow.xaml` (no `Command` binding anywhere — confirmed via grep)
**Confidence: 90%**

`RefreshAsync` re-queries `_spatialIndexService.Query(viewport)` from scratch to get a count — this is precisely the "duplicate spatial query per rendered frame" that ICW-003 (`docs/tasks/JIRA.md`) was completed to eliminate, by introducing `ApplyFrame(viewport, visibleItemCount)` which reuses the render pipeline's already-computed count. `ApplyFrame` is what `MainWindow.RenderFrameAsync` actually calls (line 206); `RefreshCommand` has no XAML `Command` binding and no code-behind caller — it exists solely to be exercised by `CanvasViewportViewModelTests.cs`. Also note lines 35-38 and 52-55 duplicate the identical `is LiveSpatialIndexService<T>` snapshot-timestamp check.

**Fix:** Delete `RefreshAsync`/`RefreshCommand` and its two tests, or wire it to an actual "Refresh" UI affordance if one is wanted; either way, extract the duplicated snapshot-timestamp check into one private helper first.

---

### F-12 — Speculative generality: `IRenderer<TScene, TOutput>` has zero implementations
**File:** `IRenderer.cs`
**Confidence: 95%**

```csharp
public interface IRenderer<in TScene, out TOutput> { TOutput Render(TScene scene, ViewportRenderRequest request); }
```

`grep` across the entire tree finds no class implementing this interface and no consumer referencing it anywhere. `ZeroCopyBitmapFactory` — the closest thing to a renderer — doesn't implement it and has an incompatible method shape (two different `GenerateFrozenBitmap` overloads, not `Render(TScene, ViewportRenderRequest)`). Same story for `ViewportRenderRequest`, which is only referenced by this interface.

**Fix:** Delete both, or if this is intentionally scaffolding for a near-term abstraction (e.g. to support a future GPU/Direct2D path per the DesignDoc's open question #3), say so explicitly in an ADR so it doesn't read as orphaned code to the next contributor.

---

### F-13 — `SampleAnnotation` is a `record` containing array/dictionary fields, breaking value-equality semantics
**File:** `SampleImageTile.cs:181-191`
**Confidence: 80%**

```csharp
public sealed record SampleAnnotation(..., IReadOnlyDictionary<string, double> Features, ..., byte[] DefectPixels) : ISpatialEntity
```

C# records advertise structural (value-based) `Equals`/`GetHashCode`. Arrays and (most) dictionary implementations don't override `Equals`, so the compiler-generated `SampleAnnotation.Equals` will use reference equality for `DefectPixels` and `Features` while doing genuine value comparison on the other members — a record that's "half value type, half reference type" for equality purposes. Currently nothing appears to rely on `SampleAnnotation` equality/hashing (no `HashSet<SampleAnnotation>`, no `.Distinct()`, no dictionary keying found), so this is latent rather than actively wrong today — but it's a trap for the next feature that assumes record semantics hold.

**Fix:** Either make `SampleAnnotation` a plain `sealed class` (honest about reference semantics) or keep it a record but suppress/override `Equals`/`GetHashCode` with an explicit comment explaining why identity (`Id`) is what matters, not structural equality.

---

### F-14 — Compound validation blocks blame the wrong parameter in `ArgumentOutOfRangeException`
**Files:** `SampleImageGenerator.cs:26-29, 97-100, 183-186, 227-230`, `SampleImageTile.cs:28-31`
**Confidence: 90%**

Recurring pattern:

```csharp
if (imageCount <= 0 || pixelWidth <= 0 || pixelHeight <= 0 || objectsPerTile < 0 || columns <= 0 || defectPoolSize <= 0)
    throw new ArgumentOutOfRangeException(nameof(imageCount));   // always blames imageCount
```

Five other conditions can trigger this throw, and every one of them gets reported as `imageCount` — actively misleading during debugging. Same shape recurs in `GenerateMonochromePixels` (blames `width` for a `height` violation), `ResampleTemplate` (blames `targetWidth` for `targetHeight`), `GenerateCenteredDefectPixels` (same), and `SampleImageTile`'s constructor (blames `pixelWidth` for a `pixelHeight` violation).

**Fix:** Validate each parameter with its own `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paramName)` call (already used correctly in `Bgra32BufferLayout.cs:7-8` — good existing precedent to copy from).

---

### F-15 — `double.Epsilon` misused as a floating-point comparison tolerance
**File:** `MainWindow.xaml.cs:617`
**Confidence: 85%**

```csharp
return Math.Abs(scaleXDelta - 1) > double.Epsilon || Math.Abs(scaleYDelta - 1) > double.Epsilon;
```

`double.Epsilon` (≈4.9×10⁻³²⁴) is the smallest representable positive `double`, not a "close enough" tolerance — a well-documented .NET pitfall (Microsoft's own `double.Epsilon` docs warn against using it this way). In practice this comparison behaves almost identically to `scaleXDelta != 1` exactly, since virtually any floating-point rounding noise exceeds `double.Epsilon`. It happens not to cause a visible bug today because the surrounding logic tolerates the "always true unless bit-identical" behavior, but it doesn't achieve its apparent intent (guarding against floating-point noise near a no-op zoom) and will mislead the next reader.

**Fix:** Use a real tolerance, e.g. `1e-9`, or better, compare `scaleXDelta` against `1.0` with `Math.Abs(...) > someExplicitTolerance` named as a constant.

---

### F-16 — Single-buffered `ZeroCopyBitmapFactory`; safety is entirely dependent on `MainWindow`'s external front/back swap
**Files:** `ZeroCopyBitmapFactory.Windows.cs:60-144`, `MainWindow.xaml.cs:218-250` (`AcquireBackBuffer`/`PublishFrame`)
**Confidence: 55%**

`ZeroCopyBitmapFactory` has exactly one unmanaged memory-mapped view per instance, mutated in place on every `GenerateFrozenBitmap` call. `MainWindow` correctly implements external double-buffering (`_frontBitmapFactory`/`_backBitmapFactory`, swapped in `PublishFrame`), which is the right mitigation — verified by tracing the swap logic, it does ensure the buffer being written is always one generation removed from what's currently assigned to `Image.Source`. The residual risk (why this isn't 0%) is that WPF's composition thread copies `InteropBitmap` pixel data to the GPU **asynchronously** relative to the dispatcher thread; nothing in this codebase synchronizes "compositor has finished reading buffer N" before buffer N is recycled as the next back buffer. In practice the dispatcher-thread-driven render cadence here is almost certainly slow enough (bounded by `Task.Run` work, not 60fps-tight) that this window is never hit, which is why confidence is only moderate — but it's worth an explicit note since any future perf work that shrinks frame latency reduces the safety margin.

**Fix:** No action needed unless visual tearing is ever observed; if it is, the fix is a third buffer slot (triple buffering) rather than architecture changes.

---

### F-17 — Annotation placement doesn't clamp to remain inside small tiles
**File:** `SampleImageGenerator.cs:127-129`
**Confidence: 60%**

```csharp
var size = random.Next(70, 201);
var localX = random.Next(0, Math.Max(1, (int)tileBounds.Width - size));
```

If a caller configures `pixelWidth`/`pixelHeight` smaller than ~200 (not reachable via the current UI, which only exposes tile *count*, not tile *pixel dimensions* — but `GenerateSet`'s `pixelWidth`/`pixelHeight` parameters are public API), `Math.Max(1, negative)` collapses the range to `[0,1)`, `localX`/`localY` become `0`, but `size` (70-200) is left unclamped and the resulting annotation bounds can extend past the tile's edge. No test exercises tile dimensions below ~200px, so this is latent.

**Fix:** Clamp `size` to `Math.Min(size, (int)tileBounds.Width)` (and same for height) before computing the placement range.

---

### F-18 — `ProjectionAndBitmapBenchmarks` measures a retired rendering path
**File:** `benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs`
**Confidence: 80%**

This benchmark exercises the point-based `GenerateFrozenBitmap` overload (F-10) at up to 100,000 points — a workload shape from the pre-ADR-0002 point-cloud demo. The shipped renderer now composes Gray8 tiles + sparse annotation patches (`DrawTile`/`DrawDefectPatch`), which is a completely different cost profile (per-pixel tile sampling vs. per-point scatter). This benchmark no longer tells you anything about the actual product's frame-time budget.

**Fix:** Add a benchmark that exercises `GenerateFrozenBitmap(tiles, annotations, camera)` at representative tile/annotation counts (this directly supports ICW-004's overdraw investigation, already on the backlog).

---

### F-19 — Scattered magic numbers with no named constants
**Files:** `MainWindow.xaml.cs:24,115,187-188,375-376,737` · `SampleImageGenerator.cs:40` (192×192 template size)
**Confidence: 90%**

- Camera scale bounds `(0.01, 50)` are duplicated verbatim at the field initializer (line 24) and inside `RegenerateSceneAsync` (line 115).
- The `4096` max-surface-dimension clamp appears twice, identically, in `RenderFrameAsync` (187-188) and `ClampCameraToScene` (375-376) — this is also the *exact* duplicated-clamp-block noted structurally below (F-20).
- The `2000` max-tile-count UI limit (line 737) has no named constant and no comment explaining the number's origin.
- `BuildDefectTemplatePool`'s `192, 192` template dimensions (`SampleImageGenerator.cs:40`) are hardcoded with no named constant.

**Fix:** Promote each to a `private const` (or, for the 4096 ceiling, the constant that ICW-005's DPI/max-surface policy work will need anyway — good opportunity to land it while doing that ticket).

---

## 4. Findings — Low Severity / Opportunities

### F-20 — Duplicated clamp-and-enforce block between `RenderFrameAsync` and `ClampCameraToScene`
**File:** `MainWindow.xaml.cs:187-190` vs. `375-378`
**Confidence: 85%**

Both methods independently compute `width`/`height` via the identical `Math.Clamp((int)Math.Ceiling(ViewportHost.ActualWidth), 1, 4096)` and call `EnforceZoomFloor` + `_camera.ClampToBounds`. `RenderFrameAsync` does not call `ClampCameraToScene()` even though the two blocks are otherwise identical — a missed reuse.

**Fix:** Extract a `(int Width, int Height) ClampViewportAndCamera()` helper used by both.

---

### F-21 — Per-pixel inverse-transform division in `DrawTile`/`DrawDefectPatch` hot loops
**File:** `ZeroCopyBitmapFactory.Windows.cs:161-183, 200-228`
**Confidence: 70% (real cost), ties to ICW-004**

Each visible screen pixel computes `(y - camera.OffsetY) / camera.ScaleY` and `(x - camera.OffsetX) / camera.ScaleX` independently — two divisions per pixel, repeated per tile/annotation that overlaps that pixel range. This is directly relevant to the already-open ICW-004 ("measure zoomed-out pixel overdraw") — when that spike happens, this loop is the natural place to switch from per-pixel division to incremental multiplication (precompute `1/scale` once, step by a fixed increment per column/row).

**Fix:** Defer to ICW-004; flagging here only so the implementer has the exact call sites.

---

### F-22 — Duplicated `LockBits`/`UnlockBits` unsafe boilerplate; unweighted grayscale conversion
**Files:** `SampleImageGenerator.cs:290-385` (`GenerateMonochromeBitmap`, `GenerateCenteredDefectBitmap`, `CreateTemplateFromBitmap`), `SampleImageTile.cs:144-177` (`ConvertBitmapToGray8`)
**Confidence: 90% (duplication), low severity**

Four separate methods repeat the same `LockBits`/pointer-walk/`UnlockBits` pattern. `ConvertBitmapToGray8` and `CreateTemplateFromBitmap` both convert RGB→gray via unweighted `(r+g+b)/3` rather than a perceptual weighting (e.g. Rec. 601 `0.299R+0.587G+0.114B`) — not a bug (these are synthetic monochrome images so the distinction barely matters), but worth a one-line comment if intentional.

**Fix:** Extract a shared `WithLockedBits(bitmap, mode, format, Action<IntPtr,int> body)` helper.

---

### F-23 — `UnmapViewOfFile` return value ignored
**File:** `ZeroCopyBitmapFactory.Windows.cs:248`
**Confidence: 90%, low impact**

```csharp
UnmapViewOfFile(_view);
```

Return value (`bool`) is discarded; a failure here (rare, but possible under resource exhaustion) is silently swallowed with no `Marshal.GetLastWin32Error()` capture, unlike the constructor's error handling for `CreateFileMapping`/`MapViewOfFile` which does check and throw. Minor inconsistency in an otherwise careful class.

**Fix:** Log (don't throw — this runs in `Dispose`) if the call fails.

---

### F-24 — Test coverage gaps
**Confidence: 95% (directly observable from `tests/` contents)**

No direct unit tests for:
- `SpatialBounds` (constructor validation, `Intersects`) — only exercised indirectly via `CameraTransformTests`/`StrTreeSpatialIndexServiceTests`.
- `ImmutableSpatialIndexService<T>` — only exercised transitively through `LinearSpatialIndexBuilder` in `LiveSpatialIndexServiceTests`.
- `CanvasViewportViewModel.ApplyFrame` — the method actually used in production; only the unused `RefreshCommand` (F-11) has tests.
- Everything in `MainWindow.xaml.cs`: `TryComputeZoomDeltas`, `ApplyDeadZone`, `ClampCameraToScene`, `EnforceZoomFloor`, both selection-outline animators, `AnnotationDisplayOptions`/`CreateFillBrush`. This is a direct consequence of F-05 (God Class) — none of this logic is reachable from a non-UI test project today.

**Fix:** Add a `SpatialBoundsTests.cs` and `ImmutableSpatialIndexServiceTests.cs` (cheap, self-contained). The `MainWindow.xaml.cs` gap should close naturally once F-05's extraction happens — prioritize that over trying to test the private nested types in place.

---

### F-25 — `Features` as `Dictionary<string, double>` is stringly-typed
**File:** `SampleImageGenerator.cs:148-152`, `SampleImageTile.cs:411-412` (`CreateAnnotationToolTip`)
**Confidence: 75%**

```csharp
new Dictionary<string, double> { ["Confidence"] = ..., ["Severity"] = ... }
...
var confidence = annotation.Features["Confidence"];   // MainWindow.xaml.cs:411, unchecked indexer
```

Two magic strings (`"Confidence"`, `"Severity"`) connect the producer and consumer with no compiler-checked contract; a typo in either location fails silently in the dictionary version (`KeyNotFoundException` at runtime, not a compile error) or "successfully" produces `0.0` if someone changes to `TryGetValue` carelessly later. Given there are exactly two fixed metrics today, this is Primitive Obsession — a small `readonly record struct AnnotationMetrics(double Confidence, double Severity)` would be equally simple and compiler-checked.

**Fix:** Low priority given only 2 call sites reference it today; worth doing before a third metric is added.

---

### F-26 — `objectId` has no collision detection
**File:** `SampleImageGenerator.cs:130`
**Confidence: 55%, cosmetic**

`random.NextInt64(0x100000000L, 0xFFFFFFFFFFFFL).ToString("X12")` — a 48-bit random ID with no uniqueness check across the ~48-bit space. At the demo's current scale (max 2000 tiles × unbounded objects, see F-03) collision probability is astronomically low (birthday bound on a 2⁴⁸ space), so this is purely a note, not an actionable risk today.

---

## 5. Assumptions & Open Questions

**Assumptions made during this audit:**
1. The `main` branch tarball fetched via the public repository archive at the given SHA is authoritative and matches what `git show <sha>` would return (the repository host REST API was rate-limited during this session, so commit metadata itself — author/date/message — was not independently re-verified; only the tree contents were, via the tarball).
2. "Production" means the `InfiniteCanvas.App` WPF executable specifically; findings about dead code (F-10, F-11, F-12) are scoped to that binary, not to the solution as a whole — those types remain reachable and tested elsewhere.
3. `DesignDoc.md`'s code samples (namespace `SpatialViz.*`, type `GeoPoint`) are explicitly aspirational/illustrative and were not compared line-for-line against the real `InfiniteCanvas.*` implementation — only its stated architectural principles (zero-copy, immutable STR-tree, MVVM, matrix clamping) were used as a conformance baseline, since the actual namespaces/types differ intentionally.
4. No `dotnet build`/`dotnet test` was executed in this sandbox (no .NET 10 SDK / Windows target available here); all findings are static-analysis-based. The repo's own handoff docs report a clean, passing build/test state as of the audited commit, which is taken at face value.

**Open questions for the maintainer:**
1. Is the point-based `GenerateFrozenBitmap` overload (F-10) intentionally kept as a reusable primitive for a future point-cloud mode, or genuinely forgotten? This determines whether F-10/F-18 are "delete" or "document intent" fixes.
2. Is `IRenderer<TScene,TOutput>` (F-12) intended scaffolding for the DesignDoc's open question #3 (potential DirectX/D3DImage pivot)? If yes, it should be referenced from an ADR so it doesn't look orphaned.
3. What's the intended behavior if a user closes the app mid-regenerate (F-08)? "Best-effort, may log a background exception" vs. "must gracefully cancel" changes the fix's shape (simple `await` vs. threading a token through `GenerateSet`).
4. Should `objectsPerTile`'s upper bound (F-03) scale with `columns × rows` (i.e., cap total annotation count, not per-tile count), given the eager (non-lazy) annotation generation?

## 6. Confidence Methodology

Confidence values reflect: (a) how directly the finding is confirmed by the retrieved source (grep verification of call sites counts as high confidence; behavioral claims about timing/races that depend on unobserved runtime conditions are capped lower), and (b) whether a counter-explanation was actively searched for and ruled out (e.g., F-16's moderate confidence explicitly reflects that `MainWindow`'s double-buffering *does* mitigate the naive single-buffer tearing concern — full credit was given to that mitigation rather than flagging the naive version of the issue at high confidence).
