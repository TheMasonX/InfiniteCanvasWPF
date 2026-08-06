# InfiniteCanvasWPF — Delta Report: Historical Audit Provenance — Two Old Bugs Traced to Their Wave G Fix, One Still Open

**Previous reports:** twenty-one prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. Per last session's stated priority, this round reads the backfilled historical audit passes (`pass6`, `pass8`, `pass9`, `pass10`, all dated 2026-07-27/28, well before this series began) before claiming anything further in concurrency, noise generation, or spatial indexing — exactly the areas those passes cover.

---

## 0. Why this session exists

Last session's report flagged a backlog of previously-unseen historical audit documents as a priority to read before claiming novelty in overlapping territory. This session reads four of them (`pass6`: `TileWorkCoordinator` concurrency; `pass8`: reentrant lock chain; `pass9`: noise seamlessness; `pass10`: `ICW-078` dependency tracking) and traces each one's findings forward to their current resolution state — confirming, in one case, that a bug found on **2026-07-27** was only actually fixed on **2026-08-05**, nine days and multiple sprints later, by this series' own most recently-verified work (`ICW-320`/Wave G).

---

## 1. Confirmed: `pass6`'s finding #1 (ghost-claimant from register-before-add ordering) is the direct origin of `ICW-320`'s F-014 fix, verified this session as fully resolved

`pass6` (2026-07-27) found that `TileWorkItem.AddClaimant` registered a cancellation callback on the claimant's token **before** adding the claimant to the tracked list — meaning a pre-canceled token fires its removal callback synchronously against an entry that doesn't exist yet, then the claimant gets added anyway with no future way to be removed via its own token. `pass6`'s exact recommended fix: *"Add the claimant to `_claimants` first, then register the cancellation callback."*

I confirmed in report 21 that Wave G's `ICW-320` (F-014) implements exactly this reordering. Re-reading `pass6`'s full description this session and comparing it word-for-word against the diff I already verified in report 21 confirms this is the same bug, the same fix, and the same recommended code shape — just separated by nine days and (per the intervening reports in this series) at least three more sprints of other work. **This is worth recording precisely**: a real, correctly-diagnosed, correctly-prescribed fix sat open from `pass6` through this entire series' reports 1–21 without being applied, until Wave G's audit-synthesis process independently rediscovered and fixed the identical issue. Whether Wave G's fix was informed by `pass6` directly or rediscovered independently, I can't tell from the artifacts available — but either way, the nine-day gap between diagnosis and fix, for a `High`-severity, `85%`-confidence, precisely-specified finding, is worth noting as a data point on how long a well-documented finding can sit unactioned in this project's history.

**Confidence:** 95% (both documents' text compared directly, and the fix already independently verified against source in report 21).

---

## 2. Confirmed: `pass6`'s finding #3 (defect-bitmap dispose race) was resolved as a side effect of `ICW-321`'s dead-code removal, exactly as `pass6` itself predicted

`pass6`'s finding #3 described a live race: `TileWorkCoordinator.CancelAll()` is cooperative and non-blocking, so a render frame could still be mid-`LockBits` on a pooled `DefectBitmap` at the exact moment `RegenerateSceneAsync` disposes that pool — a genuine crash risk, matching `ICW-103`'s description. `pass6`'s own finding #5 (a reinforcement of an earlier `pass5` finding) noted that the `DefectBitmap`/`LockBits` sampling path this race depended on was **dead code** to begin with — `DrawDefectPatch` builds the bitmap via a redundant copy from the same `pixels` array that also becomes `DefectPixels`, and then never actually reads from the bitmap it built. `pass6` explicitly predicted: *"Deleting it... would also eliminate finding #3's race."*

I checked directly this session: `ZeroCopyBitmapFactory.Windows.cs` has **zero remaining `LockBits`/`DefectBitmap` calls** — only two comments referencing the history (*"the GDI+ LockBits ... pooled DefectBitmap ... precedes the ICW-102 rescope"*). This matches Wave G's `ICW-321` (*"dead `DefectBitmap` LockBits sampling removed from `DrawDefectPatch`"*, confirmed in report 21's read of the handoff). **`pass6`'s finding #3 is resolved, not by directly fixing the race, but by removing the code path the race depended on** — precisely the outcome `pass6` itself anticipated as the better fix (over adding synchronization to a path that shouldn't have existed at all).

