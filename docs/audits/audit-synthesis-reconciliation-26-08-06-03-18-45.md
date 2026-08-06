# Audit Synthesis Reconciliation — Six Audit Reports at HEAD c552830

**Description:** Reconcile six external audit reports (Wave E delta-8, ICW-314 next-slice, ICW-316 assembly-extraction, and three ICW delta-findings reports) into a verified, de-duplicated findings report, with priority on the requirements to serve as a functional web inspection viewport.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `c55283023d993f9c799ad31e941ad2cc9621c198` (HEAD, origin/main)
**Latest commit:** `c552830` - `feat: wave H — extract canvas component into InfiniteCanvas.Controls library (ICW-316)`
**ID Hash:** `9653b1b6109ab89b76665db41a31b36b`
**Author:** InfiniteCanvas Agent (DeepSeek V4 Flash)
**Timestamp:** 2026-08-06 03:18 US Central
**Review mode:** full reconciliation
**Scope:** six audit reports under `docs/audits/`; the coordinator, tile cache, canvas boundary, interaction ownership, and task-tracker records they touch.

## Executive Summary

This pass reconciled six audit reports against HEAD `c552830`. It extracted 21 candidate claims (19 from the reports plus 2 independent provenance findings: the byte-identical ICW-314/ICW-316 audit files and the JIRA.md divergence for ICW-313/314). Direct source verification confirmed 21 of 21 claims; none were refuted. Eight claims were already resolved or tracked and need no new work. Ten accepted findings are recorded: four create new tasks (`ICW-327`..`ICW-330`), five update existing records, and one is a process finding.

**Concurrent-run resolution (Wave I):** during this synthesis, a concurrent Wave I batch (handoff `docs/handoffs/2026-08-06-wave-i-cancellation-and-boundary-hardening.md`) independently implemented and validated all four coordinator/boundary findings under the same keys. Source verification confirms the implementations and tests are present at the working tree: `TileWorkCoordinator.cs` re-coalesce registration refresh (ICW-327, line 823/834), `SampleImageTile.cs` single-pass mip scan (ICW-328, line 314), `CanvasControl.xaml.cs` stale-frame revision guard (ICW-329, lines 135/175/180), and the full ICW-330 scope (`IsRunning()` line 928, caller-held-lock docs on `StartWorkItem`/`CancelWorkItem`, eviction-discard comment precision). The canonical key mapping is therefore ICW-327 = AddClaimant, ICW-328 = mip scan, ICW-329 = revision wiring, ICW-330 = coordinator lock contract. An earlier draft of this report assigned ICW-328/329 in reverse; those duplicate Proposed records were removed this pass, the ticket files were renamed to the canonical names, and the trackers were aligned. No implementation work remains from this synthesis.

The highest-risk result is `F-001`: `TileWorkItem.AddClaimant` re-coalesce never refreshes its `CancellationTokenRegistration`, so any multi-frame tile generation becomes uncancellable after one frame boundary. It survived both Wave F and Wave G hardening of the same method and is untracked at HEAD. This directly undermines the claimant-token cancellation that the web-inspection viewport's live-streaming scenario depends on, and it is the top implementation priority.

The material provenance corrections: the `icw-316-canvas-assembly-extraction-audit` file is a byte-for-byte copy of the `icw-314-next-slice-audit` file (identical MD5) and contains no assembly-extraction review; `CanvasFrame.Revision`, listed as delivered by the Done `ICW-316A` ticket, is a scaffolded no-op; the 2026-08-03 council's "stale" rejections of C11 and C23 did not hold up under verification.

## Review Method and Coverage

Fixed point `c552830` was confirmed with `git rev-parse`. Each external claim was verified by reading the cited source directly: `TileWorkCoordinator.cs`, `SampleImageTile.cs`, `CanvasFrame.cs`, `ICanvasItem.cs`, `CanvasViewModel.cs`, `CanvasControl.xaml.cs`, and `MainWindow.xaml.cs`. File identity of the two suspect audit files was confirmed by MD5 comparison. Trackers (`active-tasks.md`, `JIRA.md`, ticket files) were read to confirm task status, ID availability, and divergence. No build or test suite was run this pass; verification is static source tracing, consistent with the source audits being reconciled. The web-inspection-viewport requirement priority comes from the user, `DesignDoc.md` (live-streaming section), `ADR-0007`, and the requirements registry.

