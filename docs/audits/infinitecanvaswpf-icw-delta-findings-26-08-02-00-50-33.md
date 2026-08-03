# InfiniteCanvasWPF — Delta Report: ADR-0006 vs. `ICW-143` Implementation Gap

**Previous reports:** six prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round examines `docs/ADR/` (not previously read) and re-verifies a piece of already-read code against one specific ADR's decision text.

---

## 1. Finding: `ICW-143` ("Done") does not fully implement the scheduling policy ADR-0006 decided on — the "relevance-priority" half of its own title is unimplemented

**ADR-0006** ("Viewport-Aware Tile Work Scheduling and Cancellation," status **Proposed**) states its Decision explicitly:

> *"The queue prioritizes current visible requests by viewport relevance, with **center distance and mip suitability as deterministic tie-breakers**. Prefetch work is lower priority than visible work."*

**`ICW-143`** ("Add viewport culling and **relevance-priority** tile scheduling," status **Done**, "All P0 dependencies cleared... delivered in Sprint 1 Wave D") is the ticket that implements this ADR. I re-read `TileWorkCoordinator.DrainQueueWithLivenessCheck`'s current, already-verified-unchanged implementation (confirmed via diff against every prior session's copy — this code hasn't moved since Wave E) specifically checking for a distance-to-viewport-center or mip-suitability comparison. **There is none.** The actual logic is a binary split only: when the head-of-queue item's key `!_interestSet.IsVisible(key)`, the method scans forward through the queue for the *first* item (in enqueue order) that *is* visible, and promotes it — full stop. Among multiple visible items, or among multiple items already at the head of the queue, there is no ranking by distance from the viewport center and no comparison of "mip suitability" at all. What shipped is accurately described as "visible tiles drain before non-visible tiles, FIFO within each group" — not the distance/mip-ranked relevance ordering the ADR decided on and the ticket's own title names.

**This isn't a hypothetical gap — there's a concrete scenario in this exact codebase where it matters.** `MainWindow.RenderFrameAsync` (read and diffed in a prior session) constructs the interest set by adding **two** keys per visible tile when the camera-selected mip level is greater than zero: one for mip 0 and one for the currently-selected mip. Both keys can be `Queued` simultaneously for the same tile. When `DrainQueueWithLivenessCheck` encounters this pair, both are equally "visible" per `ViewportInterestSet.IsVisible` — nothing distinguishes "the mip that's actually useful to display right now" from "the other one" the way ADR-0006's "mip suitability" tie-breaker was meant to. Whichever of the two happens to be dequeued first wins, which is enqueue-order-dependent, not relevance-dependent.

**Why this matters as a tracker-accuracy issue, not just a missing optimization:** `ICW-143`'s own text claims full delivery with no caveat about this gap — its "Deferred Items" section lists three specific performance/allocation concerns (already known from the council review) and two housekeeping tickets (`ICW-144`, `ICW-081`), but never mentions that the ADR's named tie-breaking criteria were left out. A reader checking "does the scheduler implement ADR-0006?" by looking at `ICW-143`'s "Done" status and its dependency graph (which explicitly lists `docs/ADR/0006-viewport-aware-tile-work-scheduling.md` as a linked file) would reasonably conclude yes. It's a partial yes: the coarser, arguably higher-value half (visible beats non-visible) shipped correctly and is well-tested; the finer half named explicitly in the ADR's Decision section did not ship and isn't tracked as outstanding anywhere I could find.

**Recommendation:**
1. Do not move ADR-0006 from **Proposed** to **Accepted** until this gap is either closed or the ADR's Decision text is revised to describe only what shipped (dropping the "center distance and mip suitability" tie-breaker language, or explicitly marking it as a future-phase addition).
2. Open a follow-up ticket for the actual tie-breaking logic (a reasonable shape: sort candidates within the visible set by `camera center distance` ascending, then by `mip suitability` — e.g., prefer the key matching the camera's currently-selected mip over other resident mip requests for the same tile — before falling back to FIFO), and link it from both `ICW-143` and ADR-0006 rather than leaving the gap undocumented.
3. Add a regression test for the specific dual-mip-key scenario described above (two visible keys for the same tile at different mip levels, both queued) to make the current absence of tie-breaking visible in test output rather than only discoverable by reading the method body.

**Confidence:** 90% (the ADR's decision text and `DrainQueueWithLivenessCheck`'s full implementation were both read directly and compared line-for-line; the dual-mip-key scenario is confirmed from `MainWindow.RenderFrameAsync`'s interest-set construction code read and diffed in a prior session). The lower bound of that confidence reflects that "should this block ADR acceptance" is a judgment call about project process, not a pure code fact.

---

## 2. Corrections Summary Table

| Ticket / Doc | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-143` | Done; title claims "relevance-priority tile scheduling"; no caveat in Deferred Items | **Correction**: only the visible/non-visible binary split shipped. The "center distance and mip suitability" tie-breaking ADR-0006 decided on, and the ticket's own title names, is absent and untracked. | §1 |
| ADR-0006 | Proposed | **Recommendation**: do not advance to Accepted until the tie-breaking gap is closed or the Decision text is revised to match what actually shipped. | §1 |
| *(new, no existing ticket found)* tie-breaking follow-up | — | **New ticket recommended**, linked from `ICW-143` and ADR-0006, covering center-distance and mip-suitability ordering within the visible set — including the concrete dual-mip-key-per-tile test scenario. | §1 |

---

## 3. Assumptions & Open Questions

- I did not exhaustively search every ticket file for one that might already cover this specific tie-breaking gap under a different name or ID — I checked `ICW-143`'s own text (Deferred Items, Related Tasks) and found no mention, and a targeted search for "center distance" / "mip suitability" phrasing turned up nothing, but a differently-worded ticket covering the same intent can't be fully ruled out without a broader search than this session's budget allowed.
- The severity judgment ("arguably higher-value half shipped correctly") is a reasonable engineering assessment, not a measured one — no benchmark data exists yet (per `ICW-144`, still open) to confirm whether the missing tie-breaking actually produces a user-visible ordering problem during real fast-scroll usage, versus being a rare, low-impact edge case.

---

*Methodology note: this session read all six ADR documents for the first time this audit series and specifically cross-checked ADR-0006's Decision text, sentence by sentence, against the exact implementation of `TileWorkCoordinator.DrainQueueWithLivenessCheck` already on file from prior sessions (re-confirmed unchanged by diff), rather than assuming the ADR and its implementing ticket agree simply because the ticket links to the ADR.*
