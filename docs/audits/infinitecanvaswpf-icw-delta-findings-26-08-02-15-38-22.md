# InfiniteCanvasWPF — Delta Report: An Invisible Parallel ViewModel and a False README Claim

**Previous reports:** nine prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round reads `README.md` (not previously read) and, on noticing a discrepancy with what it describes, traces `CanvasViewportViewModel<T>`'s actual production wiring for the first time.

---

## 1. Finding: `CanvasViewportViewModel<SampleAnnotation>` runs on every frame in production but has zero UI consumers — a fully-executing, fully-invisible parallel ViewModel

**This is the most significant architectural finding in this report series to date.** README's "Implemented design pillars > MVVM" section documents `CanvasViewportViewModel<T>` as the project's shipped MVVM story. I traced its actual wiring in `MainWindow.xaml.cs`:

- Line 109: `_viewModel = new CanvasViewportViewModel<SampleAnnotation>(_spatialIndex);` — constructed with the real, live spatial index.
- Line 111 (two lines later): `DataContext = _mainViewModel;` — a **different** object (`MainViewModel`) is what's actually bound to the window.
- Line 429, inside the per-frame render completion path: `_viewModel.ApplyFrame(viewport, frame.VisibleItems.Count);` — genuinely called every single rendered frame, with real data.
- Line 430, the very next line: `_mainViewModel.ApplyViewportState(frame.VisibleItems.Count, _spatialIndex.Count);` — a **second, separate call**, on the object that's actually bound, doing overlapping work (visible/total item counts) through an entirely different code path.

`CanvasViewportViewModel<T>`'s complete public surface is four `[ObservableProperty]` fields (`Viewport`, `VisibleItemCount`, `TotalItemCount`, `LastSnapshotPublishedAtUtc`) and one method (`ApplyFrame`). A repo-wide grep confirms `CanvasViewportViewModel` is referenced in exactly two places: `MainWindow.xaml.cs` (the construction and the one `ApplyFrame` call above — never read from again) and its own dedicated unit test file. **None of its four observable properties are bound in `MainWindow.xaml`, read by any other code, or displayed anywhere.** Every `PropertyChanged` notification `CanvasViewportViewModel<T>` raises — including the `LastSnapshotPublishedAtUtc` update via its `is LiveSpatialIndexService<T>` downcast — fires into a void. The object is fully wired to real data, does real work every frame (including a spatial-index type-check), and produces output that literally nothing in the running application ever looks at.

**This directly recalibrates my fourth report's finding about that same downcast** (`ISpatialIndexService<T>`'s shallow interface forcing `CanvasViewportViewModel<T>` to check `is LiveSpatialIndexService<T>`). That finding's code-level accuracy is unaffected — the downcast is real, it runs, and it would still silently produce `null` for a different `ISpatialIndexService<T>` implementation. But its **practical, user-visible severity today is zero**, because nothing consumes `LastSnapshotPublishedAtUtc` regardless of whether the downcast succeeds or fails. The interface-shape problem is real and worth fixing on its own architectural merits (per ADR-0003, as report 8 established) — it just isn't currently causing anyone to see a wrong value on screen, because no screen shows that value at all.

**The higher-priority problem this reveals is the redundant parallel ViewModel itself.** The application maintains two objects tracking overlapping "visible/total item count" state through two different mechanisms (`CanvasViewportViewModel<T>.ApplyFrame` vs. `MainViewModel.ApplyViewportState`), updates both every frame, and only one is ever actually displayed. This is exactly the kind of pattern the project's own `.github/agents/infinitecanvas.agent.md` code-smell checklist names: **Duplicated Code** (two objects computing the same counts from the same frame data), and arguably **Speculative Generality** (a fully-general, source-generator-backed, ADR-0003-compliant-boundary-respecting ViewModel built and wired in, for a UI surface that never asked for it).

**Recommendation:** pick one of two directions, don't leave both running:
1. **Delete `_mainViewModel.ApplyViewportState`'s redundant counting and bind the XAML directly to `_viewModel`** (i.e., actually make `CanvasViewportViewModel<T>` the real `DataContext`, or a nested bindable property of it) — this is more work but actually realizes the README's documented architecture.
2. **Delete `_viewModel` and its construction/`ApplyFrame` call entirely**, since `MainViewModel.ApplyViewportState` already does the job that's actually displayed — simpler, and honest about what the app actually is today.

Either way, update README's MVVM section to describe what's actually true after the fix, since right now it describes a component (`CanvasViewportViewModel<T>`) as if it were the operative ViewModel when it demonstrably is not the one driving the UI.

**Confidence:** 95% (every claim directly confirmed: exact line numbers for both `ApplyFrame`/`ApplyViewportState` calls, the `DataContext` assignment, the complete member list of `CanvasViewportViewModel<T>`, and the two-hit repo-wide grep for all its consumers).

