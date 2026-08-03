# InfiniteCanvasWPF — Delta Report: Report 2's Tracker-Sync Recommendation Was Fulfilled — Correcting the Historical Record

**Previous reports:** twelve prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round reads `docs/audits/viewport-requirements-council-review-26-07-30.md` (not previously read), and while cross-checking its duplicate-ID claims against current ticket files, re-examined `active-tasks.md` rows that this series had not specifically re-checked since report 2.

---

## 0. What this session found, stated plainly

**Report 2's central finding was: "`active-tasks.md` was not updated to reflect [Sprint 1's shipped fixes] — 6 of those 7 'Done' items still read 'Proposed'/'To Do'."** That was accurate when I wrote it, checked directly against the file as it existed at that point in the repo's history (right after Sprint 1 Wave D). **It is no longer true, and hasn't been for a long time** — checking the same rows in the current file (unchanged since at least report 3's session, ten sessions ago) shows `ICW-P0-ACTIVECOUNT`, `ICW-P0-STALE-PUB`, `ICW-P0-SPATIAL-INDEX-SAFETY`, and `ICW-P1-CLAIMANT-TOKENS` **all now say "Done,"** each with a substantive, specific Notes entry citing test counts, exact mechanisms, and — for `ICW-P0-SPATIAL-INDEX-SAFETY` and `ICW-P0-STALE-PUB` specifically — a direct citation of `docs/audits/external-audit-master-synthesis-26-07-30.md` as the source requirement.

**This series never went back and confirmed this.** Report 3 (the very next session) diffed `TileWorkCoordinator.cs` and `BackgroundTileContracts.cs` for the Wave E changes but did not specifically re-diff `active-tasks.md` against report 2's snapshot. From report 5 onward, individual tickets were checked and correctly found "Done" as encountered organically (e.g., `ICW-P0-PIXELOMETER-READOUT` in report 5), but **no session explicitly stated "report 2's tracker-sync complaint has been resolved."** Eleven subsequent reports have referenced tracker-hygiene concerns generally (duplicate IDs, empty ticket stubs, stale ticket text) without revisiting whether this specific, named complaint from report 2 was ever addressed. It was — likely during the same Wave E / council-review window this series has otherwise been tracking closely — and this report is the first to say so plainly.

This is a different flavor of correction than reports 8, 9, 11, and 12 (which corrected my own errors). This one corrects an **omission**: a recommendation was acted on, and the record should show that rather than silently continuing to imply otherwise by never mentioning it again.

---

## 1. What actually happened, with evidence

Current `active-tasks.md` rows, quoted directly:

- **`ICW-P0-ACTIVECOUNT`**: status **Done**. Notes: *"Verified at Sprint 1 Wave A: `_activeCount` decremented in worker termination path only, not in `CancelWorkItem`. No changes needed."* Next steps: *"Keep complete. Two residual issues surfaced (see `ICW-P0-ACTIVECOUNT-residuals`)."*
- **`ICW-P0-STALE-PUB`**: status **Done**. Notes cite `docs/audits/external-audit-master-synthesis-26-07-30.md` directly as the originating requirement and describe the exact injection test (`CoordinatorCompletion_WithStaleEpoch_DiscardsPixels`) that verifies it, with a test count (91/91).
- **`ICW-P0-SPATIAL-INDEX-SAFETY`**: status **Done**. Notes: *"`LiveSpatialIndexService.Query` now returns `ToArray()` instead of mutable `List<T>` for full snapshot isolation during publish. Added `Query_ReturnsImmutableArray_CallerCannotModify`... and `QueryDuringPublish_ReturnsConsistentSnapshot`... 91/91 tests pass."*
- **`ICW-P1-CLAIMANT-TOKENS`**: status **Done**. Notes describe the per-tile claimant identity, `ClaimantTokenProvider`, and the two-frame deferred CTS disposal pattern — all confirmed independently via direct code read in report 2's own session, just under a still-"Proposed" tracker status at the time.

**A companion, previously-unremarked-on detail**: `ICW-P0-ACTIVECOUNT-residuals` (the ticket documenting the two second-order issues my report 3 also independently found) is present in `active-tasks.md` as its own row, status **Proposed** — correctly reflecting that the residuals themselves are not yet fixed, while the underlying `ICW-P0-ACTIVECOUNT` fix they're residual *to* is marked Done. The tracker is being precise about this distinction, which is exactly the granularity report 2 asked for (*"a status like 'Done (partial — see requirements registry)' would have prevented this"*) — it just took a different, equally clear form (a separate residuals ticket) rather than a partial-status annotation on the same row.

---

## 2. Secondary finding from this session's actual reading target: the council review's duplicate-ID enumeration doesn't match current ticket files, in a way worth flagging precisely