Not inspected: runtime execution, profiler logs, benchmark outputs, and the historical audit passes referenced only transitively (pass5, pass7). The unread external-audit-review documents named by the source audits remain uninspected and are listed as open questions.

## Table of Findings

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task | Sources |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F-001 | AddClaimant re-coalesce never refreshes the registration | Spec | Create | Confirmed | P1 | 95% | ICW-327 | S6, S1 |
| F-002 | CanvasFrame.Revision is a scaffolded no-op | Spec | Create | Confirmed | P2 | 95% | ICW-329 | S1 |
| F-003 | TryGetBestResidentMip allocate-and-sort under lock, reachable from the clean pixelometer path | Standards | Create | Confirmed | P2 | 90% | ICW-328 | S5 |
| F-004 | Coordinator lock contract and SetRunning query semantics | Standards | Create | Confirmed | P3 | 85% | ICW-330 | S4, S5, S1 |
| F-005 | ICW-314 item contract too shallow; host still owns selection/tooltip | Spec | Update | Confirmed | P2 | 90% | ICW-314 | S2, S3 |
| F-006 | ICW-316 audit file is a byte-identical copy of the ICW-314 audit file | Standards | Update | Confirmed | P3 | 100% | provenance note | S2, S3 |
| F-007 | ICW-313/314 absent from JIRA.md | Standards | Update | Confirmed | P3 | 100% | JIRA.md | S2, S3 |
| F-008 | ICW-204 "optional follow-up" note understates a precise high-severity defect | Spec | Update | Confirmed | P3 | 90% | ICW-204, ICW-327 | S6 |
| F-009 | Eight claims already resolved or tracked; verified, no new work | Standards | Close | Confirmed | none | 95% | none | S4, S5 |
| F-010 | Council "stale" rejection pattern lacked per-claim evidence | Standards | Reject as finding | Confirmed | P3 | 90% | process note | S5 |

## Findings

### F-001 AddClaimant re-coalesce never refreshes the registration

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 95%, limited to static tracing; no runtime reproduction was available.
**Origin:** `icw-wave-e-audit-delta-6.md` re-verified by `infinitecanvaswpf-icw-delta-findings-26-08-05-20-52-39.md`; independently re-verified this pass at HEAD.

#### Description

`TileWorkItem.AddClaimant` (`src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs:788-840`), when the claimant already exists (`existing is not null`, lines 796-805), updates only the `OnCompleted`/`OnFailed` callbacks and returns. The newly supplied `claimantToken` is never registered, and the stale `Registration` from the first token carries forward. `MainWindow.RenderFrameAsync` replaces and cancels the per-frame CTS every frame, so every claimant token fires at the next frame boundary. After exactly one coalesce cycle, the claimant has no live registration on any token that will ever fire again. `PublishInterestSet` skips `Running` items and `DrainQueueWithLivenessCheck` only checks `Queued` items, so the token registration was the only cancellation path for a running item. The generation runs to completion and holds its cache reservation no matter how far the tile scrolls off-screen.

#### Rationale

Direct source read this pass confirms: the `existing is not null` branch never touches `claimantToken` and never disposes the existing `Registration`; the ICW-320 F-014 fix (add-then-register, lines 810-835) applies to the first-add path only. This defeats claimant-token cancellation for exactly the multi-frame generations `ICW-204` was built to handle (S6, S1). The mandatory requirement "Claimant token wiring" in `docs/requirements/functional-requirements-and-invariants.md` requires token-source replacement with live registration; this defect silently disables it.

#### Counter-evidence and Deduplication

