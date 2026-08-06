# InfiniteCanvasWPF — Delta Report: Provenance Correction — This Series' Report 1 Substantially Re-Derived an Existing Master Synthesis

**Previous reports:** eleven prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round reads `docs/audits/external-audit-master-synthesis-26-07-30.md`, one of ~30 pre-existing audit documents in `docs/audits/` that this series had never opened.

---

## 0. What this session found, stated plainly before the details

`docs/audits/` contains roughly 30 audit documents dated 2026-07-24 through 2026-07-30 — an extensive prior audit history (pass1 through pass12, several deep-dive/followup/net-new passes, and a master synthesis) that **predates this entire report series**. This series' very first report (commit `afa8b5b8`, the repo's HEAD at the time) was produced without ever opening `docs/audits/` to check what was already there. Reading `external-audit-master-synthesis-26-07-30.md` now — a document that consolidates 12 of those prior audit files via four parallel review passes — shows that **my first report's findings (`ICW-P0-ACTIVECOUNT`, `ICW-P1-CLAIMANT-TOKENS`, `ICW-P0-QUEUE-DRAIN`, `ICW-P0-PIXELOMETER-READOUT`, `ICW-P0-LEASE-RELEASE`, `ICW-P1-PIXELCOST-MIPS`, `ICW-P0-BUFFER-REUSE-SYNC`, `ICW-100`, `ICW-P0-TRANSACTIONAL-REGEN`, `ICW-P1-SETTINGS-VALIDATION`, and the `ICW-060`/`ICW-099-serilog` "already resolved" corrections) were, almost item-for-item, already documented here — including the exact same `ICW-P0-*`/`ICW-P1-*` naming convention my first report used.** I did not copy this document (I had not read it); I independently re-derived the same findings via direct source reading. But I should have found and cited it, and I'm stating this plainly rather than letting eleven reports of apparent original discovery stand uncorrected once the actual provenance is known.

**This does not retroactively invalidate report 1's work** — independent re-verification against source has value on its own, and report 1 did catch mistakes the earlier documents made (see §2). But the historical record should show that report 1 was substantially a re-confirmation of already-completed analysis, not a first discovery, and that this master synthesis document is the actual origin of the `ICW-P0-*`/`ICW-P1-*` ticket family this entire series has since been building on.

---

## 1. What report 1 rediscovered vs. what came after

Everything from report 1's headline findings maps directly onto this master synthesis's `H1`–`H10` (High) and `M1`–`M13` (Medium) inventories: coordinator concurrency accounting (`H3`↔`ICW-P0-ACTIVECOUNT`), claimant tokens (`H1`↔`ICW-P1-CLAIMANT-TOKENS`), queue-drain liveness (`H9`↔`ICW-P0-QUEUE-DRAIN`), pixelometer cache-budget bypass (`H8`↔`ICW-P0-PIXELOMETER-READOUT`), the no-op reservation-release counter (`H10`↔`ICW-P0-LEASE-RELEASE`), mip pixel-cost undercounting (`M8`↔`ICW-P1-PIXELCOST-MIPS`), the buffer-reuse compositor race (`M9`↔`ICW-P0-BUFFER-REUSE-SYNC`), and the `RenderRequestTracker` wiring gap (`H6`↔`ICW-100`) — this last one down to the master synthesis explicitly noting *"ICW-078/RenderRequestTracker wiring absent for 19+ commits"*, which is the exact mechanism report 1 found via direct code read.

**Everything from report 2 onward is genuinely additive**, because this master synthesis is dated 2026-07-30 and its source audits predate Sprint 1 entirely — the Wave A–E verification (report 2–3), the residual issues the `ICW-P0-ACTIVECOUNT` fix itself introduced (report 3 §2.1), the pixelometer dual-algorithm inconsistency (report 6 §1.1), the dead XAML binding (report 6 §1.3), the benchmark-suite gap confirmation (report 7), the ADR-0006 vs. `ICW-143` scheduling gap (report 8), the `IBackgroundTileSource` self-correction (report 9), the DesignDoc cross-check (report 10), the invisible parallel ViewModel found to be `ICW-017` (report 11), and the `task-tracker.md`/`ICW-082` checks (report 11 companion) all concern code, tickets, or ADRs that didn't exist yet when this master synthesis was written. Those findings stand on their own regardless of this session's discovery.

---

## 2. Two precise distinctions worth preserving, so nothing gets incorrectly merged

**`M1` (this master synthesis) is not the same finding as report 1's `E26`, despite both being "`SpatialBounds` issues."** `M1` states: *"SpatialBounds permits zero Width/Height; DrawTile divides by zero with no guard"* — a degenerate-input crash risk. Report 1's `E26` (later linked in report 7 to `ICW-064-spatial-boundary-semantics.md`) is about closed-vs-half-open **interval semantics** at tile/pixel boundaries — a correctness/off-by-one concern, not a crash. Both are real, both involve `SpatialBounds`, and they are **different bugs with different fixes**. If `ICW-064-spatial-boundary-semantics` and a future ticket for `M1`'s zero-size guard are ever consolidated for cleanup, they should not be merged into one ticket — they need separate acceptance criteria.

