# Council Review: Viewport Requirements — Task Coverage and Sequencing

## Decision

The current task corpus does not fully support the viewport requirements. 16 critical gaps exist between the 51 viewport-related requirements and their supporting tasks. The ICW-P0-* and ICW-P1-* tasks must be promoted from Proposed to active implementation before ICW-143 viewport culling can begin.

## Evidence Reviewed

- `docs/requirements/functional-requirements-and-invariants.md` — 31 canonical requirements + 14 external audit mandatory requirements
- `docs/tasks/active-tasks.md` — full task corpus with statuses
- `docs/ADR/0006-viewport-aware-tile-work-scheduling.md`
- `docs/handoffs/2026-07-30-external-audit-synthesis-handoff.md`
- `docs/audits/external-audit-master-synthesis-26-07-30.md`

## Findings

| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---|---|
| Viewport Architecture Reviewer | 0/22 viewport requirements have full task coverage. All 14 external audit mandatory requirements are Proposed/unstarted. ICW-143 is blocked. | 0.90 | ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN, ICW-P1-CLAIMANT-TOKENS are hard prerequisites for ICW-143. 15 of 22 requirements have zero task coverage. |
| Coordinator/Concurrency Reviewer | 8 of 12 coordinator/concurrency requirements have zero implemented coverage. All external audit mandatory requirements (activeCount timing, queue drain, claimant tokens, lease release, cooperative cancellation, GDI+ concurrency, pixel cost, static claimant) are unstarted. | 0.92 | ICW-142 built the coordinator infrastructure but left every defect the external audit identified unfixed. 16 proposed tasks lack ticket files. |
| Settings/Persistence/MVVM Reviewer | 6 of 16 settings/persistence/UI requirements have hard gaps. Settings validation unification, Phase 1 settings lifecycle, regeneration atomicity, pixelometer budget bypass, frame surface reuse sync, and migration sequencing policy all have zero implemented coverage. | 0.88 | The compatibility plan's Phase 1 can complete without fixing any of the 6 known settings bugs. `ObjectsPerTile` upper bound absent from `IsValid` is a one-line fix with high user impact. |
| Implementation Sequencing Reviewer | 2 High-severity issues: (1) ICW-P0-QUEUE-DRAIN is labeled Phase 0 but depends on ICW-P1-CLAIMANT-TOKENS (Phase 1) — a phase paradox. (2) 6 duplicate ICW IDs (ICW-100 x4, ICW-102 x3, ICW-094 x2, ICW-014 x2, ICW-098 x2, ICW-099 x2) corrupt tracker data integrity. 7 missing dependency links between related tasks. | 0.85 | ICW-081 (ticket reconciliation) must precede any new task creation. The critical path is 4 serial steps: ICW-P0-ACTIVECOUNT → ICW-P1-CLAIMANT-TOKENS → ICW-P0-QUEUE-DRAIN → ICW-143. |

## Synthesis

### What changes now

1. **Create ticket files** for all 16 proposed ICW-P0-* and ICW-P1-* tasks under `docs/tasks/tickets/`
2. **Correct duplicate ICW IDs** per ICW-081 — ICW-100 appears 4 times, ICW-102 appears 3 times, ICW-094/ICW-014/ICW-098/ICW-099 each appear twice
3. **Fix the Phase 0 paradox** — ICW-P0-QUEUE-DRAIN depends on ICW-P1-CLAIMANT-TOKENS (Phase 1). Split ICW-P0-QUEUE-DRAIN into structural preparation (Phase 0) and liveness wiring (Phase 1), or promote ICW-P1-CLAIMANT-TOKENS to Phase 0
4. **Add 7 missing dependency links** between related tasks (documented in sequencing review)
5. **Move ICW-P0-ACTIVECOUNT to To Do** — this is the highest-priority task in the entire path
6. **Move ICW-100 to To Do** — RenderRequestTracker re-application is a hard prerequisite for ICW-143
7. **Expand ICW-022 scope** to include settings-lifecycle acceptance criteria from ICW-P1-SETTINGS-SCOPE
8. **Expand ICW-021 scope** with concrete acceptance criteria for compositor-safe InteropBitmap reuse
9. **Expand ICW-134 scope** to explicitly name the `_pixelCost` mip-0-only defect and `ReleaseReservation` no-op counter

### What is deferred

