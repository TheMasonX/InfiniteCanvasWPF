# Implementation Sequencing Review

**Date:** 2026-07-30
**Reviewer:** Implementation Sequencing Reviewer
**Scope:** Viewport-aware tile work + external audit requirements sequencing

---

## Findings Summary

### A) Dependency Chain Correctness

#### A1. ICW-143 dependency list completeness

**Finding:** ICW-143 lists dependencies on ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN, and ICW-P1-CLAIMANT-TOKENS. This is correct but incomplete.

**Missing dependency — ICW-100 (RenderRequestTracker):** ICW-143 requires frame-level epoch guarding to ensure stale viewport snapshots do not schedule work for tiles outside the current epoch. The task description says "ICW-078/RenderRequestTracker dependency is tracked via ICW-100" — this acknowledges the dependency but does not list ICW-100 as an explicit dependency in the same way the three coordinator tasks are listed. ICW-100 (re-apply RenderRequestTracker wiring) is a precondition for ICW-143 because without frame-level stale-publication guarding, viewport-culled tile work could still publish stale results.

**Missing dependency — ICW-P0-STALE-PUB:** ICW-143 adds viewport culling. Without a tile-level stale publication guard (ICW-P0-STALE-PUB), a tile that was relevant in the previous viewport snapshot but not the current one could still publish its results. While the frame-level guard (ICW-100) catches this downstream, the tile-level guard is the correct architectural boundary per ADR-0006 ("Completion publishes only if the cache key/revision and request epoch are still valid").

**Severity:** Medium
**Recommendation:** Add `ICW-100` and `ICW-P0-STALE-PUB` to ICW-143's dependency list.

---

#### A2. ICW-100 (RenderRequestTracker re-application) — dependency of ICW-143

**Finding:** Yes, ICW-100 is a dependency of ICW-143. The current sequencing places ICW-100 at "To Do" status while ICW-143 is also "To Do." This is correct ordering but ICW-100 is not assigned to any phase (Phase 0/1/2). It falls between the cracks.

**Severity:** Medium
**Recommendation:** Explicitly assign ICW-100 to Phase 0 (safety harness) alongside ICW-P0-STALE-PUB. Both guard against stale publication at different levels. Update the Phase 0 definition in ADR-0006 to include ICW-100.

---

#### A3. ICW-P0-QUEUE-DRAIN phase placement vs. its own dependencies

**Finding:** ICW-P0-QUEUE-DRAIN is listed as a Phase 0 task but its description states "Currently dormant (gated by ICW-P0-ACTIVECOUNT + ICW-P1-CLAIMANT-TOKENS)." ICW-P1-CLAIMANT-TOKENS is a Phase 1 task. This creates a contradiction: a Phase 0 task cannot be completed without a Phase 1 prerequisite. The DrainQueue liveness check cannot operate correctly until real claimant tokens exist.

Two possible resolutions:
1. Move ICW-P0-QUEUE-DRAIN to Phase 1 and split it into structural skeleton (Phase 0) + liveness wiring (Phase 1).
2. Promote ICW-P1-CLAIMANT-TOKENS to Phase 0 (the handoff calls it "Critical" — it is indeed a safety/correctness prerequisite for both Queue-Drain and the entire coordinator architecture).

**Severity:** High
**Recommendation:** Split ICW-P0-QUEUE-DRAIN into two sub-tasks: (a) Phase 0 — add the DrainQueue liveness-check method skeleton and tests with CancellationToken.None (no behavior change, structural preparation), and (b) Phase 1 — wire the real claimant-token liveness check after ICW-P1-CLAIMANT-TOKENS lands. Alternatively, promote ICW-P1-CLAIMANT-TOKENS to Phase 0.

---

#### A4. ICW-P0-SEQUENCING as the first step

**Finding:** ICW-P0-SEQUENCING says "Restructure ICW-141 epic with Phase 0 and Phase 1 milestones." This is the right conceptual first step — the epic needs a sequencing framework before implementation begins. However, ICW-P0-SEQUENCING is a planning/documentation task, not an implementation task. The actual first implementation step should be ICW-P0-ACTIVECOUNT (as the handoff recommends).