`ICW-204`'s "optional follow-up: avoid dooming in-flight work when a frame boundary creates a zero-claimant window" note is the only related record and understates the issue (F-008). `ICW-320` F-014 is a distinct first-add ordering fix, not this defect. No ticket file references the re-coalesce registration path; `ICW-327+` IDs were free at HEAD.

#### Recommendation and Validation

Create `ICW-327`. Apply delta-6's specified fix: dispose the existing registration, register the fresh token, store it with the updated callbacks, and handle the synchronous pre-canceled case. Add the regression test that holds a generation across 3+ simulated frame boundaries via re-`Request()` with fresh tokens on the same claimant ID and confirms canceling the latest token cancels the work. This is the highest-priority coordinator finding and a prerequisite for trusting the coordinator under live web-inspection streaming.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-327 | Created this pass |
| Ticket | ICW-204 | Corrected next-step note |
| ADR | ADR-0006 | Claimant-token lifecycle |
| Requirement | functional-requirements-and-invariants.md | Claimant token wiring row |

#### Finding Sources

S1, S6.

---

### F-002 CanvasFrame.Revision is a scaffolded no-op

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, verified both ends of the pipe (construction and consumption) plus a source-wide grep.
**Origin:** `icw-wave-e-audit-delta-8.md`; independently re-verified this pass.

#### Description

`CanvasFrame.Revision` (`src/InfiniteCanvas.Controls/CanvasFrame.cs`, constructor parameter `int revision = 0`, property documented "Stale-frame revision identity (ICW-316A)") is never assigned a real value at its only construction site and never consumed. `MainWindow.PublishFrame` (`src/InfiniteCanvas.App/MainWindow.xaml.cs:662-669`) constructs the frame without `revision:`. `CanvasControl.PublishFrame` (`src/InfiniteCanvas.Controls/CanvasControl.xaml.cs:159-176`) reads `Width`, `Height`, `Raster`, `Viewport`, and counts but never `Revision`. A source-wide grep for `\.Revision` and `revision:` across `src/` returns nothing outside the property definition.

#### Rationale

The `ICW-316A` ticket (status Done) lists "revision identity" as one of four delivered hardening items; the property exists, is validated for no negative-adjacent condition (any `int`, including negative, is accepted), and does nothing. This is the recurring "shipped the shape, not the behavior" pattern previously flagged for `ICW-064`. It reads as though stale-frame detection exists at the host/canvas boundary when it does not, which is dangerous for the web-inspection live-streaming scenario where a host publishes continuously growing data.

#### Counter-evidence and Deduplication

`RenderRequestTracker` (`BeginRequest`/`IsCurrent`/`Advance`) is a working stale-frame guard at the frame level (`ICW-100`); this is a separate, boundary-level property that was scaffolded to do the same but never wired. Not a duplicate.

#### Recommendation and Validation

Create `ICW-329`. Option A (recommended): thread a monotonic revision (the `RenderRequestTracker` version or a local counter in the host publisher) into the constructor and add the consumer half — `CanvasControl.PublishFrame` discards a frame whose `Revision` is not greater than the last displayed. Option B: remove the parameter and property and correct the `ICW-316A` ticket text. Either way `ICW-316A`'s "Done" status must not be read as "revision identity is enforced" until this lands. Canonical key per Wave I: ICW-329.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-329 | Canonical key for revision wiring (Wave I) |
| Ticket | ICW-316A | Correction to delivered claim |
| ADR | ADR-0007 | Host/canvas boundary |
| Ticket | ICW-100 | Working frame-level epoch guard |

#### Finding Sources

S1.

---

### F-003 TryGetBestResidentMip allocate-and-sort under lock

**Axis:** Standards
**Provenance:** Net-new (previously rejected as "stale" by the 2026-08-03 council without rationale)
**Task disposition:** Create
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, exact code fragment and reachability confirmed by source read.
**Origin:** C23 via `infinitecanvaswpf-icw-delta-findings-26-08-05-20-09-37.md` and `icw-wave-e-audit-delta-5.md`; independently re-verified this pass.

#### Description

