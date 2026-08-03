# InfiniteCanvasWPF — Delta Report: Requirements-Registry Cross-Check

**Previous reports:** four prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round is entirely new findings, produced by reading a document not previously examined: `docs/requirements/functional-requirements-and-invariants.md`, the project's own canonical registry of "behavioral contracts that must remain true," and then verifying its claims against source rather than trusting them.

---

## 1. New Finding: the pixelometer's "must never initiate tile acquisition" requirement is not met, and the tracker's "Done" status obscures this

**This is the significant finding in this report.** The requirements registry states, under "External audit mandatory requirements" (a table explicitly framed as *"Meeting them is mandatory"*):

> **Pixelometer readout:** Pixelometer readout (`TryReadPixelValue`) must never initiate tile acquisition as a side effect of mouse movement. It must consume a published-frame snapshot or the best available resident payload only. Tile generation triggered by hover must participate in `TileCacheBudget` accounting.

This is one requirement with two clauses: (a) never trigger acquisition from hover, and (b) if it does, participate in budget accounting. I traced the actual call path: `MainWindow.TryReadPixelValue` calls `tile.TryGetPixelsNonBlocking(mipLevel, out sourcePixels, out residentMipLevel, tryReserveCacheEntry: () => _tileCacheBudget.TryReserve(tile))`. Reading `SampleImageTile.TryGetPixelsNonBlocking`'s implementation directly: when the requested mip isn't already resident, it calls `EnsurePixelsGenerationStarted(tryReserveCacheEntry)` — the exact same generation-kickoff path used by the renderer's own on-demand tile materialization. **Clause (a) is not met: hovering the mouse over a not-yet-generated tile does start real generation work**, confirmed by direct code read, not inference.

**Clause (b) is met** — `EnsurePixelsGenerationStarted` receives and threads through `tryReserveCacheEntry`, so any generation it triggers is now correctly counted against `TileCacheBudget` rather than leaking untracked memory, exactly as `ICW-P0-PIXELOMETER-READOUT`'s Wave B fix intended.

**The tracker status is technically accurate but easy to misread.** `active-tasks.md`'s row for `ICW-P0-PIXELOMETER-READOUT` says **"Done,"** with its own Notes explicitly stating: *"Interim fix applied... Long-term published-frame snapshot conversion deferred."* Read carefully, this is honest — it doesn't claim the "never initiate" clause is met. But the requirements registry lists this same ticket ID as the "Related work" for the **full, two-clause mandatory requirement**, not just the accounting clause. Anyone cross-referencing "is this mandatory requirement satisfied?" by checking whether its related ticket is "Done" — which is exactly what a tracker is for — would reasonably conclude yes, when the requirement's primary, harder clause is not met and, more importantly, **has no ticket ID, owner, or scheduled work tracking the deferral** — the registry's own "Notes" column just says "deferred," a dead end with nothing to click through to.

**Why the practical severity is moderate, not severe (worth stating precisely rather than overclaiming):** `EnsurePixelsGenerationStarted` is non-blocking — it submits to the async coordinator queue and returns immediately, so the mouse-move handler itself never stalls. It also self-guards via `Interlocked.CompareExchange(ref _generationQueued, 1, 0)`, so if the hovered tile's generation was already triggered by the normal render path (which, given `ICW-143`'s viewport culling, is true for essentially any tile the mouse can be hovering over, since the mouse can only be over the visible viewport), the pixelometer's call is a cheap no-op, not a duplicate generation. The realistic window where this clause's violation actually does something the render path wouldn't have done anyway is narrow (e.g., a hover that lands microseconds before the render path's own request for the same tile). This is a real, confirmed gap — just not the "unbounded memory growth from mouse movement" failure mode the requirement's own "Failure mode" language might suggest to a reader who hasn't traced the interaction with `ICW-143`.

**Recommendation:**
1. Split the registry's "Pixelometer readout" row (or annotate it) to distinguish the two clauses' status explicitly: accounting clause **Done**, never-initiate clause **Not done, no tracked follow-up**.
2. Create an actual ticket for the deferred long-term work (a real "published-frame snapshot" or "best-available-resident-only, no side effects" implementation for `TryReadPixelValue`), rather than leaving it as a prose note with no ID — this is the same "informal deferral becomes a permanent gap" pattern worth naming on its own, since the registry's stated purpose is specifically to prevent exactly this kind of requirement from quietly falling through.
3. When updating `active-tasks.md`'s statuses generally (a recommendation carried over from my second report), avoid marking a ticket "Done" when it satisfies only one clause of a multi-clause mandatory requirement — a status like "Done (partial — see requirements registry)" would have prevented this specific cross-referencing gap.

**Confidence:** 92% (both call paths read directly — `MainWindow.TryReadPixelValue` and `SampleImageTile.TryGetPixelsNonBlocking`/`EnsurePixelsGenerationStarted` — and the tracker/registry text quoted verbatim from their own files). The "moderate practical severity" calibration is a reasoned inference from the code's guard logic and `ICW-143`'s interest-set behavior, not independently measured/profiled.