The risk is that ICW-P0-SEQUENCING becomes a blocking gatekeeper: if it is treated as a dependency of every Phase 0 task, it serializes all work behind one documentation change. This is unnecessary — Phase 0 tasks can start implementation as soon as the epic sequencing is agreed upon, even before the ADR is formally updated.

**Severity:** Low
**Recommendation:** Keep ICW-P0-SEQUENCING as a planning prerequisite for Phase 1+ tasks that need the ADR-0006 update, but do not let it block Phase 0 implementation. Phase 0 tasks can proceed in parallel with the ADR update.

---

### B) Phase Ordering

#### B1. Phase 0 completeness

**Finding:** The proposed Phase 0 (ICW-P0-STALE-PUB, ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN, ICW-P0-PIXELOMETER-READOUT, ICW-P0-TRANSACTIONAL-REGEN, ICW-P0-BUFFER-REUSE-SYNC, ICW-P0-SPATIAL-INDEX-SAFETY, ICW-P0-LEASE-RELEASE, ICW-P0-SEQUENCING, ICW-P0-MIGRATION-GUARD) is generally comprehensive but has notable omissions:

**Missing from Phase 0:**
1. **ICW-100 (RenderRequestTracker re-application)** — frame-level stale publication guard. This is a safety harness task and belongs in Phase 0.
2. **ICW-102 (defect-pool dispose with render fence)** — race condition between DisposeDefectTemplatePools and in-flight render. This is a safety/race-condition fix, not a correctness or scheduling feature. Belongs in Phase 0.
3. **ICW-021 (backbuffer reuse safety)** — already partially tracked via ICW-P0-BUFFER-REUSE-SYNC. Ensure the link is explicit.
4. **ICW-134 (variant-aware cache accounting)** — the `_pixelCost` defect is a correctness bug (undercounts memory by ~33%). It is linked to ICW-P1-PIXELCOST-MIPS but the ticket scope was expanded. Consider whether the `_pixelCost` fix should be Phase 0 (it is a memory accounting correctness bug).

**Severity:** High
**Recommendation:** Add ICW-100 and ICW-102 to Phase 0. Add a note linking ICW-021 to ICW-P0-BUFFER-REUSE-SYNC. Evaluate whether the `_pixelCost` hotfix (without full variant accounting) belongs in Phase 0 as a targeted fix.

---

#### B2. Phase 1 completeness

**Finding:** Phase 1 (ICW-P1-CLAIMANT-TOKENS, ICW-P1-COOPERATIVE-CANCEL, ICW-P1-GDI-CONCURRENCY, ICW-P1-SETTINGS-VALIDATION, ICW-P1-PIXELCOST-MIPS, ICW-P1-SETTINGS-SCOPE) covers correctness tasks. However:

**Missing from Phase 1:**
1. **ICW-104 (tile-cache eviction policy)** — FIFO-by-dict-order eviction is non-deterministic. This is a correctness issue that affects memory pressure and cache behavior. It should be Phase 1 or explicitly deferred to Phase 2.
2. **ICW-134 (variant-aware cache accounting)** — linked to ICW-P1-PIXELCOST-MIPS but not listed in Phase 1. The expanded scope (per-handoff) includes the specific `_pixelCost` defect. If the targeted fix is Phase 0, the full variant accounting goes in Phase 1.

**Severity:** Medium
**Recommendation:** Add ICW-104 to Phase 1 or document explicit deferral to Phase 2. Explicitly link ICW-134 to ICW-P1-PIXELCOST-MIPS.

---

#### B3. Missing pre-phase tasks

**Finding:** There are no explicitly defined pre-phase infrastructure tasks:

1. **ICW-036 (CI and nullable-enforcement baseline)** — CI infrastructure is important for validating Phase 0/1 changes but is not a dependency. It could run in parallel.
2. **ICW-014 (global exception safety net)** — In Progress. Error handling improvements are beneficial for all phases but not a hard dependency.
3. **ICW-081 (reconcile duplicate and orphaned tickets)** — The duplicate ID problem (see Status Correctness section) makes the task tracker unreliable for sequencing. This reconciliation should ideally happen before or during Phase 0 to ensure accurate tracking.