- ICW-P1-COOPERATIVE-CANCEL and ICW-P1-GDI-CONCURRENCY — depend on Phase 0 coordinator fixes landing first
- ICW-P0-SPATIAL-INDEX-SAFETY — independent of coordinator work, can proceed in parallel but lower priority
- ICW-P0-MIGRATION-GUARD — process policy, not a code fix; document in ADR-0006 during ICW-P0-SEQUENCING
- ICW-132/ICW-133/ICW-144 — benchmark/stress work depends on a stable coordinator baseline

### What evidence gates must be passed

1. ICW-P0-ACTIVECOUNT: stress test proves `_activeCount` now represents physical execution during cancellation bursts
2. ICW-100: regression test proves `RenderRequestTracker` wiring survives re-application
3. ICW-P0-LEASE-RELEASE: leak-detection test proves exactly-once release semantics
4. ICW-102: render-fence test proves `DisposeDefectTemplatePools` is safe against concurrent in-flight render
5. ICW-P0-STALE-PUB: injection test proves stale tile completions are discarded

## Dissent

- **Phase ordering**: The Sequencing Reviewer recommends promoting ICW-P1-CLAIMANT-TOKENS to Phase 0 (it's a correctness/safety item, not a feature). The Architecture Reviewer agrees (without real claimant tokens, viewport culling cannot function). The Coordinator Reviewer notes this means Phase 0 scope expands by one task but the critical path remains serial.
- **ICW-P0-QUEUE-DRAIN phase paradox**: All seats agree the task must be split into Phase 0 (structural) and Phase 1 (liveness wiring) to resolve the paradox.

## Acceptance Criteria

1. All 16 ICW-P0-*/P1-* tasks have ticket files under `docs/tasks/tickets/` with proper front-matter
2. ICW-081 is complete — duplicate IDs are deduplicated, tracker data integrity is validated
3. ADR-0006 is updated with Phase 0/1/2+ milestones (ICW-P0-SEQUENCING)
4. ICW-P0-ACTIVECOUNT implementation is verified by stress test
5. ICW-143 dependency list includes all Phase 0 prerequisites

## Open Questions

1. Should ICW-081 (ticket deduplication) be treated as a pre-phase gate before any new tasks are created? Without it, the tracker cannot reliably represent the dependency chain.
2. Can the structural preparation for ICW-P0-QUEUE-DRAIN (Phase 0) and the liveness wiring (Phase 1) be implemented in separate commits, or does the liveness check naturally fall out of the structural change?
3. Should ICW-102 (defect-pool disposal with render fence) be promoted to Phase 0 given its race-condition safety nature?

## Task Modifications

### New tasks to create ticket files for

All ICW-P0-* and ICW-P1-* tasks need ticket files at `docs/tasks/tickets/ICW-P0-{NAME}.md` and `docs/tasks/tickets/ICW-P1-{NAME}.md`.

### Existing tasks needing scope expansion

| Task | Scope change |
|---|---|
| ICW-021 | Add concrete acceptance criteria: reproduction steps, synchronization strategy, regression test |
| ICW-022 | Add settings-lifecycle acceptance criteria: (a) settings not reset on RegenerateSceneAsync, (b) IsValid checks all bounds, (c) every field consumed |
| ICW-134 | Explicitly name `_pixelCost` mip-0-only defect; add `ReleaseReservation` no-op counter fix; add `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` test |

### Tasks needing dependency updates

| Task | Add dependency |
|---|---|
| ICW-143 | ICW-100, ICW-P0-STALE-PUB |
| ICW-P0-QUEUE-DRAIN | ICW-P1-CLAIMANT-TOKENS (structural: Phase 0; liveness: Phase 1) |
| ICW-P0-STALE-PUB | ICW-100 (share epoch mechanism) |
| ICW-P0-LEASE-RELEASE | ICW-P1-PIXELCOST-MIPS (lease must account for mip levels) |
| ICW-P0-TRANSACTIONAL-REGEN | ICW-102 (integrate with render disposal fence) |
| ICW-P0-BUFFER-REUSE-SYNC | ICW-021 (address same InteropBitmap compositor-handoff race) |
| ICW-143 | ICW-P0-SPATIAL-INDEX-SAFETY (viewport culling reads spatial index during publish) |
| ICW-P1-GDI-CONCURRENCY | ICW-P0-ACTIVECOUNT (neither sufficient alone) |
