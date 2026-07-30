---
id: ICW-P0-SEQUENCING
author: External Audit (Integration-1)
key: ICW-P0-SEQUENCING
title: Restructure ICW-141 epic with Phase 0 and Phase 1 milestones
status: Proposed
type: Task
priority: P0
tags:
  - process
  - planning
  - epic
  - sequencing
dependsOn: []
related:
  - ICW-141
  - ADR-0006
  - ICW-P0-MIGRATION-GUARD
links:
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-SEQUENCING — Restructure ICW-141 epic with Phase 0 and Phase 1 milestones

## Summary

The ICW-141 epic (viewport-aware tile work scheduling) was delivered as ICW-142 → ICW-143 → ICW-144 without a dedicated Phase 0 safety harness. The external audit requires restructuring into explicit phases so the execution order is clear to future agents.

**Current state:** ICW-142 (Done), ICW-143 (Done), ICW-144 (Proposed). P0/P1 safety items were delivered as side-batches in Sprint 1 Waves A-D but are not tracked as an explicit phase in the epic.

**Required:** Restructure the epic into three phases and update ADR-0006 to reflect the phase structure. Mark ICW-141 as Done after restructuring. Create a new epic or meta-task for the P0/P1 items that remain.

## Phase Structure

### Phase 0 — Safety Harness (P0 items)
Status: Partially done (Waves A-D), partially open.

| ID | Status | Summary |
|---|---|---|
| ICW-P0-ACTIVECOUNT | Done | Worker-termination-path decrement (Wave A) |
| ICW-P0-ACTIVECOUNT-residuals | **Proposed** | Double-ReleaseReservation + duplicate-admission race |
| ICW-P0-QUEUE-DRAIN | Done | Claimant-liveness check (Waves A+B) |
| ICW-P0-STALE-PUB | Done | Stale publication guard (Wave C) |
| ICW-P0-SPATIAL-INDEX-SAFETY | Done | Immutable Query results (Wave C) |
| ICW-P0-PIXELOMETER-READOUT | Done | Cache budget wired through pixelometer (Wave B) |
| ICW-P0-LEASE-RELEASE | **Proposed** | Replace counter with IDisposable lease |
| ICW-P0-TRANSACTIONAL-REGEN | **Proposed** | Transactional RegenerateSceneAsync |
| ICW-P0-BUFFER-REUSE-SYNC | **Proposed** | Compositor sync or triple-buffering |
| ICW-P0-MIGRATION-GUARD | **Proposed** | Concurrent-development policy |

### Phase 1 — Correctness (P1 items)
Status: Partially done, partially open.

| ID | Status | Summary |
|---|---|---|
| ICW-P1-CLAIMANT-TOKENS | Done | Real per-frame tokens (Wave B) |
| ICW-P1-COOPERATIVE-CANCEL | **Proposed** | Cancellation checks in generators |
| ICW-P1-GDI-CONCURRENCY | **Proposed** | GDI+ concurrency management |
| ICW-P1-PIXELCOST-MIPS | **Proposed** | Mip-aware byte accounting |
| ICW-P1-SETTINGS-VALIDATION | **Proposed** | Shared validation functions |
| ICW-P1-SETTINGS-SCOPE | **Proposed** | Settings-bug acceptance criteria |

### Phase 2+ — Viewport Scheduling (original ICW-142/143/144 scope)
Status: ICW-142 (Done), ICW-143 (Done), ICW-144 (Proposed).

| ID | Status | Summary |
|---|---|---|
| ICW-142 | Done | Bounded cancellable materialization |
| ICW-143 | Done | Viewport culling and priority |
| ICW-144 | Proposed | Fast-scroll stress benchmarks |

## Scope

### Required Changes

1. **Update ADR-0006**: replace the current flat implementation sequence with the three-phase structure above. Include the P0/P1 sequencing table and note that ICW-P0-MIGRATION-GUARD's policy applies during Phases 0-1.

2. **Update ICW-141 ticket file**: change status to Done, reference the restructured phases, add a note that P0/P1 items are tracked as separate ICW-P0-* and ICW-P1-* tickets.

3. **Ensure no gap between phases**: Phase 0 items that are "Proposed" must be explicitly sequenced relative to each other (see individual ticket dependencies).

### Acceptance Criteria

- ADR-0006 documents the three-phase structure with completion criteria for each phase.
- ICW-141 ticket references the restructured phases and points to ICW-P0-* and ICW-P1-* tickets for remaining work.
- A future agent can determine at a glance which phase is active and what the next step is.

## Files to Change

| File | Change |
|---|---|
| `docs/ADR/0006-viewport-aware-tile-work-scheduling.md` | Replace flat sequence with three-phase structure |
| `docs/tasks/tickets/ICW-141-viewport-aware-tile-work-scheduling.md` | Update status to Done, reference phases |
| `docs/tasks/active-tasks.md` | Ensure phase structure is visible |

## Validation

Manual review: read ADR-0006 and confirm the phase structure is complete and consistent.

## Related Tasks

- ICW-141: parent epic (update to Done)
- ICW-P0-MIGRATION-GUARD: concurrent-development policy for executing this sequence
- ADR-0006: parent ADR (update with phase structure)