---

## 2. Secondary observation: runtime/benchmark diagnostics don't yet cover every metric the "Tile cache capacity and metrics" requirement names (lower confidence, likely already tracked)

The registry also states: *"Runtime and benchmark paths must expose queue depth, generation, conversion/copy, eviction, cache residency, and frame timing before changing image-generation or pixel-transfer technology."* `TileWorkCoordinatorCounters` (the coordinator's diagnostics snapshot record) exposes queue depth (`QueuedCount`), admission/coalescing/completion/cancellation/failure counts, and reservation releases; `TileCacheBudget` separately exposes `UsedBytes`, `EvictionCount`, and `ResidentTileCount`. Between the two, "queue depth," "eviction," and "cache residency" are clearly covered. I did **not** find a runtime-exposed generation-duration, conversion/copy-duration, or frame-timing counter on either type — `SampleImageTile` does track `_generationDurationTicks` internally (set inside `EnsurePixelsGenerationStarted`'s factory delegate) but I did not confirm whether it's exposed as a public property or surfaced anywhere in the app's diagnostics UI (`CacheStatusText`).

**I'm flagging this at lower confidence and explicitly not claiming it's an uncovered gap**, because the requirement's own phrasing ties it to `ICW-064` and to the benchmark-evidence tickets (`ICW-132`–`135`), and I did not read the full `benchmarks/` project this session (`ProjectionAndBitmapBenchmarks.Windows.cs`, `TileMaterializationBenchmarks.Windows.cs`, etc. — these are BenchmarkDotNet harnesses and very plausibly already capture generation/conversion/frame timing independently of the two runtime types I checked, since that's exactly what benchmark harnesses are for). Recommend a future session specifically verify whether the *runtime* (not just benchmark) path exposes generation/conversion/frame timing before treating this as an actionable gap — as stated, this is a "worth checking," not a confirmed finding.

**Confidence:** 55% (the absence on the two runtime types I checked is confirmed; whether this constitutes a real gap given the benchmark project's likely separate coverage is not confirmed).

---

## 3. Corrections Summary Table

| Ticket / Doc | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-P0-PIXELOMETER-READOUT` (tracker: Done) | Marked "Done"; registry lists it as satisfying the full mandatory pixelometer requirement | **Correction: only the budget-accounting clause is satisfied.** The primary "must never initiate tile acquisition" clause is confirmed still violated by direct code read of `TryGetPixelsNonBlocking`/`EnsurePixelsGenerationStarted`. Practical severity is moderate (non-blocking, self-deduplicating against the render path), not severe — stated precisely to avoid overclaiming. | §1 |
| `docs/requirements/functional-requirements-and-invariants.md` | "Pixelometer readout" row cites `ICW-P0-PIXELOMETER-READOUT` as related work with no clause-level status split | **Extend**: annotate the two clauses separately, and replace the "deferred" prose note with an actual tracked ticket ID for the long-term fix. | §1 |
| "Tile cache capacity and metrics" requirement (registry) | Names generation/conversion/copy/frame timing as required runtime+benchmark diagnostics | **Flag for verification** (not a confirmed gap): the two runtime diagnostic types checked this session don't expose these; benchmark-project coverage not yet checked. | §2 |

---

## 4. Assumptions & Open Questions

- I read `docs/requirements/functional-requirements-and-invariants.md` in full this session for the first time; it is a rich, well-maintained document and most of its other invariants (zoom clamping, camera scale limits, deterministic tile generation, claimant token wiring, etc.) were spot-checked opportunistically against code already read in prior sessions and found consistent — only the pixelometer requirement produced a confirmed discrepancy worth reporting. This is not an exhaustive line-by-line verification of every row in that table; a future session could productively do the same cross-check treatment for the remaining ~25 rows not yet spot-checked.
- §2's lower-confidence finding should not be actioned without first reading the `benchmarks/` project's four/five files, which this session did not have time to cover.
- Open question: should the requirements registry itself adopt a convention for multi-clause requirements (e.g., a checklist sub-row per clause) so that partial completion is structurally visible rather than requiring someone to read the ticket's own Notes text to discover it, as happened here? This is a process suggestion, not a code change, but it's the direct, generalizable lesson from this specific finding.

---

*Methodology note: this session's finding was produced by reading the requirements registry's "External audit mandatory requirements" table in full, selecting the one entry (pixelometer readout) most directly falsifiable against code already partially read in prior sessions, then re-tracing the exact call chain (`MainWindow.TryReadPixelValue` → `SampleImageTile.TryGetPixelsNonBlocking` → `EnsurePixelsGenerationStarted`) from scratch to confirm or refute the registry's claim independently of what the tracker said about it.*
