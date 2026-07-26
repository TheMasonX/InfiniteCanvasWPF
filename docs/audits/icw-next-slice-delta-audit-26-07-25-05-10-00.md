---
title: ICW next-slice delta audit
audit_hash: be07c993f1
repo: TheMasonX/InfiniteCanvasWPF
commit_ref: d74dde2655b9cee1f6502e0200fca022ce1435dd
scope: Delta-only follow-up after the prior audit slice
checked_task_corpus: ICW-###
checked_duplicates: TSK-### not present in the repo task corpus
---

# Executive summary

This slice surfaced one high-confidence product gap and several contract drifts rather than broad structural churn.

The biggest issues are that the sparse-tile threshold exists in settings but is not wired into the main render path, the render/settings vocabulary has drifted enough that some options are persisted but never consumed, and the defect-overlay readout still depends on collection order even though the spatial backends do not guarantee the same ordering.

The cache diagnostics are still too coarse for the stated ICW-096 acceptance criteria, and the scrollbar wiring test is still a string-presence check rather than a behavior check. There is also avoidable hot-path duplication in the mip generator and defect overlay renderer.

# Findings

## 1) Sparse-tile threshold is inert in the app shell
**Confidence:** 98%

`CanvasUserSettings.MinimumSparseTilePixelSize` exists and is validated/persisted, but `MainWindow.RenderFrameAsync` still calls `ZeroCopyBitmapFactory.GenerateFrozenBitmap(...)` without passing a threshold, so the renderer falls back to the default `minimumSparseTilePixelSize = 0` path. The XAML also does not expose a control for this setting, despite ICW-074 claiming a runtime slider and persisted threshold.  
Evidence: `src/InfiniteCanvas.Core/CanvasUserSettings.cs:45-58`, `src/InfiniteCanvas.App/MainWindow.xaml:213-246`, `src/InfiniteCanvas.App/MainWindow.xaml.cs:56-68`, `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:111-118`, `docs/tasks/tickets/ICW-074-min-pixel-size-sparse-tiles.md:25-41`.

**Recommendation:** thread the persisted setting into the render call, add the missing UI control (or delete the dead setting), and re-run the ICW-074 validation path.

## 2) Settings contract drift / primitive obsession
**Confidence:** 97%

`CanvasUserSettings` still carries `ShowBoxes` and `ShowSparseImageTiles`, but the app does not read either field. Meanwhile, the visible `ShowImageTiles` checkbox is wired into the `showSparseImageTiles` parameter on the bitmap factory, so the persisted naming and runtime meaning have drifted apart. This is hard to reason about and makes the settings schema feel accidental rather than intentional.  
Evidence: `src/InfiniteCanvas.Core/CanvasUserSettings.cs:21-58`, `src/InfiniteCanvas.App/MainWindow.xaml:204-212`, `src/InfiniteCanvas.App/MainWindow.xaml.cs:94-103`, `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:111-143`, `tests/InfiniteCanvas.Tests/CanvasUserSettingsTests.cs:11-38`.

**Recommendation:** rename the settings to the actual runtime concepts, remove dead fields, and consider a small `DisplayOptions` value object to avoid this vocabulary drift.

## 3) Defect overlay output depends on enumeration order
**Confidence:** 91%

`DefectOverlaySampler.ResolveDisplayValue(IEnumerable<SampleAnnotation>)` keeps the last matching annotation value it sees. That means the visible defect value for overlapping annotations depends on the enumeration order returned by the spatial backend. `ImmutableSpatialIndexService` preserves input order, but `StrTreeSpatialIndexService` only promises the STRtree query result, not a stable overlay order. This is a hidden contract and can make the same scene render differently across implementations or refactors.  
Evidence: `src/InfiniteCanvas.Rendering/DefectOverlaySampler.cs:16-27`, `src/InfiniteCanvas.Spatial/ImmutableSpatialIndexService.cs:19-27`, `src/InfiniteCanvas.Spatial/StrTreeSpatialIndexService.cs:33-39`, `src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs:47-55`, `tests/InfiniteCanvas.Tests/SampleImageTileTests.cs:39-75`.

**Recommendation:** define an explicit overlay precedence rule (`z-index`, insertion order, priority, or nearest-center) and encode it in the sampler instead of inheriting backend enumeration order.

## 4) Cache diagnostics are still too shallow for the new ICW-096 contract
**Confidence:** 98%

