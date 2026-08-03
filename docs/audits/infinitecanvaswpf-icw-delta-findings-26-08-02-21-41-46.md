# InfiniteCanvasWPF — Delta Report: JIRA.md Cross-Check — Session 10's Finding Was Already Tracked

**Previous reports:** ten prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round reads `docs/tasks/JIRA.md` (168 lines, not previously read in this series) end to end.

---

## 1. Self-correction: my tenth report's "invisible parallel ViewModel" finding is `ICW-017`, already tracked since before this audit series began — I should have found it first

**My tenth report** presented, as a new finding, that `CanvasViewportViewModel<SampleAnnotation>` is instantiated and updated every frame in production but has zero UI consumers, duplicating `MainViewModel.ApplyViewportState`'s job.

**`JIRA.md` already contains this exact ticket**, predating this entire audit series:

> **`ICW-017` | To Do | Improvement | Remove or rewire dead RefreshCommand path** | *"Eliminate redundant CanvasViewportViewModel RefreshCommand flow or wire it as canonical UI path and remove duplicate logic/tests."*
>
> Activity log, 2026-07-24: *"Logged from deep-dive audit cross-validation: RefreshCommand path is test-only and redundant with ApplyFrame usage."*

This is the same underlying problem — a redundant, unbound `CanvasViewportViewModel` path — identified by an earlier audit pass before I ever looked at this codebase. **I should have searched `docs/tasks/` for `CanvasViewportViewModel` before writing up session 10's finding as new**, the same discipline I applied for every other finding in this series. Retracting the "new finding" framing; this is a **confirmation and update** of `ICW-017`, not a discovery.

**What this session adds that's still worth recording — the ticket's own text is now stale relative to the code it describes.** `ICW-017` frames the problem as a "RefreshCommand flow" that needs eliminating or wiring up. I confirmed via direct code read (session 10) and a repo-wide grep (also session 10) that `CanvasViewportViewModel<T>`'s current implementation **has no `RefreshCommand` at all** — its complete surface is four `[ObservableProperty]` fields and one `ApplyFrame` method, and `RelayCommand`/`IAsyncRelayCommand` appear nowhere in the entire solution. The `RefreshCommand` the ticket describes was evidently removed at some point after the ticket was written (or after the 2026-07-24 activity-log entry), leaving the ticket's specific technical description out of date — but the *underlying* problem it was created to track (a redundant, effectively-invisible parallel ViewModel path) persists in a new, more specific form: `ApplyFrame`, not `RefreshCommand`, is now the redundant call, and I traced it to the exact two lines (`MainWindow.xaml.cs:429-430`) where both the dead path and the live path run side by side on every frame.

**Recommendation:** update `ICW-017`'s description to reflect the current code shape (`ApplyFrame`/`MainViewModel.ApplyViewportState` duplication, not `RefreshCommand`) rather than leaving text that no longer matches what a contributor would find if they went looking for a `RefreshCommand` to remove. The two remediation options I proposed in report 10 (make `CanvasViewportViewModel<T>` the real `DataContext`, or delete it and its call site) map directly onto `ICW-017`'s own two stated options ("eliminate... or wire it as canonical UI path") — no new option needed, just updated specifics.

