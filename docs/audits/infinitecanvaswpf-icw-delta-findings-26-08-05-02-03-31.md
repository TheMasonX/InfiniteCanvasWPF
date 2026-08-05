# InfiniteCanvasWPF — Delta Report: `ICW-312` Landed — Two Prior Findings Confirmed Fixed, One New Design Question in the Fresh Code

**Previous reports:** nineteen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip. Real, substantial work landed: `ICW-312` (canvas data-source abstraction) and `ICW-315` (`CanvasFrame` render-pipeline boundary) are both Done. New Core contracts (`ICanvasItem`, `ICanvasSceneSource`, `ICanvasSpatialQuerySource`, `CanvasPixelSample`) and `CanvasFrame.cs` landed alongside changes to `CanvasControl.xaml.cs`, `CanvasViewModel.cs`, `MainWindow.xaml.cs`, and `SampleImageTile.cs`.

---

## 1. Confirmed: two findings from this series were fixed correctly in this exact batch

**Report 18's dead `InfiniteCanvas.Spatial` project reference** — verified fixed by directly reading the current `.csproj`: the `<ProjectReference Include="..\InfiniteCanvas.Spatial\..." />` line is gone, leaving only `InfiniteCanvas.Core`. Matches the handoff's claim exactly.

**Report 5/16's pixelometer "must never initiate tile acquisition" violation** — verified fixed by reading `SampleImageTile.TryGetResidentPixels` directly: it reads `TryGetNativePixels`, the `_mipPixels` dictionary under `_cacheGate`, and falls back to `TryGetBestResidentMip` — no call to `EnsurePixelsGenerationStarted` anywhere in the method. This is a real, structural fix (a new read-only method, not a guard added to the old one), and the handoff's own description matches what I found in the code: *"hover no longer initiates tile generation."* This closes a finding that traced back through reports 5, 9 (the ADR-0005 cross-reference), and 16 across several sessions — good to see it land as a clean, purpose-built fix rather than a patch on the old path.

**Confidence:** 95% for both (both read directly against the exact claim being verified).

---

## 2. New finding: `ICanvasSceneSource` and `ICanvasSpatialQuerySource` declare an identical `QueryVisible(SpatialBounds)` method, and neither is called by `CanvasControl` yet — worth resolving before the pattern hardens further

**Checked directly, not assumed.** `ICanvasSceneSource.QueryVisible(SpatialBounds viewport)` and `ICanvasSpatialQuerySource.QueryVisible(SpatialBounds viewport)` are the exact same signature on two different interfaces. `MainWindow` implements both on the same class (`ICanvasSceneSource, ICanvasSpatialQuerySource`) and its own code has a comment acknowledging the overlap directly: *"ICanvasSpatialQuerySource expose the same QueryVisible signature."* `CanvasControl` declares both as separate dependency properties (`SceneSource`, `SpatialQuerySource`) and `MainWindow` wires both (`CanvasSurface.SceneSource = this; CanvasSurface.SpatialQuerySource = this;`).

**I checked whether `CanvasControl` actually calls either one — it calls neither.** A grep for member access on both properties (`SceneSource.` and `SpatialQuerySource.`) inside `CanvasControl.xaml.cs` returns zero hits for both. This isn't "one property is used, the other is dead" (the pattern this series has found three times already in this new code) — it's earlier-stage than that: **both properties are fully wired end-to-end (declared, set, assigned) but neither is consumed by anything yet.** That's consistent with `ICanvasItem`'s own doc comment stating *"ICW-314 extends it with interaction members"* — the actual hit-testing/selection consumption is explicitly deferred to a later ticket, so this may simply be plumbing laid ahead of the feature that will use it, which is a reasonable incremental-development choice on its own.

