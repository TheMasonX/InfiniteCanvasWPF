# InfiniteCanvasWPF Deep Audit Follow-up
**Report ID:** ICW-AUDIT-20260725-01  
**Repo:** TheMasonX/InfiniteCanvasWPF  
**Latest commit reviewed:** `52a3442d98a47d88df345f2cec9f24b08fbecb67`  
**Base commit for delta review:** `7524b88414858b4dc5113367cb964dab0e3193a1`  
**Date:** 2026-07-25

## Executive summary

The newest commit is mostly a follow-up sweep across task docs, README text, and a few source paths, but it also introduces a partially integrated viewport-scrollbar slice in `MainWindow.xaml.cs`. That slice appears unfinished: the code references scrollbar-related fields and methods, yet the inspected XAML contains no corresponding controls, and the render path never calls the new update method. That makes this the highest-risk regression in the latest push.

The other high-confidence issues are not new, but the latest commit does not resolve them: the defect pixelometer still disagrees with the renderer’s defect overlay contract, the cache reset path can be repopulated by in-flight generation, and the camera/bounds math still contains an inclusive-vs-half-open boundary mismatch. Several of these are already tracked in ICW tickets, but a few are not yet captured cleanly.

GitHub shows no workflow runs and no combined status checks for the latest commit, so there is no CI evidence on the head revision from the repository metadata I could inspect.

## What changed in the latest commit

The head commit adds or updates:
- task-tracker guidance in `.github/agents/infinitecanvas.agent.md`
- README and requirements text
- ICW ticket updates, including new proposed items `ICW-070` and `ICW-071`
- `MainWindow.xaml.cs` render/input logic
- `TileGridIndexLookup.cs`
- `SampleImageGenerator.cs`
- `SampleImageTile.cs`
- `ZeroCopyBitmapFactory.Windows.cs`
- `CanvasViewportViewModel.cs`
- regression tests for generation semantics and grid lookup

The source changes are mixed quality: there is real cleanup in the test surface and generation validation, but the new scrollbar work is not yet wired cleanly.

## Highest-priority findings

### 1) Scrollbar slice is orphaned / likely uncompilable
**Severity:** P0  
**Confidence:** 92%  
**Status:** Untracked as a code issue; related conceptually to ICW-070

**Evidence**
`MainWindow.xaml.cs` now contains `UpdateScrollbar`, `OnScrollbarTrackMouseLeftButtonDown`, `OnScrollbarThumbMouseLeftButtonDown`, `OnScrollbarThumbMouseMove`, `PanToScrollbarPositionAsync`, and `UpdateViewportScrollbars` in the later half of the file. The top-level field list in the same class does not declare the scrollbar fields those methods use, and the inspected `MainWindow.xaml` contains no scrollbar elements or matching named controls. The render path also stops after `UpdateZoomDisplay`, cache updates, and pixelometer refresh; it never calls `UpdateViewportScrollbars`.  
Sources: `src/InfiniteCanvas.App/MainWindow.xaml.cs` (top fields; render path; scrollbar methods), `src/InfiniteCanvas.App/MainWindow.xaml`.

**Why it matters**
This looks like a half-applied feature slice. Best case, the code is dead. Worst case, the project no longer builds because the partial XAML-generated names do not exist. Even if it compiles through hidden/generated members, the behavior is still inert because nothing invokes the update path.

**Recommendation**
Either finish the feature end-to-end in one slice or remove the dead code now. Add the scrollbar controls, field wiring, and an explicit `UpdateViewportScrollbars(camera, viewportWidth, viewportHeight)` call in the frame-update path; otherwise delete the methods and keep ICW-070 as a design ticket only.

---

### 2) Defect pixelometer still disagrees with the renderer contract
**Severity:** P1  
**Confidence:** 93%  
**Status:** Already tracked by ICW-035

**Evidence**
The pixelometer path blends using `BlendDefect(baseValue, defectValue)` in `MainWindow.xaml.cs`, which subtracts half of the defect value from the background sample. The renderer path in `ZeroCopyBitmapFactory.Windows.cs` does not do that: `DrawDefectPatch` writes grayscale defect pixels directly into the frame buffer.  
Sources: `src/InfiniteCanvas.App/MainWindow.xaml.cs` (`TryReadPixelValue`, `BlendDefect`), `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs` (`DrawDefectPatch`).

