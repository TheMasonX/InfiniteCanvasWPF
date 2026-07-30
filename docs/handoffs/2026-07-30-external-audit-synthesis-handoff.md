# Handoff: External Audit Synthesis — Validated Findings and Master Task Plan

**Date:** 2026-07-30
**Previous handoff:** 2026-07-27-tile-generation-stability-sprint.md
**HEAD:** 139a8b62fa2d6363615eb6a819d07a76aa8c55c2

## Summary

4 parallel subagents reviewed 12 audit files (11 markdown, 4 .docx via cross-referencing audit documents) against current code at HEAD 139a8b6. Produced a validated master list of 53 findings (10 High, 13 Medium, 15 Low/Info, 1 verified fixed, 9 adequately tracked, 8 partially tracked, 12 untracked). Created 16 proposed new tasks (10 Phase 0 safety harness, 6 Phase 1 correctness) plus scope updates to 12 existing tasks.

## Key Findings

### Critical (must fix before ICW-143 starts)

1. **Coordinator concurrency accounting is wrong** (3 findings, 0 tracked):
   - `_activeCount` decremented before physical work stops — concurrency cap is not real during cancellation bursts
   - Claimant tokens hardcoded to `CancellationToken.None` — auto-removal path is dead code
   - `DrainQueue` does not check claimant-token liveness — stale items block usable ones
   - Two independent audits confirm these defects at HEAD

2. **Pixelometer bypasses cache budget** (1 finding, 0 tracked):
   - `TryReadPixelValue` triggers untracked, unrecoverable tile generation outside `TileCacheBudget`
   - Generated tiles from hover cannot be evicted

3. **Background noise settings silently reset** (1 finding, 0 tracked):
   - `InitializeSpatialState()` creates new `MainViewModel` on every `RegenerateSceneAsync`
   - Breaks just-shipped ICW-066/ICW-067 feature on its primary user action

4. **ICW-078/RenderRequestTracker wiring absent for 19+ commits** (1 finding, ICW-100):
   - Stale-frame publication guard missing; ICW-078 incorrectly marked Done
   - Directly blocks ICW-143 viewport culling

5. **Defect-pool dispose races in-flight render** (1 finding, ICW-102):
   - `DisposeDefectTemplatePools` has no synchronization against render
   - `CancelAll()` targets coordinator, not render pipeline

### Existing tasks needing status/scope updates

- **ICW-078**: Done → In Progress (wiring reverted, never re-applied)
- **ICW-100**: Proposed → To Do (tracks ICW-078 re-application)
- **ICW-141**: Proposed → In Progress (restructure with Phase 0/1)
- **ICW-134**: Expand scope for `_pixelCost` mip-0-only defect + `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` test
- **ICW-018**: Expand scope for `IBackgroundTileSource` alongside `IRenderer`
- **ICW-014**: Add user-visible signal for dispatcher exceptions
- **ICW-102**: Add render fence requirement for `DisposeDefectTemplatePools`
- **ICW-021**: Add concrete acceptance criteria (was template-only)
- **ICW-023**: Add TileGridIndexLookup overflow, dead diagnostic properties, redundant parameter
- **ICW-031/111**: Add `CreateAnnotationToolTip` as migration target

### Audit file corrections needed

- `infinitecanvaswpf-pass7-audit-26-07-27-19-13-25.md` §2: `BackgroundTileReadoutInfo` IS used (3 sites in MainWindow.xaml.cs + tests). Correction: the overall finding about unused scaffolding remains valid for the other 4 types, but the ReadoutInfo row is wrong.

## Proposed New Task Structure

### Phase 0: Safety Harness (before architectural refactoring)
- ICW-P0-STALE-PUB, ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN
- ICW-P0-PIXELOMETER-READOUT, ICW-P0-TRANSACTIONAL-REGEN
- ICW-P0-BUFFER-REUSE-SYNC, ICW-P0-SPATIAL-INDEX-SAFETY
- ICW-P0-LEASE-RELEASE, ICW-P0-SEQUENCING, ICW-P0-MIGRATION-GUARD

### Phase 1: Correctness
- ICW-P1-CLAIMANT-TOKENS, ICW-P1-COOPERATIVE-CANCEL
- ICW-P1-GDI-CONCURRENCY, ICW-P1-SETTINGS-VALIDATION
- ICW-P1-PIXELCOST-MIPS, ICW-P1-SETTINGS-SCOPE

## Documents Updated

- `docs/audits/external-audit-master-synthesis-26-07-30.md` — comprehensive master report
- `docs/audits/external-audit-requirements-synthesis-26-07-30.md` — mandatory requirements traceability matrix
- `docs/tasks/active-tasks.md` — status corrections and 18 new task entries
- `docs/requirements/functional-requirements-and-invariants.md` — 14 new external-audit mandatory requirements

## Documents Still Needing Updates (next sprint)

- `docs/ADR/0006-viewport-aware-tile-work-scheduling.md` — restructure sequencing, add Phase 0/1 mandates
- `docs/ADR/0005-source-agnostic-background-tile-mips.md` — add mip memory accounting requirement
- `DesignDoc.md` — add concurrency invariant for `_activeCount`
- `docs/audits/infinitecanvaswpf-pass7-audit-26-07-27-19-13-25.md` — correct `BackgroundTileReadoutInfo` reference count

## Recommended Next Step

1. Review the master task list in `docs/audits/external-audit-master-synthesis-26-07-30.md`
2. Prioritize Phase 0 tasks and begin ICW-P0-ACTIVECOUNT (fixes `_activeCount` timing, unblocks everything else)
3. Fix the background noise settings reset (single-method change, high UX impact)
4. Update ICW-078 status and re-apply RenderRequestTracker wiring (ICW-100)
5. Update ADR-0006 with Phase 0/1 sequencing mandates