**Confidence:** 95% (the ticket text and activity-log entry are quoted verbatim; the "RefreshCommand no longer exists" claim was already at 95% confidence from session 10's repo-wide grep).

---

## 2. Secondary observation: `ICW-016`'s prior README fix and my ninth/tenth reports' README findings are related but distinct — worth noting so neither is mistaken for contradicting the other

`ICW-016` (Done): *"Update README MVP behavior to current inspection-scene implementation"* — activity log confirms this fixed a **different** README problem (the "MVP behavior" section describing stale application behavior) than what my tenth report found (the separate "Implemented design pillars > MVVM" section's false `IAsyncRelayCommand` claim). These are two different sections of the same file, fixed at two different times, and `ICW-016`'s prior fix does not cover or contradict the tenth report's finding. Noting this explicitly so a future reader doesn't assume "README was already audited and fixed (`ICW-016`, Done)" means the tenth report's separate finding is redundant — it isn't; it's a different, still-open inaccuracy in a different part of the same file. No action needed beyond what report 10 already recommended.

**Confidence:** 90% (both tickets/sections read directly and compared).

---

## 3. Confirmed compliant, checked for transparency: `ICW-082` (background-image visibility persistence)

`JIRA.md`'s activity log includes a striking historical entry: *"2026-07-25 | ICW-082 | Critical peer review verified that background-image visibility is not persisted despite ICW-073 and the requirements registry claiming independent layer-toggle persistence."* This looked like exactly the kind of "claim doesn't match code" pattern this series has repeatedly found, so I checked it directly against current code rather than assuming either the old bug report or the current "Done" status was accurate. **Current code is correct**: `CanvasUserSettings.ShowBackgroundImages` is loaded into both the checkbox and the render-path field at startup (`MainWindow.xaml.cs:161-162`), updated on toggle (`:1075`), and captured back into the settings snapshot before save (`:1507`) — a complete round trip. `ICW-082`'s "Done" status is accurate; this was a real historical bug that has since been fixed. No correction needed — recorded here so a future session doesn't need to re-check it.

**Confidence:** 90% (all four call sites read directly).

---

## 4. Corrections Summary Table

| Ticket / Prior Report | Current status/claim | Correction | Basis |
|---|---|---|---|
| My 10th report (invisible `CanvasViewportViewModel` finding) | Presented as a new finding | **Retract "new"**: this is `ICW-017`, already tracked since 2026-07-24, before this audit series began. | §1 |
| `ICW-017` | To Do; describes a "RefreshCommand flow" | **Update ticket text**: current code has no `RefreshCommand` (confirmed absent solution-wide) — the live redundancy is now `ApplyFrame` vs. `MainViewModel.ApplyViewportState`, at the exact lines report 10 identified. Underlying problem unchanged; description is stale. | §1 |
| `ICW-016` (Done) vs. my 9th/10th reports' README findings | Could appear contradictory at a glance | **Clarify**: no contradiction — `ICW-016` fixed the "MVP behavior" section; report 10 found a separate, still-open issue in the "Implemented design pillars" section. Both true simultaneously. | §2 |
| `ICW-082` (Done) | Background-image visibility persistence | **Confirmed accurate**, checked directly rather than assumed. No correction needed. | §3 |

---

## 5. Assumptions & Open Questions

- I have now read `JIRA.md` in full for the first time this series. It's a rich historical record (168 lines, dating back to 2026-07-23) that predates my first session by several days — a number of findings across this entire report series were, in hindsight, likely already present in this log under an ICW ID I hadn't yet searched for. This report only re-checks the one item I could concretely trace (session 10's finding); a more exhaustive pass cross-referencing every finding from all ten prior reports against this specific file's ~90 rows was not performed this session given time constraints, and could be a productive use of a future session specifically dedicated to that reconciliation.
- Open process question, now asked for the second time in this series (first raised after the ADR self-correction in report 8): should `docs/tasks/JIRA.md` — not just `docs/tasks/active-tasks.md` and `docs/tasks/tickets/` — be a mandatory search target before any finding in this series is written up as "new"? This session's evidence suggests yes: two separate self-corrections in this series (report 8's `IBackgroundTileSource`, this report's `ICW-017`) both trace back to not having read a document that existed the entire time and would have prevented the error.

---

*Methodology note: this session read `docs/tasks/JIRA.md` in full for the first time in this series, specifically checking it against the one finding from report 10 that seemed most likely to already have prior history (a ViewModel-wiring issue, exactly the kind of thing an earlier "deep-dive audit cross-validation" pass — evidently already part of this project's history before this series began — would have caught). It also spot-checked one historical bug entry (`ICW-082`) directly against current code rather than assuming its "Done" status without verification, in keeping with this series' standing practice of not taking any status at face value in either direction.*