**Why it matters**
The pixelometer is telling the user a different story than the actual image they are seeing. That makes inspection output less trustworthy and can undermine the whole demo.

**Recommendation**
Extract one shared “defect sample / blend” helper and use it from both render and pixelometer code. If the renderer should show the defect grayscale directly, then the pixelometer should report the same grayscale result; if not, update the renderer to match the intended contract. Keep this tied to ICW-035 rather than creating a duplicate task.

---

### 3) Cache reset is not fenced against in-flight generation
**Severity:** P1  
**Confidence:** 84%  
**Status:** Not cleanly tracked yet; adjacent to ICW-021 / ICW-064

**Evidence**
`SampleImageTile.ResetImageCache()` clears `_pixels`, `_generationQueued`, and (on Windows) `_backgroundFetched`, but it does not cancel or fence any `Task.Run` generation that is already in flight. The background generation path later assigns `_pixels` and raises `PixelsGenerated` if it finishes after the reset.  
Sources: `src/InfiniteCanvas.Rendering/SampleImageTile.cs` (`ResetImageCache`, `EnsurePixelsGenerationStarted`).

**Why it matters**
The “Reset Cache” UI is not a hard reset. A tile can repopulate itself after the user has explicitly cleared it, which makes debugging and cache diagnostics nondeterministic.

**Recommendation**
Make reset semantics explicit. The minimal fix is to add a generation epoch/version token or cancellation gate so stale work cannot publish into a cleared tile. If the intended behavior is “best effort reset,” document that clearly and do not present it as a strict reset.

---

### 4) `imageCount` is not an exact count when `rows` is omitted
**Severity:** P2  
**Confidence:** 81%  
**Status:** Partially related to ICW-015, but still open semantically

**Evidence**
`SampleImageGenerator.GenerateSet` calculates `rowCount` as `Math.Ceiling(imageCount / (double)columns)` when `rows` is null, then returns `columns * rowCount` tiles. That means `imageCount` is rounded up to a full grid when it is not evenly divisible by `columns`.  
Source: `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs`.

**Why it matters**
The API name implies an exact item count, but the implementation uses it as a lower bound in the default layout path. That is easy to misread, easy to misuse, and a source of subtle test assumptions.

**Recommendation**
Decide whether `imageCount` is a strict count or a minimum-to-fill-grid hint. If it is a strict count, change the layout logic. If it is a minimum, rename the parameter and document the rounding behavior. Add a regression test for a non-divisible input such as `imageCount: 3, columns: 2`.

---

### 5) Spatial boundary semantics are still inconsistent
**Severity:** P1  
**Confidence:** 88%  
**Status:** Already tracked by ICW-033

**Evidence**
`SpatialBounds.Intersects` is inclusive on both ends (`<=` / `>=`). `TileGridIndexLookup.TryGetTileIndex` treats the right and bottom edges as exclusive (`worldX >= sceneBounds.Right`, `worldY >= sceneBounds.Bottom` returns false).  
Sources: `src/InfiniteCanvas.Core/SpatialBounds.cs`, `src/InfiniteCanvas.Core/TileGridIndexLookup.cs`.

**Why it matters**
Touching-edge cases can appear or disappear depending on which helper is used. That is a classic source of off-by-one behavior, duplicate edge hits, and subtle selection inconsistencies.

**Recommendation**
Pick one boundary policy and enforce it everywhere. Half-open bounds are usually the cleaner choice for grid math; if so, update `SpatialBounds.Intersects` and the downstream query/sampling code to match. Keep this attached to ICW-033 instead of creating a parallel fix.

---

### 6) `CoalescingAsyncAction` still swallows faults from the fault handler
**Severity:** P2  
**Confidence:** 86%  
**Status:** Existing gap, not yet isolated as a ticket

**Evidence**
`CoalescingAsyncAction.ReportActionFault` catches and ignores every exception thrown by `_onActionFault`. That means the mechanism used to surface a render fault can itself fail silently.  
Source: `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs`.

