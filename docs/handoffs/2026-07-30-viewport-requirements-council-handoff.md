# Handoff: Viewport Requirements Council Review — Adapted MemorySmith Process

**Date:** 2026-07-30
**Previous handoff:** 2026-07-30-external-audit-synthesis-handoff.md
**HEAD:** 139a8b62fa2d6363615eb6a819d07a76aa8c55c2

## Council Process Adapted

Adapted the MemorySmith LLM Council peer review process to InfiniteCanvasWPF. The MemorySmith council uses 6 seats (Source-Grounded Archivist, Data Model Architect, Retrieval Specialist, Human Learning Advocate, Skeptical Reviewer, Synthesizer) with structured evidence review and dissent recording.

Adapted for this repo as 4 seats:
- **Viewport Architecture Reviewer** — maps viewport requirements to tasks
- **Coordinator/Concurrency Reviewer** — maps coordinator/concurrency requirements to tasks
- **Settings/Persistence/MVVM Reviewer** — maps settings/UI requirements to tasks
- **Implementation Sequencing Reviewer** — validates task sequencing, dependencies, and statuses

The adapted process retains: evidence-first approach, seat-by-seat findings with confidence, explicit dissent, acceptance criteria, and open questions. Changes from MemorySmith original: reduced from 6 to 4 seats (fits our subsystem boundaries), no skill-hook evidence bundle script, and subagents used with explicit permission (per the council skill's own policy).

## Key Council Findings

### 1. Requirement-to-Task Coverage Is Critically Incomplete

| Reviewer | Requirements Mapped | Full Coverage | Partial Coverage | Zero Coverage |
|---|---|---|---|---|
| Viewport Architecture | 22 | 0 | 7 | 15 |
| Coordinator/Concurrency | 12 | 2 | 2 | 8 |
| Settings/Persistence/MVVM | 16 | 10 | 0 | 6 |
| **Total** | **50** | **12** | **9** | **29** |

### 2. Critical Path Identified (4 serial steps)

ICW-P0-ACTIVECOUNT → ICW-P1-CLAIMANT-TOKENS → ICW-P0-QUEUE-DRAIN (Phase 1 portion) → ICW-143

Every step is Proposed or To Do. None has implementation started.

### 3. Phase 0 Paradox Resolved

ICW-P0-QUEUE-DRAIN was labeled Phase 0 but depends on ICW-P1-CLAIMANT-TOKENS (Phase 1). Resolution: split into structural preparation (Phase 0 — method skeleton + tests with CancellationToken.None placeholder) and liveness wiring (Phase 1 — real token check after ICW-P1-CLAIMANT-TOKENS lands).

### 4. 6 Duplicate ICW IDs Corrupt Tracker Data Integrity

ICW-100 appears 4 times, ICW-102 appears 3 times, ICW-094/ICW-014/ICW-098/ICW-099 each appear twice. ICW-081 (ticket reconciliation) must precede any new task creation.

### 5. 7 Missing Dependency Links Added

Between: ICW-P0-QUEUE-DRAIN ↔ ICW-P1-CLAIMANT-TOKENS, ICW-P0-STALE-PUB ↔ ICW-100, ICW-P0-LEASE-RELEASE ↔ ICW-P1-PIXELCOST-MIPS, ICW-P0-TRANSACTIONAL-REGEN ↔ ICW-102, ICW-P0-BUFFER-REUSE-SYNC ↔ ICW-021, ICW-143 ↔ ICW-P0-SPATIAL-INDEX-SAFETY, ICW-P1-GDI-CONCURRENCY ↔ ICW-P0-ACTIVECOUNT

## Scope Changes Applied

| Task | Change |
|---|---|
| ICW-143 | Added dependencies: ICW-100, ICW-P0-STALE-PUB, ICW-P0-SPATIAL-INDEX-SAFETY |
| ICW-P0-QUEUE-DRAIN | Split into Phase 0 (structural) + Phase 1 (liveness) |
| ICW-P0-STALE-PUB | Added dependency: ICW-100 (share epoch mechanism) |
| ICW-100 | Added link: ICW-P0-STALE-PUB (share epoch mechanism) |
| ICW-021 | Added link: ICW-P0-BUFFER-REUSE-SYNC; added concrete acceptance criteria scope |
| ICW-022 | Expanded acceptance criteria: settings not reset on RegenerateSceneAsync, IsValid checks all bounds, every field consumed |
| ICW-134 | Explicitly named: `_pixelCost` mip-0-only defect, `ReleaseReservation` no-op counter; added dependency on ICW-P0-LEASE-RELEASE |
| ICW-P0-LEASE-RELEASE | Added dependency: ICW-P1-PIXELCOST-MIPS |
| ICW-P0-TRANSACTIONAL-REGEN | Added dependency: ICW-102 (render disposal fence) |
| ICW-P0-BUFFER-REUSE-SYNC | Added link: ICW-021 |
| ICW-P1-GDI-CONCURRENCY | Added dependency: ICW-P0-ACTIVECOUNT |

## Still Needed (next sprint)

1. **Create ticket files** for all 16 ICW-P0-* and ICW-P1-* tasks under `docs/tasks/tickets/`
2. **ICW-081**: Execute ticket deduplication before creating any new tasks
3. **ADR-0006 update**: Phase 0/1/2+ milestones, migration guard policy, dependency documentation
4. **Update `scripts/Validate-TaskTracker.ps1`**: Add duplicate-ID detection in active-tasks.md
5. **Promote ICW-P0-ACTIVECOUNT to To Do** — highest priority unblocked task

## Recommended Next Step

Start ICW-P0-ACTIVECOUNT (fix `_activeCount` timing — move decrement to worker `finally`). This is the first task on the critical path and unblocks everything else. Create its ticket file first, then proceed with the remaining 15.