`SampleImageTile.TryGetBestResidentMip` (`src/InfiniteCanvas.Rendering/SampleImageTile.cs:310-333`) constructs `new List<(int MipLevel, byte[] Pixels)>(_mipPixels.Count + 1)` and runs `.OrderBy(...).ThenBy(...).FirstOrDefault(...)` while holding `_cacheGate`. `TryGetResidentPixels` (line 248), the ICW-312 clean pixelometer read used by `MainWindow.xaml.cs:177`, falls through to this method whenever the exact requested mip is not resident — the most likely case during active pan/zoom.

#### Rationale

Both the old and new pixelometer paths converge on the same shared fallback, so fixing the acquisition-triggering half did not touch this allocation half. The `_mipPixels` dictionary is bounded by `BackgroundTileMipPolicy.MaxMipLevel`, so a single-pass scan is straightforward.

#### Counter-evidence and Deduplication

The rejection of C23 as "stale" is not supported: the code is unchanged from when it was reported, and it is now reachable from newer code (`TryGetResidentPixels`) than existed at report time (S5). This is a distinct mechanism from C20 (resolved by ICW-321).

#### Recommendation and Validation

Create `ICW-328`. Replace the list and sort with a single pass tracking the best candidate by absolute mip distance with the higher-resolution tiebreak preserved. Add a selection-parity test. No behavior change. Canonical key per Wave I: ICW-328.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-328 | Canonical key for the mip scan (Wave I) |
| Ticket | ICW-312 | Clean pixelometer read path |
| ADR | ADR-0005 | Mip selection contract |

#### Finding Sources

S5.

---

### F-004 Coordinator lock contract and SetRunning query semantics