**`L12` (this master synthesis) and report 1's §3.12 (`ICW-101` tooltip-presenter restore) are related but not identical, and citing one doesn't cover the other.** `L12` frames the dictionary-indexer tooltip issue as *"a concrete instance of the tracked string-keyed pattern"* (i.e., an example supporting the broader `ICW-031/111` typed-metrics migration). Report 1's finding is more specific: it identifies that `AnnotationFeaturePresenter.BuildTooltipContent` **already exists, is already safe, and is simply not called** from `CreateAnnotationToolTip` — a same-day, no-typed-metrics-migration-required fix, independent of whether `ICW-031/111`'s larger typed-metrics work ever happens. This master synthesis's own "Existing Tasks Needing Status/Scope Updates" table already recommends *"Ensure `CreateAnnotationToolTip` (`MainWindow.xaml.cs:724`) is named as migration target"* under `ICW-031/111` — report 1's finding satisfies that exact recommendation with a concrete, minimal fix, and should be cited there rather than only under the larger typed-metrics ticket.

**Minor count correction, deferring to the more precise source:** this master synthesis's `H7` states *"19/21 async void handlers lack try blocks"* — a precise count from a dedicated pass. Report 1's `E28` stated *"21... spot-checked ~8, not all 21 individually"* at 80% confidence. Defer to `H7`'s more precise 19/21 figure.

---

## 3. What remains genuinely new even after this discovery: the duplicate-ID data points

This master synthesis's own inventory references `ICW-081`'s existence as a known problem but does not enumerate specific duplicate-ID pairs. **The four specific duplicate-ID pairs found across this series (`ICW-055`, `ICW-100`, `ICW-064`, `ICW-004` — reports 3, 4, 7, and 10 respectively) are not listed here** and remain genuinely new, granular data points for whoever eventually executes `ICW-081`'s cleanup. This is the one category of finding from reports 1–11 that this master synthesis doesn't already cover, worth stating so it isn't mistakenly folded into the "already known" bucket along with everything else in §1.

---

## 4. Corrections Summary Table

| Item | Prior framing | Correction | Basis |
|---|---|---|---|
| This series' Report 1 (entire finding set) | Presented as original code-audit findings | **Provenance correction**: substantially re-derives `docs/audits/external-audit-master-synthesis-26-07-30.md`, which predates report 1 and originated the `ICW-P0-*`/`ICW-P1-*` naming this series adopted. Independently re-verified, not copied — but should be cited as confirming prior work, not discovering it. | §0, §1 |
| Report 1's `E26` vs. master synthesis `M1` | Could be conflated as "the same SpatialBounds issue" | **Keep separate**: `M1` is a zero-size divide-by-zero risk; `E26` is closed-vs-half-open interval semantics. Different bugs, different fixes. | §2 |
| Report 1's `ICW-101` finding vs. master synthesis `L12` | Could be treated as duplicative | **Both valid, different grain**: report 1's finding is the specific, minimal, same-day fix; `L12` supports the larger `ICW-031/111` migration. Cite report 1's finding under `ICW-031/111`'s already-recommended `CreateAnnotationToolTip` scope note. | §2 |
| Report 1's `E28` (21 async-void handlers) | 80% confidence, not individually verified | **Defer to master synthesis's `H7`**: precise 19/21 count from a dedicated pass. | §2 |
| Duplicate-ID pairs (`ICW-055`, `-100`, `-064`, `-004`) | Found across reports 3/4/7/10 | **Confirmed genuinely new**: not enumerated in the master synthesis; remain a valid, additive contribution for `ICW-081`. | §3 |

---

## 5. Assumptions & Open Questions

- I read only `external-audit-master-synthesis-26-07-30.md` this session, not the ~29 other pre-existing audit files it synthesizes (`pass1` through `pass12`, several deep-dive/followup/net-new documents, two `.docx` files, `implementation-sequencing-review-26-07-30.md`, `external-audit-requirements-synthesis-26-07-30.md`, `viewport-requirements-council-review-26-07-30.md`, and `viewport-architecture-review-requirements-to-task-mapping.json`). Given this one synthesis document already accounts for the overlap with report 1's entire finding set, reading the other 29 individually is unlikely to surface much beyond what's already been reconciled here and in `task-tracker.md` — but it hasn't been done, and a fully rigorous provenance check would do it.
- This finding changes how this session's own future summary of "this series' contributions" should be framed: reports 2–11 remain the load-bearing, additive content of this audit series; report 1 should be understood as a (valuable, independently-confirmed) restatement of pre-existing work rather than this series' own original contribution.
- Open question, now asked a third time across this series: should `docs/audits/` be scanned for existing content — not just `docs/tasks/`, `docs/ADR/`, and `docs/requirements/` — before any future session's *first* finding is written up? This session's discovery is the largest-magnitude version of the pattern first seen in reports 8 and 11.

---

*Methodology note: this session opened `docs/audits/` for a full directory listing for the first time in this series — a step that should have happened before report 1 was written — and read the one document most likely to reveal prior overlap (`external-audit-master-synthesis-26-07-30.md`, explicitly a consolidation of 12 other files) rather than the full ~30-document corpus, given time constraints. The comparison in §1–§3 was done by matching this session's document's `H`/`M`/`L` inventory against report 1's own `E`-numbered evidence ledger, item by item.*