**Severity:** Low
**Recommendation:** Document ICW-081 (ticket deduplication) as a recommended pre-phase or early-Phase-0 task. Do not make it a hard gate.

---

### C) Parallelism Opportunities

#### C1. Phase 0 tasks that can run in parallel

| Task | File | Can parallelize with | Cannot parallelize with |
|------|------|---------------------|------------------------|
| ICW-P0-ACTIVECOUNT | TileWorkCoordinator.cs | ICW-P0-PIXELOMETER-READOUT, ICW-P0-TRANSACTIONAL-REGEN, ICW-P0-SPATIAL-INDEX-SAFETY, ICW-P0-SEQUENCING, ICW-P0-MIGRATION-GUARD | ICW-P0-LEASE-RELEASE (same file), ICW-P0-STALE-PUB (same class/interface boundary) |
| ICW-P0-STALE-PUB | SampleImageTile.cs, TileWorkCoordinator.cs | ICW-P0-PIXELOMETER-READOUT, ICW-P0-TRANSACTIONAL-REGEN, ICW-P0-SPATIAL-INDEX-SAFETY | ICW-P0-ACTIVECOUNT (coordinator overlap) |
| ICW-P0-QUEUE-DRAIN | TileWorkCoordinator.cs | Limited — depends on ACTIVECOUNT + CLAIMANT-TOKENS | ACTIVECOUNT (same file), CLAIMANT-TOKENS (Phase 1) |
| ICW-P0-PIXELOMETER-READOUT | MainWindow.xaml.cs, SampleImageTile.cs | All except TRANSACTIONAL-REGEN (same file) | ICW-P0-TRANSACTIONAL-REGEN (same file, both touch MainWindow) |
| ICW-P0-TRANSACTIONAL-REGEN | MainWindow.xaml.cs | Most Phase 0 tasks | ICW-P0-PIXELOMETER-READOUT (same file — merge conflict risk) |
| ICW-P0-BUFFER-REUSE-SYNC | MainWindow.xaml.cs | Most Phase 0 tasks | ICW-P0-PIXELOMETER-READOUT, ICW-P0-TRANSACTIONAL-REGEN (same file) |
| ICW-P0-SPATIAL-INDEX-SAFETY | InfiniteCanvas.Spatial | All Phase 0 tasks | None — independent assembly |
| ICW-P0-LEASE-RELEASE | TileWorkCoordinator.cs | Most Phase 0 tasks except ACTIVECOUNT | ICW-P0-ACTIVECOUNT (same file) |
| ICW-P0-SEQUENCING | docs/ADR/0006, active-tasks.md | All tasks | None — documentation only |
| ICW-P0-MIGRATION-GUARD | docs/ADR/0006 | All tasks | None — documentation only |

**Key parallelism constraints:**
- **Serial chain 1:** ICW-P0-ACTIVECOUNT → ICW-P0-QUEUE-DRAIN (structural part) → ICW-P1-CLAIMANT-TOKENS → ICW-P0-QUEUE-DRAIN (liveness wiring part). This is the critical path through the coordinator changes.
- **Serial chain 2:** ICW-P0-STALE-PUB → ICW-100 → ICW-143. The tile-level guard and frame-level guard are complementary but independent in implementation.
- **MainWindow.xaml.cs bottleneck:** Three Phase 0 tasks (PIXELOMETER-READOUT, TRANSACTIONAL-REGEN, BUFFER-REUSE-SYNC) all touch MainWindow.xaml.cs. They must be sequenced carefully or implemented by the same developer to avoid merge conflicts.
- **Independent clusters:** SPATIAL-INDEX-SAFETY (Spatial assembly), SEQUENCING + MIGRATION-GUARD (docs), and PIXELOMETER-READOUT (MainWindow) can all run in parallel with the coordinator tasks.