**Confidence:** 90% (the current absence of `LockBits`/`DefectBitmap` calls is directly confirmed; the causal link to `ICW-321` specifically, rather than some other change, is inferred from the handoff's description matching exactly, not from a line-by-line diff of this specific removal).

---

## 3. Still open, low priority, unchanged through two hardening waves: `pass6`'s finding #4 (`SetRunning` misused as a mutating query)

`pass6` (`Low` severity, 80% confidence) noted that `TileWorkItem.SetRunning()` uses `Interlocked.Exchange` for atomicity, but every call site already executes under the coordinator's own `_lock` — making the interlocked exchange redundant, and `CancelWorkItem`'s reuse of this *mutating* method purely to *query* prior state ("did this item already start running?") is confusing to read. I checked directly this session: `SetRunning()` is still called identically at both `StartWorkItem` (line 431) and `CancelWorkItem` (line 551, `var wasRunning = !item.SetRunning() && _activeCount > 0;`), unchanged in shape from `pass6`'s description. This has now survived both Wave F and Wave G's hardening passes through this exact file — consistent with, and a lower-stakes cousin of, this series' own ongoing `CancelWorkItem`-lock-contract pushback (reports 6, 13, 21): a real, correctly-diagnosed, low-cost-to-fix readability issue in this class that keeps not making it into the otherwise-thorough hardening passes touching the same lines around it.

**Confidence:** 85% (directly re-verified against current source; severity assessment carried over from `pass6`'s own calibration, not independently re-derived).

---

## 4. Confirmed already fully tracked, no new action needed: `pass8` and `pass9`

**`pass8`** (reentrant lock chain in cache eviction) is the direct origin of `ICW-322`, already verified fixed in report 21 — the team chose `pass8`'s recommendation #1 (document the chain) over recommendation #2 (restructure to remove the reentrant hop), an explicit, reasoned tradeoff recorded in Wave G's own decision log. No gap between what `pass8` asked for and what was delivered, beyond the team's own considered choice of which of `pass8`'s two options to take.

**`pass9`** (per-tile noise seed defeating seamlessness, plus per-tile-local contrast normalization) is the direct origin of `ICW-324`, currently `Proposed` and correctly blocked on a product decision — I read `ICW-324` in full this session and confirmed it not only cites `pass9`'s findings accurately (as `F-010`/`F-022`) but catches a subtlety `pass9` itself didn't fully resolve: a **genuine requirement conflict** between the deterministic-per-tile-stream invariant and the seamless-worldspace-sampling goal, which needs a decision before any code changes, not a blind fix. This is good process — nothing missing here either.

---

## 5. Confirmed superseded, no action needed: `pass10`

`pass10` (2026-07-28) found `ICW-078`'s `RenderRequestTracker` epoch-guard fix had been silently reverted, contradicting `JIRA.md`'s "Done" status. This is now moot: this series' own report 2 (session 2, well before this session) confirmed `RenderRequestTracker` was correctly re-wired into `MainWindow.xaml.cs` during Sprint 1 Wave A, after `pass10` was written. No new action; recorded here only to close the loop on this specific historical document rather than leave it looking unaddressed.

---

## 6. Corrections Summary Table

| Item | Origin | Current status | Basis |
|---|---|---|---|
| Ghost-claimant register-before-add ordering | `pass6` #1 (2026-07-27) | **Confirmed fixed** by `ICW-320` F-014 (2026-08-05), nine days later. | §1 |
| Defect-bitmap dispose race | `pass6` #3 (2026-07-27) | **Confirmed resolved** as a side effect of `ICW-321`'s dead-code removal, exactly as `pass6` predicted. | §2 |
| `SetRunning` mutating-query misuse | `pass6` #4 (2026-07-27) | **Still open**, unchanged through Wave F and Wave G. Low priority, cheap fix, keeps not landing. | §3 |
| Reentrant lock chain in cache eviction | `pass8` (2026-07-28) | **Confirmed fully tracked** — `ICW-322`, documentation chosen over restructure, matches `pass8`'s own two options. | §4 |
| Noise-seam / seed-per-tile issue | `pass9` (2026-07-28) | **Confirmed fully tracked** — `ICW-324`, correctly blocked on a product decision `pass9` didn't itself resolve. | §4 |
| `ICW-078` revert | `pass10` (2026-07-28) | **Confirmed superseded** — re-fixed in Sprint 1 Wave A, already noted in this series' report 2. | §5 |

---

## 7. Assumptions & Open Questions

- `pass7` was not read this session (not in the backfilled batch found — only `pass6`, `pass8`, `pass9`, `pass10` appeared; `pass7` may not exist as a separate document, or may be filed under a different name not matched by this session's search). Worth confirming its existence in a future session.
- Two "external-audit-review" documents (`infinitecanvaswpf-external-audit-review-addendum-26-07-30-05-30-01.md` and `-and-architecture-feedback-26-07-29-21-24-17.md`) and `icw-wave-e-audit-delta-5/6/7.md` (the continuation of the "other agent's" Wave E series first noticed in report 21) remain unread — good candidates for a near-future session, particularly the Wave E deltas, since report 21 noted C20/C23 (from that same series) were rejected by the council without independent verification by this series.
- §1's nine-day gap observation is descriptive, not a recommendation to act on — I don't have enough visibility into this project's actual prioritization process to say whether nine days was reasonable, slow, or irrelevant given other priorities in flight during that window.

---

*Methodology note: this session read four historical audit documents identified as unread in the prior session's "Assumptions & Open Questions," then, for each finding in them, checked current source directly to determine resolution status rather than assuming either "still open" (the documents' own age) or "surely fixed by now" (this series' general experience of findings getting addressed) — three of six findings were confirmed fixed, two confirmed already correctly tracked with no gap, and one confirmed still genuinely open.*