**What's still worth flagging now, before more code is built on top of it:** the *only* existing test referencing `SpatialQuerySource` — `CanvasBoundaryZeroReferenceTests.CanvasControl_ExposesSceneSourceAndSpatialQuerySourceDependencyProperties` — is a **string-content scan of the source file's text** (`Assert.That(source, Does.Contain("SpatialQuerySourceProperty"), ...)`), not a behavioral test that exercises the property being read and returning real data. A test like this would pass unchanged whether or not `SpatialQuerySource` is ever actually consulted for anything — it verifies the property's textual existence, not its function. Since nothing distinguishes `SceneSource` and `SpatialQuerySource`'s actual behavior today (identical signature, same underlying implementation class, neither called), there's a real open question worth answering *before* `ICW-314` builds hit-testing on top of one or both of them: is the split intentional (e.g., so a future consumer could someday supply spatial querying from a different object than scene metadata, an ADR-0007-aligned decoupling goal not yet realized) or is it accidental duplication that should collapse to one property before more code depends on the split existing? Either answer is fine — but resolving it now is cheaper than discovering, mid-`ICW-314`, that two supposedly-independent properties are actually always the same object in every real usage and the split never served a purpose.

**Recommendation:** either (a) document explicitly (in `ICanvasSpatialQuerySource`'s own doc comment, which currently doesn't explain *why* it's separate from `ICanvasSceneSource` beyond "non-generic wrapper over the host's spatial index") the concrete scenario where the two would differ, or (b) collapse to one property if no such scenario is intended, before `ICW-314` starts consuming either one. Also worth adding one behavioral test (construct a `CanvasControl`, assign a fake `ICanvasSpatialQuerySource`, and assert `QueryVisible` is actually invoked when expected) rather than relying solely on the structural source-scan test to represent this boundary's coverage.

**Confidence:** 90% (every claim — the identical signatures, the dual implementation with its self-aware comment, the zero call sites in `CanvasControl`, and the structural-only nature of the one existing test — confirmed by direct reads; the "is this intentional or accidental" question is genuinely open and can't be resolved from the code alone).

---

## 3. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `InfiniteCanvas.ViewModels.csproj` dead `Spatial` reference | Fixed in this batch | **Confirmed correct** by direct read. | §1 |
| `ICW-P0-PIXELOMETER-READOUT` (hover-triggers-generation) | Fixed in this batch via `TryGetResidentPixels` | **Confirmed correct** by direct read — a genuine new read-only method, not a patched guard. | §1 |
| `ICanvasSceneSource`/`ICanvasSpatialQuerySource` | Both Done (`ICW-312`) | **New finding**: identical `QueryVisible` signature on two interfaces, both fully wired but neither consumed by `CanvasControl` yet, and the only test covering this is a textual source-scan rather than a behavioral check. Recommend resolving the intentional-split-vs-accidental-duplication question before `ICW-314` builds on top of it. | §2 |

---

## 4. Assumptions & Open Questions

- I did not read `CanvasFrame.cs`, the full diffs of `CanvasControl.xaml.cs`/`CanvasViewModel.cs`/`MainWindow.xaml.cs` for this batch, or `CanvasSceneSourceContractsTests.cs`/`CanvasBoundaryZeroReferenceTests.cs` beyond the one test method quoted above — all are strong candidates for a focused follow-up session, particularly `CanvasFrame.cs` given the handoff's description of it as the new frame-boundary value type (directly relevant to report 16 §3's `FramePresenter.Child` dual-path finding — worth checking whether that bypass was incidentally closed by this refactor, since `PublishFrame(UIElement)` was replaced by `PublishFrame(CanvasFrame)` entirely).
- §2's open question is presented as a question for the team, not a defect — I don't have enough information (no design doc or ADR section specifically justifies the split beyond the one-line doc comment) to say confidently which answer is correct.

---

*Methodology note: this session verified two specific claims from the new handoff document against source directly (the csproj fix and the pixelometer fix) rather than trusting the handoff's prose, then noticed the identical `QueryVisible` signature while reading the four new Core contract files and checked its actual consumption with a targeted grep — confirming, rather than assuming, that neither property is yet called anywhere in `CanvasControl` before writing up the finding.*