**Severity:** Info
**Recommendation:** Document the serial chains explicitly. Assign MainWindow.xaml.cs tasks to one developer or sequence them. Run SPATIAL-INDEX-SAFETY, SEQUENCING, and MIGRATION-GUARD fully in parallel with coordinator work.

---

### D) Missing Dependency Links

#### D1. ICW-P0-QUEUE-DRAIN → ICW-P1-CLAIMANT-TOKENS

**Finding:** The task description acknowledges "gated by ICW-P1-CLAIMANT-TOKENS" but there is no formal dependency link. This is a hard dependency: DrainQueue cannot implement liveness checking without real claimant tokens.

**Severity:** High
**Recommendation:** Add explicit dependency: ICW-P0-QUEUE-DRAIN (liveness wiring) depends on ICW-P1-CLAIMANT-TOKENS.

---

#### D2. ICW-P0-STALE-PUB → ICW-100

**Finding:** Both guard against stale publication — ICW-P0-STALE-PUB at the tile completion callback level and ICW-100 at the frame publication level. They are architecturally complementary but have no dependency link. If both are implemented without coordination, there could be inconsistent epoch handling (tile reports stale relative to frame epoch, frame rejects tile that thinks it is current).

**Severity:** Medium
**Recommendation:** Add cross-reference dependency link between ICW-P0-STALE-PUB and ICW-100. They should share the same epoch mechanism and contract.

---

#### D3. ICW-P0-LEASE-RELEASE → ICW-P1-PIXELCOST-MIPS

**Finding:** ICW-P0-LEASE-RELEASE replaces `ReleaseReservation` with `IDisposable` lease that decrements `UsedBytes`. ICW-P1-PIXELCOST-MIPS fixes the byte accounting so `UsedBytes` reflects actual resident mip payloads, not mip-0-only. If LEASE-RELEASE lands before PIXELCOST-MIPS, the lease will correctly release bytes but the bytes will be undercounted. If PIXELCOST-MIPS lands first, the lease will release correctly.

**Severity:** Medium
**Recommendation:** Sequence ICW-P1-PIXELCOST-MIPS before ICW-P0-LEASE-RELEASE, or at minimum add a dependency link. A lease that releases the wrong byte count is better than no release but still incorrect.

---

#### D4. ICW-P0-TRANSACTIONAL-REGEN → ICW-102

**Finding:** `RegenerateSceneAsync` calls `DisposeDefectTemplatePools` as part of regeneration. ICW-102 adds a render fence to `DisposeDefectTemplatePools`. ICW-P0-TRANSACTIONAL-REGEN must integrate with the render fence added by ICW-102. Without a dependency link, the transactional regen task could implement rollback logic that is incompatible with the disposal fence.

**Severity:** Medium
**Recommendation:** Add dependency: ICW-P0-TRANSACTIONAL-REGEN depends on ICW-102 (defect-pool dispose fence).

---

#### D5. ICW-P0-BUFFER-REUSE-SYNC → ICW-021

**Finding:** ICW-P0-BUFFER-REUSE-SYNC and ICW-021 both address the `InteropBitmap` compositor-handoff race. The Phase 0 task says "Expand ICW-021 with concrete acceptance criteria" but there is no formal link. Currently ICW-021 is at "To Do" status and ICW-P0-BUFFER-REUSE-SYNC is "Proposed." Without linking, they could be implemented independently with different synchronization strategies.

**Severity:** Medium
**Recommendation:** Merge ICW-021 into ICW-P0-BUFFER-REUSE-SYNC or add a bidirectional dependency link. Both tasks describe the same defect.

---

#### D6. ICW-143 → ICW-P0-SPATIAL-INDEX-SAFETY

**Finding:** ICW-143 (viewport culling) needs to query the spatial index to determine which tiles are visible. If the spatial index is not thread-safe during publish (ICW-P0-SPATIAL-INDEX-SAFETY), viewport culling could read a partially-published index. ICW-143 does not list SPATIAL-INDEX-SAFETY as a dependency.

**Severity:** Medium
**Recommendation:** Add ICW-P0-SPATIAL-INDEX-SAFETY as a dependency of ICW-143.