`TileCacheBudget.DescribeStatus()` only reports bytes, resident tile count, and evictions. It does not identify the active cache instance, queued work, reserved tiles, variant/mip identity, or reset state. That is narrower than the ICW-096 acceptance criteria and narrower than the user-visible debugging contract now implied by the scrollbar/mip work.  
Evidence: `src/InfiniteCanvas.Rendering/SampleImageTile.cs:37-154`, `src/InfiniteCanvas.App/MainWindow.xaml.cs:27-33`, `docs/tasks/tickets/ICW-096-scrollbars-and-resident-mip-fallback.md:45-55`.

**Recommendation:** expose a richer diagnostics snapshot from the budget/cache layer, not just a formatted string, and include variant/mip identity plus queue/reservation counts.

## 5) Scrollbar wiring tests are presence checks, not behavior checks
**Confidence:** 95%

`CanvasScrollbarWiringTests` only asserts that the XAML/code-behind contains specific names and strings. That catches accidental deletions, but it will still pass if the overlay is present yet mispositioned, not hit-testable, or wired to the wrong geometry.  
Evidence: `tests/InfiniteCanvas.Tests/CanvasScrollbarWiringTests.cs:10-27`, `src/InfiniteCanvas.App/MainWindow.xaml:72-110`, `src/InfiniteCanvas.App/MainWindow.xaml.cs:30-73`.

**Recommendation:** add at least one behavior test for scrollbar geometry or render-state calculation, ideally at the policy level and with a lightweight window/visual assertion if feasible.

## 6) Hot-path duplication and unused work
**Confidence:** 94%

`SampleImageGenerator.GenerateMonochromeMipPixels` recalculates the previous mip dimensions inside the innermost loop and duplicates the box-filter logic already present in `ReduceGray8Box`. Separately, `ZeroCopyBitmapFactory.DrawDefectPatch` locks the Windows `Bitmap`, reads a pixel byte into `value`, and then never uses that byte at all because the actual display value comes from `DefectOverlaySampler`. Both are avoidable work on hot paths.  
Evidence: `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:155-226`, `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:8-41`, `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:221-274`.

**Recommendation:** pull the repeated dimension lookup out of the inner loop, route the mip reduction through the shared helper, and either remove the unused bitmap read or make it the actual sampled source.

## 7) View-model is coupled to one concrete spatial service
**Confidence:** 93%

`CanvasViewportViewModel.ApplyFrame()` special-cases `LiveSpatialIndexService<T>` to pull `LastSnapshotPublishedAtUtc`. That bakes a concrete implementation into a view-model that otherwise depends on `ISpatialIndexService<T>`, which makes future spatial backends harder to introduce cleanly.  
Evidence: `src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs:9-40`, `src/InfiniteCanvas.Spatial/ISpatialIndexService.cs:7-12`, `src/InfiniteCanvas.Spatial/LiveSpatialIndexService.cs:29-30`.

**Recommendation:** move the published-at timestamp behind an interface or a dedicated read-only contract if the UI truly needs it.

# Corrections / task impact

- **ICW-074** still needs a wiring correction: the setting exists, but the main window never passes it to the renderer.
- **ICW-035** should explicitly define overlay precedence, because the current implementation is order-sensitive.
- **ICW-096** should broaden diagnostics from a formatted string to an actual cache state snapshot.
- **ICW-037 / scrollbar work** should add at least one behavior-level assertion instead of relying on file-content checks.
- **ICW-076 / ICW-097** still have a couple of hot-path consolidation opportunities in the mip generator and defect overlay renderer.
- **ICW-089** has a clean follow-up path if the repo wants to reduce the concrete-type leak from the view-model.

# Assumptions and open questions

- I assumed the ICW task corpus is authoritative for de-duplication because no `TSK-###` task files were present in the repo.
- I treated the sparse-tile setting as intended to be user-visible because the persisted setting and ticket metadata both imply that, but the current app UI does not expose it.
- I did not classify the pixelometer as a hard bug here because ICW-076 explicitly keeps pixelometer sampling mip-zero and non-blocking for now; the remaining question is whether the UI wording should be clarified to avoid implying a true mip-aware sample.
- I did not convert the scrollbar wiring test gap into a bug ticket because it is already covered by the scrollbar-related workstream; it is a quality/correction note for that task family.
