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

---

# Forked Status Update/Sprint Plan

## Viewport Requirements Assessment

Here is where we stand against the external auditor's requirements for a "reusable production viewport engine."

### What's in good shape (~70% done)

| Area | Status |
|---|---|
| **Coordinator infrastructure** | Sound. 19 unit tests, bounded concurrency, deduplication, claimant tracking, structured diagnostics. `_activeCount` now correctly represents physical execution. |
| **Mip generation (ICW-076)** | Mip contracts, eight-level ceiling, deterministic low-pass reduction, camera-selected raster sampling all work. Nonzero mip admission/caching is the remaining gap. |
| **Resident mip fallback (ICW-140/145/096)** | The render path returns the best available mip while new ones generate. Fallback ordering is correct. |
| **Zero-copy rendering** | Mature. `InteropBitmap` + Kernel32 file mapping, `Freeze()` marshalling, Gray8 direct path. |
| **Settings persistence (ICW-043)** | JSON versioned settings, save on close, malformed-file recovery. |

### What's partially done (~30% done)

| Area | Status |
|---|---|
| **Frame-level stale rejection (ICW-078)** | `RenderRequestTracker` epoch mechanism exists but **wiring was reverted** from `MainWindow.xaml.cs`. ICW-100 tracks re-application — it's 4 lines to put back. |
| **Cache accounting (ICW-134)** | Task scope is broad but `_pixelCost` mip-0-only defect and `ReleaseReservation` no-op counter are not yet explicitly named. |
| **Exception safety (ICW-014)** | Global handlers exist, EventLog fallback verified. But 19/21 async-void handlers have no try blocks — all silent swallowing with no user-visible signal. |
| **Architecture cleanup (ICW-018)** | 2 of 5 interfaces have zero implementers (`IRenderer`, `IBackgroundTileSource`). Decision pending. |

### What needs to be built (0% done, critical path)

These are the **external auditor's mandatory Phase 0/1 requirements** that must precede or accompany ICW-143 (viewport culling):

| Priority | Task | Status | Effort |
|---|---|---|---|
| **P0** | **ICW-P0-QUEUE-DRAIN** — `DrainQueue` must check claimant-token liveness before advancing past queued items | Open | Small |
| **P0** | **ICW-P1-CLAIMANT-TOKENS** — Wire real per-frame/viewport `CancellationToken` instead of `CancellationToken.None`. Make `DefaultCoordinatorClaimant` per-tile, not `static` | Open | Small-medium |
| **P0** | **ICW-100** — Re-apply ICW-078 `RenderRequestTracker` wiring (4 reverted lines) | Open | Trivial |
| **P0** | **ICW-P0-PIXELOMETER-READOUT** — Mouse hover must not trigger untracked tile generation outside `TileCacheBudget` | Open | Small (interim) |
| **P0** | **ICW-P0-TRANSACTIONAL-REGEN** — Failed/canceled regenerate must restore previous scene | Open | Medium |
| **P0** | **ICW-P0-LEASE-RELEASE** — Replace `ReleaseReservation` no-op counter with `IDisposable` lease | Open | Small |
| **P1** | **Background noise settings reset** — `InitializeSpatialState()` doesn't preserve `MainViewModel` across regenerate | Open | Trivial |
| **P1** | **ICW-P1-COOPERATIVE-CANCEL** — Mip factory must check cancellation token during expensive sub-phases, not just after | Open | Small |
| **P1** | **ICW-P1-GDI-CONCURRENCY** — Bounded GDI+ concurrency for tile generation (independent of coordinator's cap) | Open | Medium |
| **P1** | **ICW-P1-SETTINGS-VALIDATION** — Fix `ObjectsPerTile` `IsValid` gap; thread `MinimumSparseTilePixelSize` into render path | Open | Small |
| **P1** | **ICW-P1-PIXELCOST-MIPS** — `_pixelCost` should reflect all resident mips, not just mip-0 | Open | Small |
| **P1** | **ICW-P0-BUFFER-REUSE-SYNC** — `InteropBitmap` compositor handoff race (ICW-021 needs concrete scope) | Open | Medium |

### Overall completion estimate

```
Coordinator core (ICW-142)          ████████░░  80%  (with _activeCount fix just landed)
Safety harness (Phase 0)            █░░░░░░░░░  10%  (just did 1 of ~10 items)
Correctness (Phase 1)               ░░░░░░░░░░   0%  (all 6 items still open)
Viewport culling (ICW-143)          ░░░░░░░░░░   0%  (blocked by Phase 0/1)
Stress/benchmarks (ICW-144)         ░░░░░░░░░░   0%
Rendering pipeline                  █████░░░░░  50%
Settings/ViewModel                  ██░░░░░░░░  20%
Tests/benchmarks (existing)         ██████░░░░  60%
Documentation (ADRs, requirements)  ████░░░░░░  40%

OVERALL toward external audit       ███░░░░░░░  ~30-35%
```

### What to do next (highest ROI)

1. **ICW-P0-QUEUE-DRAIN** — Add token liveness check. Cheap, unblocks ICW-143 directly.
2. **ICW-100** — Re-apply the 4 reverted `RenderRequestTracker` lines. Trivial, blocks ICW-143.
3. **Background noise settings reset** — Move `MainViewModel` construction out of `InitializeSpatialState()`. Single-method fix, high user impact.
4. **ICW-P1-CLAIMANT-TOKENS** — Make claimant identity per-tile, wire real cancellation tokens. This plus #1 and #2 unblocks ICW-143.

The next ~1-2 days of focused work on those 4 items would bring us to the point where ICW-143 (viewport culling) can safely start.