---

#### D7. ICW-P1-GDI-CONCURRENCY → ICW-P0-ACTIVECOUNT

**Finding:** ICW-P1-GDI-CONCURRENCY says "GDI+ concurrency is unbounded during cancellation bursts (ICW-P0-ACTIVECOUNT)." The unbounded concurrency is caused by the `_activeCount` defect. Fixing ACTIVECOUNT without also adding GDI concurrency management still leaves GDI+ unbounded during normal operation (up to DefaultMaxConcurrency=4 concurrent GDI+ operations). Conversely, adding GDI concurrency management without fixing ACTIVECOUNT still leaves GDI+ unbounded during cancellation bursts.

**Severity:** Medium
**Recommendation:** Add bidirectional dependency link between ICW-P0-ACTIVECOUNT and ICW-P1-GDI-CONCURRENCY. Neither is sufficient alone.

---

### E) Status Correctness

#### E1. Duplicate ICW IDs (critical data integrity issue)

**Finding:** The following ICW IDs appear multiple times with different descriptions:

| ID | Occurrences | Descriptions |
|---|---|---|
| ICW-100 | 4 | (1) RenderRequestTracker re-application, (2) Overlay precedence + pixelometer alignment, (3) Reconcile duplicate ticket IDs, (4) Decompose MainWindow orchestration |
| ICW-102 | 3 | (1) Defect Bitmap pool disposal (Proposed), (2) Defect bitmap pool disposal with render fence (To Do), (3) Replace string-keyed annotation features with typed metrics |
| ICW-094 | 2 | (1) Display-settings scrollbar layout (Done), (2) Tile reset semantics (In Progress) |
| ICW-014 | 2 | (1) Global exception safety net for async event pipeline, (2) Global exception safety net for async UI pipeline |
| ICW-098 | 2 | (1) Enforce resident-mip fallback (In Progress), (2) Finish or remove scrollbar slice (Proposed) |
| ICW-099 | 2 | (1) Thread MinimumSparseTilePixelSize (To Do), (2) Harden Serilog EventLog sink (Proposed) |

The ICW-102 third occurrence (line 142, "Replace string-keyed annotation feature dictionary with typed metrics") is a duplicate of ICW-031 and ICW-111. It should use the correct ID.

The ICW-100 second occurrence (line 134, "Define overlay precedence and align pixelometer sampling") is a duplicate of ICW-035 scope (renderer-pixelometer blend contract).

The ICW-100 third occurrence (line 139, "Reconcile duplicate and orphaned ticket IDs") is a duplicate of ICW-081.

The ICW-100 fourth occurrence (line 140, "Decompose MainWindow.xaml.cs orchestration") is a duplicate of ICW-022.

**Severity:** High
**Recommendation:** Execute ICW-081 (ticket reconciliation) before any new tasks are created. Assign unique IDs to each distinct task. The Validate-TaskTracker.ps1 script should be extended to detect duplicate IDs (it already exists but apparently does not catch this).

---

#### E2. ICW-078 status

**Finding:** ICW-078 is marked "In Progress" which is correct — the wiring was reverted and never re-applied. However, the handoff says "Status correction: Done → In Progress" implying it was previously Done. The current status correctly reflects the reality.

**Severity:** Info
**Recommendation:** Keep ICW-078 as "In Progress" until ICW-100 is completed and verified with the regression test.

---

#### E3. ICW-142 status

**Finding:** ICW-142 is "Done." This is correct — the bounded cancellable tile materialization ownership has been implemented and all 86 tests pass. ICW-143 correctly lists ICW-142 as a foundation.

**Severity:** Info
**Recommendation:** No change needed.

---

#### E4. ICW-141 status

**Finding:** ICW-141 is "In Progress" with next step "Restructure epic to Phase 0 (safety harness), Phase 1 (correctness), then Phase 2+." However, ICW-P0-SEQUENCING (which implements this restructure) is still "Proposed." The epic cannot be considered "In Progress" if the restructuring task hasn't started.