`viewport-requirements-council-review-26-07-30.md` states: *"6 duplicate ICW IDs (ICW-100 x4, ICW-102 x3, ICW-094 x2, ICW-014 x2, ICW-098 x2, ICW-099 x2) corrupt tracker data integrity."* Checking current ticket files directly: `ICW-100` and `ICW-102` and `ICW-098` and `ICW-099` each have exactly **2** ticket files (not 4, 3, 2, 2 respectively — `ICW-100` and `ICW-102` are lower than claimed), `ICW-094` has **1** ticket file and **1** `active-tasks.md` row (not 2 of either that I could find), and `ICW-014` likewise now has exactly **1** ticket file and **1** `active-tasks.md` row.

**This isn't a contradiction — it's more likely evidence of partial cleanup having already happened between this council review (2026-07-30) and the current state**, consistent with §1's finding that this project has been actively responding to audit output. `ICW-100`'s two current files carry an explicit disambiguating note (*"This is a distinct concern from RenderRequestTracker (Done)... RETAINED as unique ticket"*) rather than being silently left duplicated — a real, if partial, response to the exact concern this council review raised, short of full `ICW-081` renumbering.

**What this means for the duplicate-ID pairs found across reports 3, 4, 7, and 10** (`ICW-055`, `ICW-100`, `ICW-064`, `ICW-004`): none of these four appear in this council review's list at all. Combined with evidence that some of the council review's *own* listed duplicates have since been reduced (ICW-094, ICW-014 now show only one instance each), the honest read is: **the duplicate-ID problem is not a fixed, static list — it has both improved in some places and grown in others across this project's history**, and any future `ICW-081` execution needs a fresh, current enumeration rather than trusting either this council review's list or this series' own accumulated findings as complete or current.

**Confidence:** 85% (current ticket-file and `active-tasks.md` counts directly verified via `ls`/`grep`; the *reason* for the discrepancy — partial cleanup vs. the council review having overcounted at the time — is inferred, not directly evidenced, since no history/git log is available through this session's tooling).

---

## 3. Corrections Summary Table

| Item | Prior framing | Correction | Basis |
|---|---|---|---|
| Report 2's tracker-sync finding | Presented as an ongoing, unaddressed gap; never explicitly revisited in reports 3–12 | **Correct the record**: the specific rows named in report 2 (`ICW-P0-ACTIVECOUNT`, `ICW-P0-STALE-PUB`, `ICW-P0-SPATIAL-INDEX-SAFETY`, `ICW-P1-CLAIMANT-TOKENS`) are all now "Done" with substantive notes, and have been for at least ten sessions' worth of this series without anyone saying so plainly. | §0, §1 |
| `viewport-requirements-council-review-26-07-30.md`'s duplicate-ID count | States 6 specific duplicate pairs with exact counts | **Partial correction**: current ticket files show lower counts for `ICW-100`/`ICW-102`/`ICW-094`/`ICW-014` than claimed — likely reflecting partial cleanup since this review was written, not an error in the review at the time it was written. | §2 |
| Duplicate-ID pairs found in reports 3/4/7/10 | Presented as additive to the known `ICW-081` scope | **Reaffirmed, with caveat**: still not listed in either this council review or the master synthesis — genuinely additive — but the overall duplicate-ID list is evidently a moving target, not a fixed backlog, and needs a fresh count whenever `ICW-081` is actually executed. | §2 |

---

## 4. Assumptions & Open Questions

- I don't have git history access through this session's tooling (tarball retrieval only), so I can't pinpoint exactly which commit fixed the `active-tasks.md` rows report 2 flagged, or which commit reduced `ICW-094`/`ICW-014` from two instances to one. The evidence is consistent with this happening during the Wave E / council-review window (dates align), but that's an inference from timing, not a confirmed causal link.
- This session did not re-check every other status claim across `active-tasks.md`'s full ~150 rows against this series' accumulated findings — only the specific four rows report 2 named. A more exhaustive reconciliation (report 2's own open question, echoed again here) remains undone.
- Open question, restated once more given this is now the third time this exact class of issue has appeared (reports 8, 9, 11, 12, and now this one): this series has been treating "the tracker is stale" as close to a running assumption. This session shows the opposite can also be true — a specific complaint can get fixed and then go un-credited for ten sessions simply because nobody thought to check back. Should each new session in a long-running series like this one begin with a quick re-verification pass over the *previous* report's specific open complaints, not just a diff for new code changes, before moving on to new territory?

---

*Methodology note: this session read `docs/audits/viewport-requirements-council-review-26-07-30.md` for the first time, and its specific, itemized duplicate-ID claim prompted a direct file-count check against current `docs/tasks/tickets/`. That check's results didn't match the review's numbers, which led to checking whether other things had changed since similarly-dated documents were last read — surfacing that report 2's own tracker-sync complaint, still implicitly treated as open by this series' framing in later reports, had in fact been resolved and simply never re-verified.*
