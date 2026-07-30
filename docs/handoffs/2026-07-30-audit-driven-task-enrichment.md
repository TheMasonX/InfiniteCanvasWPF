# Handoff: Audit-Driven Task Description Enrichment

**Date:** 2026-07-30
**Source audits:**
- `docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md` (Deep-dive audit, commit `afa8b5b8`)
- `docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md` (Follow-up audit, Sprint 1 Waves A-D verification, commit `596fea64`)

## Summary

Reviewed both external audits against the current task tracker and ticket files. Updated the tracker and created detailed ticket files to capture the audits' implementation plans so future agents can implement them without re-deriving the analysis.

## Changes Made

### New ticket files created (11 files)

| Ticket | File | Description |
|---|---|---|
| ICW-P0-LEASE-RELEASE | `docs/tasks/tickets/ICW-P0-LEASE-RELEASE.md` | Replace `ReleaseReservation` counter with `IDisposable` lease pattern. Full implementation plan: ICacheReservation interface, route all cancel/fail paths, fix double-ReleaseReservation residual, leak-detection tests. |
| ICW-P1-PIXELCOST-MIPS | `docs/tasks/tickets/ICW-P1-PIXELCOST-MIPS.md` | Replace `_pixelCost` with sum of all resident mip payload bytes. Full plan: ResidentByteCount property, update TryReserve/Release, eviction policy adjustment. Must land together with ICW-P0-LEASE-RELEASE. |
| ICW-P0-TRANSACTIONAL-REGEN | `docs/tasks/tickets/ICW-P0-TRANSACTIONAL-REGEN.md` | Add transactional guard for `RegenerateSceneAsync` with fallback. Full plan: snapshot/rollback, try/catch around generation, integration test. Depends on ICW-102. |
| ICW-P0-BUFFER-REUSE-SYNC | `docs/tasks/tickets/ICW-P0-BUFFER-REUSE-SYNC.md` | Add synchronization or triple-buffering for InteropBitmap compositor handoff. Two fix options documented. |
| ICW-P1-COOPERATIVE-CANCEL | `docs/tasks/tickets/ICW-P1-COOPERATIVE-CANCEL.md` | Add cancellation checks in tile generation factories. Token parameter to all expensive generator methods, ThrowIfCancellationRequested checks, injection test. |
| ICW-P1-GDI-CONCURRENCY | `docs/tasks/tickets/ICW-P1-GDI-CONCURRENCY.md` | Add explicit GDI+ concurrency management. Two options: SemaphoreSlim serialization (recommended) or dedicated worker thread. Stress test. |
| ICW-P1-SETTINGS-VALIDATION | `docs/tasks/tickets/ICW-P1-SETTINGS-VALIDATION.md` | Single validation function per option field. Fixes ObjectsPerTile upper bound, MinimumSparseTilePixelSize threading, duplicate validation in TryReadGenerationOptions. |
| ICW-P0-ACTIVECOUNT-residuals | `docs/tasks/tickets/ICW-P0-ACTIVECOUNT-residuals.md` | Close two residual issues from ICW-P0-ACTIVECOUNT fix: double ReleaseReservation call, duplicate-admission-during-cancel race. Must land before ICW-P0-LEASE-RELEASE. |
| ICW-P0-MIGRATION-GUARD | `docs/tasks/tickets/ICW-P0-MIGRATION-GUARD.md` | Add concurrent-development policy for 8-phase migration. Freeze feature work during P0 or strangler-fig pattern. |
| ICW-P0-SEQUENCING | `docs/tasks/tickets/ICW-P0-SEQUENCING.md` | Restructure ICW-141 epic with Phase 0/1/2+ milestones. Complete phase inventory with Done/Proposed status per item. |
| ICW-101-annotation-tooltip-presenter-restore | `docs/tasks/tickets/ICW-101-annotation-tooltip-presenter-restore.md` | Restore tooltip to use AnnotationFeaturePresenter.BuildTooltipContent. Cheapest fix in the audit — independent of everything else. |
| ICW-102-defect-bitmap-pool-disposal | `docs/tasks/tickets/ICW-102-defect-bitmap-pool-disposal.md` | Implement owned disposal of bitmap pool with concurrency guard. Add WaitForIdleAsync to CoalescingAsyncAction. |
| ICW-110-audit-async-void-safety-detailed | `docs/tasks/tickets/ICW-110-audit-async-void-safety-detailed.md` | Detailed implementation plan for async void handler audit and migration. |