**Severity:** Low
**Recommendation:** Either move ICW-P0-SEQUENCING to "In Progress" or re-assess whether ICW-141 should remain "In Progress" until the restructuring is committed.

---

#### E5. Phase 0/1 task statuses

**Finding:** All ICW-P0-* and ICW-P1-* tasks are "Proposed." This is the correct initial state — they were created by the external audit synthesis and have not yet been reviewed or started. No inconsistency here.

**Severity:** Info
**Recommendation:** No change needed.

---

### Cross-Cutting Observations

#### F1. ADR-0006 update lag

**Finding:** ADR-0006 (Viewport-Aware Tile Work Scheduling) is still "Proposed" and does not reflect the Phase 0/1/2 restructuring. The Implementation Sequence section lists only 4 steps without phase boundaries. The handoff explicitly says ADR-0006 needs updating. Until this is done, the task architecture and the ADR are out of sync.

**Severity:** Medium
**Recommendation:** Update ADR-0006 as part of ICW-P0-SEQUENCING. Add Phase 0, Phase 1, and Phase 2+ milestones. Document the migration guard policy (ICW-P0-MIGRATION-GUARD).

---

#### F2. Critical path length

**Finding:** The critical path from Phase 0 to ICW-143 (viewport culling) is:
ICW-P0-ACTIVECOUNT → ICW-P1-CLAIMANT-TOKENS → ICW-P0-QUEUE-DRAIN (liveness wiring) → ICW-143

That is 4 sequential steps, each requiring implementation, testing, and review. If any step hits a significant issue, ICW-143 could be delayed substantially. Parallelizing the non-coordinator Phase 0 tasks (PIXELOMETER-READOUT, TRANSACTIONAL-REGEN, BUFFER-REUSE-SYNC) helps overall throughput but does not shorten this critical path.

**Severity:** Low (observation)
**Recommendation:** Consider whether ICW-143 can start with a partial implementation — e.g., viewport interest snapshot without full culling — that does not require all three coordinator dependencies to be complete.

---

## Consolidated Recommendations (Priority Order)

1. **[High]** Fix duplicate ICW IDs (ICW-100×4, ICW-102×3, ICW-094×2, ICW-014×2, ICW-098×2, ICW-099×2). Execute ICW-081 first.
2. **[High]** Resolve ICW-P0-QUEUE-DRAIN phase paradox: it depends on a Phase 1 task (ICW-P1-CLAIMANT-TOKENS). Split or promote.
3. **[High]** Add ICW-100 (RenderRequestTracker) and ICW-102 (defect-pool dispose fence) to Phase 0.
4. **[Medium]** Add missing dependency links: ICW-P0-QUEUE-DRAIN → ICW-P1-CLAIMANT-TOKENS, ICW-P0-STALE-PUB ↔ ICW-100, ICW-P0-LEASE-RELEASE → ICW-P1-PIXELCOST-MIPS, ICW-P0-TRANSACTIONAL-REGEN → ICW-102, ICW-P0-BUFFER-REUSE-SYNC ↔ ICW-021, ICW-143 → ICW-P0-SPATIAL-INDEX-SAFETY, ICW-P1-GDI-CONCURRENCY ↔ ICW-P0-ACTIVECOUNT.
5. **[Medium]** Update ADR-0006 with Phase 0/1/2 milestones as part of ICW-P0-SEQUENCING.
6. **[Medium]** Add ICW-104 (eviction policy) to Phase 1 or document deferral.
7. **[Medium]** Add ICW-143 missing dependencies: ICW-100, ICW-P0-STALE-PUB, ICW-P0-SPATIAL-INDEX-SAFETY.
8. **[Low]** Split ICW-P0-QUEUE-DRAIN into structural (Phase 0) + liveness wiring (Phase 1).
9. **[Low]** Do not let ICW-P0-SEQUENCING block Phase 0 implementation tasks.
10. **[Info]** Sequence MainWindow.xaml.cs Phase 0 tasks to avoid merge conflicts.
11. **[Info]** Run SPATIAL-INDEX-SAFETY, SEQUENCING, MIGRATION-GUARD in parallel with coordinator work.
