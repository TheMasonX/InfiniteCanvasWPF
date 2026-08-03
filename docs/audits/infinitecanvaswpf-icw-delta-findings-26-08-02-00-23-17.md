# InfiniteCanvasWPF — Delta Report: Benchmark-Suite Verification & Ticket Cross-Reference Correction

**Previous reports:** five prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round examines the `benchmarks/InfiniteCanvas.Benchmarks/` project, not previously read.

---

## 1. Finding: `ProjectionAndBitmapBenchmarks` measures a code path with zero production usage — confirms and adds concrete detail to `ICW-133`'s own stated acceptance criterion (not a new discovery, but worth precisely verifying)

**In fairness to the project, this gap is already known** — `ICW-133-rendering-benchmark-matrix-and-baselines.md` (status: **To Do**) lists as an explicit acceptance criterion: *"Benchmark coverage includes a realistic shipped tile path rather than only the legacy point overload."* So this report's contribution isn't discovering the gap — it's independently verifying, via direct code read rather than trusting the ticket text, exactly how real the gap is and exactly what's missing, which is useful for scoping the eventual fix precisely.

**Verified:** `ZeroCopyBitmapFactory` has two `GenerateFrozenBitmap` overloads:
1. `GenerateFrozenBitmap(IEnumerable<ScreenPoint> screenPoints, Bgra32Color? color = null)` — a simple "plot colored dots" method with no tile/mip/GDI+ involvement.
2. `GenerateFrozenBitmap(IReadOnlyList<SampleImageTile> tiles, IReadOnlyList<SampleAnnotation> annotations, CameraSnapshot camera, Func<SampleImageTile,bool>? tryReserveCacheEntry, double minimumSparseTilePixelSize, bool showBackgroundImages, bool showSparseImageTiles)` — the real per-frame compositor: mip selection via `BackgroundTileMipPolicy`, world-to-screen tile mapping, on-demand generation triggering through `tryReserveCacheEntry`, and defect-patch overlay compositing via `DrawDefectPatch`.

A repo-wide grep of every `GenerateFrozenBitmap(` call site confirms: **`MainWindow.xaml.cs:411` — the sole production call site in the entire solution — uses overload 2.** Overload 1 has exactly three callers anywhere: two unit tests (`ZeroCopyBitmapFactoryTests.cs`, a basic sanity check and a disposal check) and `ProjectionAndBitmapBenchmarks.Windows.cs`. **`ICW-133`'s "legacy point overload" is, precisely, overload 1 — and it is not "legacy" in the sense of a superseded production path being phased out; it appears to have never had any production caller at all**, based on everything read across all six sessions of this audit. The benchmark that's named as if it measures "projection and bitmap" rendering cost — exactly the operation the requirements registry's "Performance evidence" invariant and the dependent tickets (`ICW-064`, `ICW-134`, `ICW-135`) need evidence for — measures a different, unshipped code path instead. `TileMaterializationBenchmarks.Windows.cs` (the other Windows benchmark) does correctly exercise real production surface (`SampleImageGenerator.GenerateSet` → `tile.Pixels`, the full generation pipeline), so this gap is specific to the compositing/projection stage, not the whole suite.

**What this adds beyond the ticket's existing text:** `ICW-133`'s acceptance criteria don't specify which overload is "legacy" or confirm it has zero production callers — a reader could reasonably assume overload 1 is at least exercised somewhere in production, just suboptimally benchmarked. This session confirms it is not exercised in production at all, which sharpens the fix: the replacement benchmark needs to construct real `SampleImageTile`/`SampleAnnotation` inputs and drive overload 2 directly (as `ZeroCopyBitmapFactoryTests.cs` already does for correctness testing — that test file is a ready-made template for the benchmark's input construction), not merely add parameters to the existing point-cloud benchmark.

**Confidence:** 95% (every call site enumerated via grep and read; the "no production caller" claim is as strong as a repo-wide search can make it, short of a Roslyn call-graph analysis).

---

## 2. Correction: my first report's `SpatialBounds` boundary-semantics finding (E26) already has a ticket I hadn't located — cross-reference it