---

## 2. Correction: README's "Implemented design pillars > MVVM" section claims a capability that exists nowhere in the codebase

While tracing §1, I checked README's specific claim: *"Its refresh command is asynchronous, runs spatial queries away from the caller thread, exposes `IsRunning` through `IAsyncRelayCommand`..."* Neither `CanvasViewportViewModel<T>` nor `MainViewModel` (nor, per a repo-wide grep, **any class in the entire solution**) uses `[RelayCommand]` or `IAsyncRelayCommand` anywhere. **This capability does not exist in the shipped codebase.** The actual async rendering orchestration in production runs through `CoalescingAsyncAction` — a hand-rolled, custom-built coalescing-request class (examined in full in the first report of this series) that has nothing to do with `CommunityToolkit.Mvvm`'s relay-command infrastructure.

This isn't a subtle interpretation gap — README states a specific, checkable technical fact (`IAsyncRelayCommand` usage) that a one-line grep disproves. It's possible this describes an earlier implementation that was later replaced by `CoalescingAsyncAction` without the README being updated, or describes aspirational/planned work that was never labeled as such. Either way, a new contributor reading README's "Implemented design pillars" section — which reads as a statement of present fact, not a roadmap — would look for `IAsyncRelayCommand` usage that isn't there.

**Recommendation:** update README's MVVM section to describe `CoalescingAsyncAction` and the actual `RenderFrameAsync`/`RequestRenderAsync` pipeline that really drives async rendering, rather than a `CommunityToolkit.Mvvm` command pattern that was never built (or was removed without a corresponding doc update).

**Confidence:** 95% (repo-wide grep for both exact identifiers returned zero matches; README's claim quoted verbatim).

---

## 3. Corrections Summary Table

| Ticket / Doc / Prior Report | Current status/claim | Correction | Basis |
|---|---|---|---|
| *(new, no existing ticket found)* | `CanvasViewportViewModel<SampleAnnotation>` instantiated and updated every frame in production | **New finding**: its output has zero consumers anywhere in the running app — it duplicates `MainViewModel.ApplyViewportState`'s job through a separate, unbound object graph. Recommend picking one ViewModel to be authoritative and deleting the other's redundant path. | §1 |
| My 4th report (`ISpatialIndexService<T>` shallow-interface finding) | Framed as an architectural violation with a real (if unstated) severity | **Recalibrate**: the code and the ADR-0003 violation are both still real, but current practical/user-visible impact is zero, since the property this downcast populates (`LastSnapshotPublishedAtUtc`) is never displayed or read anywhere — the fix is still worth doing on architectural grounds, just not because a user would currently notice a wrong value. | §1 |
| `README.md` "Implemented design pillars > MVVM" | States `IAsyncRelayCommand`/`RefreshCommand`/`IsRunning` are implemented | **Correction**: zero matches for `RelayCommand`/`IAsyncRelayCommand` anywhere in the solution. This capability doesn't exist; the real async pipeline is `CoalescingAsyncAction`. README should describe what's actually shipped. | §2 |

---

## 4. Assumptions & Open Questions

- I did not check git history (no `.git` directory available in the tarball retrieval method used throughout this series) to determine whether `CanvasViewportViewModel<T>`'s `ApplyFrame` call was ever actually bound to the UI in an earlier version and later orphaned when `MainViewModel` was introduced, versus having been built already-parallel from the start. Either history is consistent with the current evidence; it doesn't change the recommended fix.
- §1's two proposed directions (make `_viewModel` authoritative vs. delete it) are presented as options, not a recommendation between them — that's a product/architecture decision (does the team want the ADR-0003-compliant, source-generator-based ViewModel to be the real one, or is `MainViewModel`'s simpler, direct approach the intended long-term shape?) that this audit series isn't positioned to make unilaterally.
- Open question: given README made one specific, checkable, false claim in this session, does the same "Implemented design pillars" section warrant a full line-by-line verification pass the way `ICW-088`/`ICW-188`/`ICW-189` and the ADRs already received in earlier sessions? The spatial-indexing and rendering sections were spot-checked opportunistically across this series and found broadly consistent with code; only the MVVM section's `IAsyncRelayCommand` claim was found false, but a dedicated pass wasn't performed.

---

*Methodology note: this session read `README.md` in full for the first time in this series. Its "Implemented design pillars > MVVM" paragraph named `CanvasViewportViewModel<T>` as the shipped async-refresh ViewModel; checking that claim against the class's actual production wiring (rather than assuming the README was accurate) surfaced this session's primary finding, and checking the specific `IAsyncRelayCommand` claim via repo-wide grep surfaced the second.*