**Axis:** Standards
**Provenance:** Net-new (C11, with the 2026-08-03 rejection corrected) plus Corroboration (pass6 #4)
**Task disposition:** Create
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 85%, direct source read; severity carried from the originating calibrations.
**Origin:** C11 from report 6, pass6 #4, and the delta-8 eviction comment note; consolidated this pass.

#### Description

Three small, low-risk clarity items in `TileWorkCoordinator.cs` / `SampleImageTile.cs`:

1. `CancelWorkItem` (`TileWorkCoordinator.cs:546`) relies on an undocumented caller-held-lock contract. Every call site holds `_lock` (`PublishInterestSet` lines 246-326, `DrainQueueWithLivenessCheck` line 677), but the requirement is not documented.
2. `SetRunning()` (line 868) uses `Interlocked.Exchange`, but every call site (`StartWorkItem` line 431, `CancelWorkItem` line 551) already holds `_lock`, so the atomicity is redundant. `CancelWorkItem` reuses the mutating method purely to query prior state (`var wasRunning = !item.SetRunning() && _activeCount > 0;`).
3. `EvictCacheEntry` (`SampleImageTile.cs:478-510`) clears pixels and resets `_generationQueued` but does not bump `_generationEpoch`. The actual discard of the evicted-but-still-running generation's result comes from the `_pixels is null` check in `OnCoordinatorPixelsGenerated` (line 581), not the epoch comparison; the Wave G comment is imprecise for this path.

#### Rationale

All three were directly re-verified this pass. The C11 rejection as "stale" did not hold: the code still exhibits the undocumented-contract pattern after Wave F and Wave G touched the same method (S5). pass6 #4 has survived both hardening waves (S4).

#### Counter-evidence and Deduplication

These are readability/documentation items with no behavior change; they are distinct from F-001 (a functional cancellation defect in the same file). Consolidated into one cleanup ticket to keep the diff small and testable.

#### Recommendation and Validation

Create `ICW-330`. Document the caller-held-lock contract on `CancelWorkItem`/`StartWorkItem`; split `SetRunning` into a non-mutating `IsRunning` query for `CancelWorkItem`; fix the eviction-discard comment. Validation: coordinator suite 36/36 plus source-scan assertions. Delivered in full by Wave I (2026-08-06): `IsRunning()` at `TileWorkCoordinator.cs:928`, lock-contract docs on both methods, and the eviction-comment precision at the `Request` coalesce site.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-330 | Created this pass |
| Ticket | ICW-320 | Same-file hardening |
| Ticket | ICW-322 | Documentation pattern to match |
| ADR | ADR-0006 | Coordinator contract |

#### Finding Sources

S1, S4, S5.

---

### F-005 ICW-314 item contract too shallow; host still owns selection and tooltip

**Axis:** Spec
**Provenance:** Extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, direct source reads.
**Origin:** `icw-314-next-slice-audit-26-08-04-16-20-00.md` findings 1-4; independently re-verified this pass after Wave H landed.

#### Description

`ICanvasItem` (`src/InfiniteCanvas.Core/ICanvasItem.cs`) exposes only `string Id` and `SpatialBounds Bounds`; its own doc comment says ICW-314 extends it with interaction members. `MainWindow` still owns selection state (`_selectedAnnotationId`, `MainWindow.xaml.cs:43`), tooltip creation (`DeferredAnnotationToolTip`, line 793), and selection writeback (line 896). `CanvasViewModel.VisibleItems` (`CanvasViewModel.cs:59-64`, comment at line 82) is the required bridge for the control to hit-test.

#### Rationale

ADR-0007 decision item 2 requires the item contract to carry hit testing, tooltip payload, and a visual template; decision item 3 requires selection and tooltip hover inside the canvas. Assembly extraction (Wave H) landed, so the remaining work is behavioral: extend the contract, then move the interaction path. This is the highest-priority functional slice on the path to a reusable web-inspection viewport, because a web-inspection host must not reimplement interaction per host.

#### Counter-evidence and Deduplication

`ICW-313` (input handlers) is a separate, user-deferred concern. The tooltip half of ICW-314 waits on `ICW-031` (typed metrics) per the 2026-08-04 council. The `ICW-316` assembly-extraction audit file contains only this ICW-314 content (F-006).

#### Recommendation and Validation

Update `ICW-314`: state the verified host-ownership evidence, make the contract-extension scope explicit, and sequence contract-first then move. Acceptance criterion: selection/tooltip logic no longer references `SampleAnnotation`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-314 | Updated this pass |
| ADR | ADR-0007 | Reusable component boundary |
| Ticket | ICW-031 | Tooltip payload dependency |
| Ticket | ICW-313 | Deferred input abstraction |

#### Finding Sources

S2, S3.

---

### F-006 ICW-316 audit file is a byte-identical copy of the ICW-314 audit file

**Axis:** Standards
**Provenance:** Correction
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 100%, MD5 match.
**Origin:** Independent provenance finding this pass.

#### Description

`docs/audits/icw-316-canvas-assembly-extraction-audit-26-08-04-16-35-00.md` is byte-for-byte identical to `docs/audits/icw-314-next-slice-audit-26-08-04-16-20-00.md` (MD5 `B0EA13324A8562539073228C8DA053D0`). Despite the ICW-316 filename, the content audits only the ICW-314 selection/tooltip slice and never reviews the assembly extraction.

#### Rationale

The duplicate file name will be read as an extraction audit, misattributing ICW-314-only content. Docs are durable, so the file is not deleted; a provenance header note was added this pass.

#### Counter-evidence and Deduplication

No second extraction review exists for the ICW-316 scope; the actual extraction was delivered under ICW-316 (Wave H, commit `c552830`) and verified by its own ticket and consumer-host gate.

#### Recommendation and Validation

Recorded in the source ledger; provenance note added to the file. No further action.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Audit | icw-316-canvas-assembly-extraction-audit-26-08-04-16-35-00.md | Provenance note added |
| Ticket | ICW-316 | Actual extraction delivery |

#### Finding Sources

S2, S3.

---

### F-007 ICW-313/314 absent from JIRA.md

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 100%, grep of JIRA.md.
**Origin:** Independent tracker-hygiene finding this pass.

#### Description

`docs/tasks/JIRA.md` contains no rows for `ICW-313` or `ICW-314` (only ICW-316A/316/319 in the 31x range), while both ticket files exist and `active-tasks.md` lists both as Proposed.

#### Rationale

The two trackers diverge; without rows, the JIRA log cannot represent the dependency chain for the remaining ADR-0007 work.

#### Counter-evidence and Deduplication

None; the missing rows are unambiguous.

#### Recommendation and Validation

Rows added this pass for ICW-313, ICW-314, and the new ICW-327..330. Run the tracker validator to confirm no new schema errors.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Tracker | JIRA.md | Rows added |
| Tracker | active-tasks.md | Statuses matched |

#### Finding Sources

S2, S3 (context).

---

### F-008 ICW-204 "optional follow-up" note understates a precise high-severity defect

**Axis:** Spec
**Provenance:** Correction
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 90%, direct source read plus ticket read.
**Origin:** `infinitecanvaswpf-icw-delta-findings-26-08-05-20-52-39.md`.

#### Description

`ICW-204`'s next-step note ("Optional follow-up: avoid dooming in-flight work when a frame boundary creates a zero-claimant window") is the only related record for F-001 and understates it. An "optional" low-priority note is easy to defer indefinitely; a precisely-diagnosed, high-confidence, currently-live cancellation-defeat defect is not.

#### Rationale

F-001's mechanism is fully specified and verified; the note does not name it.

#### Counter-evidence and Deduplication

None; the correction replaces the vague note with a pointer to `ICW-327`.

#### Recommendation and Validation

Update the `ICW-204` ticket next-step to reference `ICW-327` (done this pass). No code change.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-204 | Corrected |
| Ticket | ICW-327 | Precise tracking |

#### Finding Sources

S6.

---

### F-009 Eight claims already resolved or tracked; verified

**Axis:** Standards
**Provenance:** Corroboration
**Task disposition:** Close
**Verification:** Confirmed
**Severity:** none
**Confidence:** 95%.
**Origin:** `infinitecanvaswpf-icw-delta-findings-26-08-05-19-50-44.md` and `-20-09-37.md`; re-verified this pass.

#### Description

Verified resolved or already tracked, with no new work:

- Ghost-claimant register-before-add (`pass6` #1) -> `ICW-320` F-014 (S4).
- Defect-bitmap dispose race (`pass6` #3) -> resolved incidentally by `ICW-321` dead-code removal; `ZeroCopyBitmapFactory.Windows.cs` retains only two history comments; the remaining `LockBits` calls in `DefectTemplateFactory.cs` and `SampleImageGenerator.cs` are live template-creation paths (S4).
- Reentrant lock chain (`pass8`) -> `ICW-322` (S4).
- Noise seam (`pass9`) -> `ICW-324`, correctly blocked on a product decision (S4).
- ICW-078 revert (`pass10`) -> superseded by the Sprint 1 Wave A re-wiring (S4).
- C20 (`DrawDefectPatch` unused local) -> fixed incidentally by `ICW-321` (S5).
- Assembly-extraction vs interaction orthogonality -> extraction delivered (Wave H), interaction remains ICW-314 (S2/S3).

#### Rationale

Each item was confirmed by direct source read or by matching the existing ticket to the mechanism. `ICW-324`/`ICW-325` remain gated and must not be closed for hygiene.

#### Counter-evidence and Deduplication

None; no new ICW keys are justified for these items.

#### Recommendation and Validation

Recorded as verified. No action.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Ticket | ICW-320/321/322/324 | Verified coverage |

#### Finding Sources

S4, S5.

---

### F-010 Council "stale" rejection pattern lacked per-claim evidence

**Axis:** Standards
**Provenance:** Net-new (process)
**Task disposition:** Reject as finding
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 90%.
**Origin:** `infinitecanvaswpf-icw-delta-findings-26-08-05-20-09-37.md`.

#### Description

The 2026-08-03 council rejected C10, C11, C20, and C23 in a single line, "Reject stale C10, C11, C20, and C23", with no individual rationale. Three of the four labels did not hold up under independent verification by one or both audit series: C11 and C23 were still live at rejection time, and C20 only became "fixed" incidentally later.

#### Rationale

Two independent audit series reached the same conclusion. The label does not reliably track "the code changed" or "already fixed", so future sessions can skip live findings.

#### Counter-evidence and Deduplication

C10 was plausibly justified (no XML `<param>` tags exist to mismatch). The process pattern is recorded, not re-litigated.

#### Recommendation and Validation

Record in this report. Recommend the review process require one line of evidence per rejected claim. No ICW key unless the owner requests a process ticket.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Audit | audit-reconciliation-council-review-26-08-03-00-00-00.md | The rejection line |

#### Finding Sources

S5.

---

## Assumptions

| ID | Assumption | Effect if false | Evidence needed | Owner |
| --- | --- | --- | --- | --- |
| A-1 | `MainWindow.RenderFrameAsync` replaces the per-frame CTS every frame, firing all claimant tokens at the next frame boundary | F-001's severity depends on this | Confirm the two-frame-deferred CTS replacement pattern in source (confirmed by earlier series reports, not re-read this pass) | unassigned |
| A-2 | ADR-0007 remains the canonical boundary contract for the reusable canvas | F-005's framing changes | Re-read ADR-0007 on next boundary work | unassigned |
| A-3 | `ICW-031` (typed metrics) remains a dependency for the tooltip payload half of ICW-314 | ICW-314 sequencing changes | Re-check ICW-031 status before ICW-314 implementation | unassigned |
| A-4 | Static source tracing is sufficient evidence for these findings | A runtime reproduction could change confidence | Reproduce F-001 CPU/reservation symptom under fast scroll on target hardware | unassigned |

## Open Questions

| ID | Question | Why it matters | Cheapest resolution | Owner |
| --- | --- | --- | --- | --- |
| Q-1 | Should the review process require a one-line evidence citation per rejected claim? | "Stale" labels hide live findings | Council/process decision on F-010 | unassigned |
| Q-2 | Does the owner want ICW-314 as one ticket (contract phase then move) or split into two? | Affects tracking granularity | Owner decision | unassigned |
| Q-3 | Does `pass7` exist as a separate document? | The historical series referenced it | File search under docs/audits | unassigned |
| Q-4 | Do the two unread external-audit-review documents contain overlapping findings? | Coverage completeness | Read the two named files | unassigned |

## Requests

| Priority | Request | Rationale | Required response |
| --- | --- | --- | --- |
| P1 | Decide F-001 (ICW-327) is accepted for the next implementation batch | It is the highest-severity untracked defect and a live-streaming prerequisite | Confirm ICW-327 priority P1 and sequence it first |
| P2 | Decide ICW-329 option A (wire Revision) vs option B (remove) | Both are valid; the choice changes the ticket scope | Choose one option (Wave I implemented option A) |
| P2 | Confirm ICW-314 is the priority functional slice for the web-inspection viewport | The user stated web-inspection-viewport requirements are highest priority | Confirm the contract-extension-first sequencing |
| P3 | Confirm the F-010 process recommendation | Rejected-claim labeling affects future audits | Accept or reject the evidence-citation rule |

## Source Ledger

| ID | Source | Type | Revision or date | Read directly | Use and limitation |
| --- | --- | --- | --- | --- | --- |
| S1 | docs/audits/icw-wave-e-audit-delta-8.md | audit | 2026-08-06 | yes | Wave E delta-8; CanvasFrame.Revision finding; eviction epoch note |
| S2 | docs/audits/icw-314-next-slice-audit-26-08-04-16-20-00.md | audit | 2026-08-04 | yes | ICW-314 slice audit |
| S3 | docs/audits/icw-316-canvas-assembly-extraction-audit-26-08-04-16-35-00.md | audit | 2026-08-04 | yes | Byte-identical copy of S2 (MD5 match); provenance note added |
| S4 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-19-50-44.md | audit | 2026-08-05 | yes | Historical provenance (pass6/8/9/10) |
| S5 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-20-09-37.md | audit | 2026-08-05 | yes | C20/C23 verification; "stale" pattern |
| S6 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-20-52-39.md | audit | 2026-08-05 | yes | AddClaimant re-coalesce finding |
| S7 | src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs | code | HEAD c552830 | yes | AddClaimant, SetRunning, CancelWorkItem, StartWorkItem, DrainQueueWithLivenessCheck |
| S8 | src/InfiniteCanvas.Rendering/SampleImageTile.cs | code | HEAD c552830 | yes | TryGetBestResidentMip, TryGetResidentPixels, EvictCacheEntry |
| S9 | src/InfiniteCanvas.Controls/CanvasFrame.cs | code | HEAD c552830 | yes | Revision property and constructor |
| S10 | src/InfiniteCanvas.Controls/CanvasControl.xaml.cs | code | HEAD c552830 | yes | PublishFrame, frame shell |
| S11 | src/InfiniteCanvas.App/MainWindow.xaml.cs | code | HEAD c552830 | yes | PublishFrame construction, selection/tooltip, pixelometer read |
| S12 | src/InfiniteCanvas.Core/ICanvasItem.cs | code | HEAD c552830 | yes | Id + Bounds contract |
| S13 | src/InfiniteCanvas.ViewModels/CanvasViewModel.cs | code | HEAD c552830 | yes | VisibleItems bridge, ApplyFrame |
| S14 | docs/ADR/0007-canvas-reusable-component-boundary.md | ADR | Accepted 2026-08-05 | yes | Reusable boundary contract |
| S15 | docs/requirements/functional-requirements-and-invariants.md | requirement | HEAD c552830 | yes | Mandatory external-audit requirements; web-inspection rows |
| S16 | DesignDoc.md | design | HEAD c552830 | yes | Live-streaming / web-inspection section |
| S17 | docs/tasks/active-tasks.md | task | HEAD c552830 | yes | Task corpus; ICW-313/314 status |
| S18 | docs/tasks/JIRA.md | task | HEAD c552830 | yes | JIRA rows; ICW-313/314 absent (F-007) |
| S19 | docs/tasks/tickets/ICW-313..ICW-330 files | task | HEAD c552830 | yes | Ticket state; ICW-327+ availability |
| S20 | docs/audits/viewport-requirements-council-review-26-07-30.md | audit | 2026-07-30 | yes | Viewport requirement coverage context |
| S21 | docs/audits/audit-reconciliation-council-review-26-08-03-00-00-00.md | audit | 2026-08-03 | yes | The "stale" rejection line (F-010) |

## Task and Sprint Updates

| Finding | Task action | Tracker locations | Sprint impact |
| --- | --- | --- | --- |
| F-001 | create ICW-327 (Done by Wave I, 2026-08-06) | active-tasks.md, JIRA.md, tickets/ICW-327 | Landed; first priority item delivered |
| F-002 | create ICW-329 (Done by Wave I; canonical key is ICW-329 for revision wiring) | active-tasks.md, JIRA.md, tickets/ICW-329 | Landed; boundary hardening delivered |
| F-003 | create ICW-328 (Done by Wave I; canonical key is ICW-328 for the mip scan) | active-tasks.md, JIRA.md, tickets/ICW-328 | Landed; pixelometer path delivered |
| F-004 | create ICW-330 (Done by Wave I; full scope landed: IsRunning, C11 lock-contract docs, eviction comment) | active-tasks.md, JIRA.md, tickets/ICW-330 | Landed; no remaining items |
| F-005 | update ICW-314 | active-tasks.md, JIRA.md, tickets/ICW-314 | Priority functional slice (P2) |
| F-006 | provenance note on S3 | docs/audits/icw-316-...-audit-...md | No change |
| F-007 | add JIRA rows | JIRA.md | No change |
| F-008 | update ICW-204 note | tickets/ICW-204 | No change |
| F-009 | close as verified | none | No change |
| F-010 | process note | this report | No change |