My first report's Evidence Ledger (E26, 75% confidence) flagged that `SpatialBounds.Intersects` uses closed-interval (`<=`/`>=`) semantics while pixel/tile lookups elsewhere (`SampleImageTile`, `TileGridIndexLookup`) use half-open `[X, Right)` semantics, and I could not determine at the time whether this was intentional. This session located the ticket that already targets exactly this: **`ICW-064-spatial-boundary-semantics.md`** (status: **Proposed**) — *"Resolve inconsistencies between closed `SpatialBounds.Intersects` and half-open sampling used by renderer/pixel sampling to avoid off-by-one omissions or double-counting at tile boundaries... recommend closed intervals for geometry queries [as the canonical policy]."* The ticket even recommends a specific resolution direction (standardize on closed intervals, update the renderer to match) that my original report left as an open question. **Recommend linking E26 in the audit trail to `ICW-064-spatial-boundary-semantics.md` explicitly, and raising confidence on the underlying observation from 75% to 90%** now that a ticket author independently arrived at the same inconsistency from a different angle (renderer-correctness review rather than an audit pass).

**Secondary, smaller finding surfaced by locating this file: `ICW-064` is itself a duplicate ID.** `docs/tasks/tickets/` contains two unrelated files both keyed `ICW-064`: `ICW-064-spatial-boundary-semantics.md` (Proposed, boundary semantics) and `ICW-064-tile-cache-capacity-and-materialization-metrics.md` (Done, cache admission/eviction). This is one more instance of the same duplicate-ID problem already tracked under `ICW-081` — not a new category of finding, just one more concrete data point for that cleanup's eventual scope (worth appending to whatever running list of duplicate IDs `ICW-081`'s eventual implementation works from, alongside the `ICW-055`/`ICW-100` duplicates already noted in my third and fourth reports).

**Confidence:** 95% (both ticket files read directly; the ID collision is a simple filename fact).

---

## 3. Corrections Summary Table

| Ticket | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-133` | To Do; acceptance criterion says "benchmark coverage includes a realistic shipped tile path rather than only the legacy point overload" | **Confirmed accurate, sharpened**: the "legacy point overload" (`GenerateFrozenBitmap(IEnumerable<ScreenPoint>, Bgra32Color?)`) has zero production callers anywhere in the solution — this isn't a suboptimal benchmark of a real fallback path, it's the sole benchmark for a method that ships nothing. Fix should construct real tile/annotation inputs and benchmark the actual production overload directly, using `ZeroCopyBitmapFactoryTests.cs` as an input-construction template. | §1 |
| `ICW-064-spatial-boundary-semantics.md` | Proposed | **Cross-reference**: this ticket is the existing home for my first report's E26 finding (closed vs. half-open interval mismatch), which I had left unlinked at 75% confidence for lack of a located ticket. Raise confidence to 90%; link the two. | §2 |
| `ICW-081` (duplicate-ID tracker hygiene) | Proposed | **Append data point**: `ICW-064` is a third confirmed duplicate ID pair (alongside the previously-noted `ICW-055` and `ICW-100` duplicates), spanning genuinely unrelated topics (boundary semantics vs. cache capacity), status "Proposed" vs. "Done" — a scenario where the duplication is especially easy to misread as "this ticket is Done" when checking the wrong file. | §2 |

---

## 4. Assumptions & Open Questions

- §1's "zero production callers" claim is based on a repo-wide text grep for `GenerateFrozenBitmap(`, which would miss a call constructed via reflection or a delegate reference without the literal method-call syntax — considered unlikely in this codebase given the pattern observed everywhere else, but not absolutely ruled out.
- I did not re-verify `ICW-134`/`ICW-135`'s own scopes this session to check whether they separately already plan to address the same benchmark gap from a different angle (e.g., as part of mip-accounting benchmark work) — only `ICW-133` was read in full, since it was the most directly relevant match by content and by its own `links:` field naming `ProjectionAndBitmapBenchmarks.Windows.cs` explicitly.
- The remaining benchmark files (`LiveSpatialQueryBenchmarks.cs`, `StrTreeQueryBenchmarks.cs`, `SnapshotBuildBenchmarks.cs`, `TileWorkCoordinatorBenchmarks.cs`) were also read this session and found well-constructed with no defects worth reporting — noted here so a future session doesn't need to re-read them from scratch, but omitted from the findings above since "this file is fine" isn't a delta worth a full section.

---

*Methodology note: this session read every file in `benchmarks/InfiniteCanvas.Benchmarks/` for the first time, cross-referenced the one substantive finding against `docs/tasks/tickets/` before writing it up (locating `ICW-133`, which already names the same gap), and separately used the same search pass to locate a ticket for a finding left unlinked in the very first report of this series six sessions ago.*