**Why it matters**
When render fault reporting breaks, the failure disappears completely instead of degrading visibly. That is the opposite of what you want in a fragile UI/render loop.

**Recommendation**
At minimum, log the secondary failure. Better: let the fault handler fail in a controlled way that is visible to the same logging pipeline used by the render owner. Do not silently ignore the callback failure.

---

### 7) Frame-shell churn remains, and the new scrollbar slice makes it worse if completed as-is
**Severity:** P2  
**Confidence:** 87%  
**Status:** Already tracked by ICW-028 / ICW-007 / ICW-019

**Evidence**
`RenderFrameAsync` still rebuilds a fresh frame visual per render (`BuildFrameVisual(...)`, then `PublishFrame(...)`). The updated commit adds more UI concerns around the same render path rather than reducing churn.  
Sources: `src/InfiniteCanvas.App/MainWindow.xaml.cs` (`RenderFrameAsync`, `BuildFrameVisual`, `PublishFrame`).

**Why it matters**
This is exactly the kind of per-frame allocation pattern that becomes visible only after the scene gets larger or the interaction rate increases. It also makes selection animation and overlays harder to preserve cleanly.

**Recommendation**
Treat the frame shell as a persistent object and update its child layers in place. Keep the overlay pooling and selection continuity work bundled with this, not as separate micro-slices.

---

### 8) Backlog / requirements text is drifting around ICW-064
**Severity:** P3  
**Confidence:** 79%  
**Status:** Documentation drift, not code breakage

**Evidence**
The requirements registry and ICW-064 ticket describe the cache admission ceiling as `4 GiB` of Gray8 tile bytes. `docs/tasks/task-tracker.md` still contains a later note describing the cache as defaulting to `32 full Gray8 tiles`, which is a different capacity framing.  
Sources: `docs/requirements/functional-requirements-and-invariants.md`, `docs/tasks/tickets/ICW-064-tile-cache-capacity-and-materialization-metrics.md`, `docs/tasks/task-tracker.md`.

**Why it matters**
Future work will be easier to misread if the backlog and contract docs disagree on the same sizing policy. That kind of mismatch tends to create duplicate investigation and accidental “fixes” to the wrong number.

**Recommendation**
Normalize the cache policy wording in one place and have the backlog point to that source of truth. If the budget is byte-based, keep every other document in byte-based language.

## Implementation guidance, ordered by ROI

1. Finish or remove the scrollbar slice immediately. This is the only thing in the latest commit that looks like it can break the build or leave the app in a half-wired state.
2. Fix the defect blend contract next. That is user-visible, easy to verify, and directly affects trust in the pixelometer.
3. Fence cache reset against in-flight generation so debugging is deterministic.
4. Resolve the `imageCount` semantics before more generation scenarios rely on it.
5. Collapse the boundary policy mismatch into one shared rule and one set of tests.

## Positive changes worth keeping

- `ICW-017` is genuinely cleaner now: the dead `RefreshCommand` path is gone and `ApplyFrame` is the canonical view-model update path.
- The new `TileGridIndexLookupTests` and `SampleImageGeneratorTests` are useful regression coverage and should be kept.
- The ICW task tracker now reflects more of the active architectural work, which should reduce duplicate effort if it stays synchronized.

## Assumptions

- I reviewed the current `main` branch as the latest available head.
- I did not find any GitHub Actions workflow runs or combined status checks for the latest commit.
- I treated the inspected repository files as authoritative; I did not assume missing generated code or hidden partial classes unless the source made that unavoidable.
- I did not run a local build in this environment, so compile-break risk is inferred from source evidence rather than build output.

## Open questions

- Is the scrollbar work intended to be landed now, or is it a partial pre-implementation slice?
- Should cache reset be a strict reset, or merely a best-effort visual refresh?
- Is `imageCount` supposed to be an exact count, or a minimum that rounds up to full rows?
- Which defect appearance is canonical: renderer grayscale overwrite or pixelometer subtractive blending?
- Should the cache-budget docs and task log be normalized to byte-capacity language everywhere?