### Updated ticket files (6 files)

| Ticket | Changes |
|---|---|
| ICW-029 | Replaced template with full shutdown lifecycle race plan: gate-wait before disposal, close-stress test, dependency on ICW-P0-TRANSACTIONAL-REGEN. |
| ICW-021 | Replaced template with full buffer-reuse-safety plan: confirmed mechanism from audit, linked to ICW-P0-BUFFER-REUSE-SYNC as concrete implementation. |
| ICW-018 | Expanded scope with precise dead-code inventory from audit: IRenderer, ViewportRenderRequest, IBackgroundTileSource, MipOptions (all zero references). Dead GenerateAnnotations. Specific delete/keep recommendations. |
| ICW-060 | Marked as Deprecated — the specific "mutable STRtree list exposure" defect was already fixed at HEAD. ICW-P0-SPATIAL-INDEX-SAFETY confirmed. |
| ICW-099 | Marked as Deprecated — SerilogHost.CreateLogger already wraps EventLog in try/catch at HEAD. Residual verification step documented. |
| ICW-022 | Expanded with Phase 1 compatibility acceptance criteria from audit. Identified which items are DONE vs still open. |
| ICW-031 | Full typed-metrics migration plan. Dependencies on ICW-101. Specific call sites listed. |
| ICW-134 | Updated to reference sub-tickets (ICW-P0-LEASE-RELEASE, ICW-P1-PIXELCOST-MIPS) instead of containing duplicate scope. |

### active-tasks.md changes

1. **Status corrections**: ICW-060 → Deprecated, ICW-099 → Deprecated.
2. **New rows added**: ICW-P0-ACTIVECOUNT-residuals, ICW-P0-SEQUENCING, ICW-P0-MIGRATION-GUARD.
3. **Row descriptions enriched**: All P0/P1 rows now include confidence scores from audits, exact mechanism descriptions, and ticket file references.
4. **Duplicate ID fixes**: ICW-100 (3 of 4 duplicates reassigned: overlay precedence retained, reconcile-IDs moved to ICW-081, decompose-MainWindow moved to ICW-022). ICW-098 duplicate (scrollbar slice) assigned ICW-098-scrollbar. ICW-102 duplicate (defect bitmap) consolidated. ICW-099 duplicate (MinSparseTilePixelSize) marked as subsumed by ICW-P1-SETTINGS-VALIDATION. ICW-014 duplicate removed. ICW-111 (was ICW-102 duplicate for typed metrics) linked to ICW-031.
5. **ICW-023 expanded scope**: Added MipOptions dead code, dead GenerateAnnotations, TileGridIndexLookup checked overflow from audit findings.

## Remaining Tracker Issues

1. **ICW-081** (duplicate ID reconciliation) is still Proposed. This handoff fixed the most confusing duplicates in active-tasks.md, but ticket files under `docs/tasks/tickets/` still have duplicate filenames (e.g., ICW-100 appears 4 times with different content). A mechanical script pass is needed.
2. **JIRA.md** (`docs/tasks/JIRA.md`) was not updated in this pass — it still has stale statuses.
3. **Scripts/Validate-TaskTracker.ps1** (ICW-084) was not updated — still needs cognitive complexity reduction and duplicate-ID validation.

## Next Step Recommendations

1. **Implement ICW-101 first** (tooltip presenter restore) — cheapest fix, independent of everything else, eliminates a crash vector.
2. **Land ICW-P0-ACTIVECOUNT-residuals** before ICW-P0-LEASE-RELEASE (removes double-dispose hazard).
3. **Implement ICW-P0-LEASE-RELEASE + ICW-P1-PIXELCOST-MIPS together** (same accounting correctness problem, interdependent).
4. **Update JIRA.md** to match active-tasks.md statuses.
5. **Run ICW-084** (Validate-TaskTracker.ps1 refactor) before creating more ticket files.

## Validation

```
dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
dotnet test tests/InfiniteCanvas.Tests --configuration Release
```

No code changes in this handoff — documentation only. Build and tests should be unaffected.
