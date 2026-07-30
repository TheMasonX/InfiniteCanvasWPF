---
id: ICW-P0-MIGRATION-GUARD
author: External Audit (Integration-1)
key: ICW-P0-MIGRATION-GUARD
title: Add concurrent-development policy for 8-phase migration
status: Proposed
type: Task
priority: P0
tags:
  - process
  - migration
  - policy
  - coordination
dependsOn: []
related:
  - ICW-P0-SEQUENCING
  - ADR-0006
links:
  - docs/ADR/0006-viewport-aware-tile-work-scheduling.md
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-MIGRATION-GUARD — Add concurrent-development policy for 8-phase migration

## Summary

**Process gap:** The codebase is under rapid active iteration with multiple agents making changes concurrently. There is no documented strategy for managing concurrent feature work during the P0/P1 migration phases. Without a guard, two agents could independently modify the same subsystem (e.g., `TileWorkCoordinator` or `SampleImageTile`) with conflicting changes, or one agent could add new features that depend on contracts that another agent is in the middle of changing.

**Confidence:** N/A (process finding, not code defect).

## Scope

### Required Artifacts

1. **Add explicit policy to ADR-0006** (viewport-aware tile work scheduling):

   **Option A — Feature freeze during Phases 1-2:**
   - Freeze all demo-app feature work (annotation display modes, settings UI, overlay controls) while P0 safety fixes land.
   - Only P0/P1 correctness and safety work is permitted.
   - Unblocks: ICW-P0-LEASE-RELEASE, ICW-P0-TRANSACTIONAL-REGEN, ICW-P0-BUFFER-REUSE-SYNC, ICW-P1-COOPERATIVE-CANCEL, ICW-P1-GDI-CONCURRENCY.
   - Blocked: ICW-007, ICW-019, ICW-028, ICW-037, ICW-070, ICW-071, ICW-077.

   **Option B — Strangler-fig pattern:**
   - New feature work targets emerging contracts (e.g., new `ICacheReservation` API) rather than the old ones being replaced.
   - Old contracts remain in place until all consumers have migrated.
   - More flexible but requires discipline and cross-references in PRs.

   **Recommendation:** Option A for the P0 items (2-3 sprint waves), then Option B for P1 items (lower risk of breaking active feature work).

2. **Update `active-tasks.md`** with a migration-state row:
   - Current phase (e.g., "P0 safety harness")
   - What work is permitted (e.g., "P0/P1 coordinator, cache, and lifecycle fixes only")
   - What work is deferred (e.g., "New viewport interaction features, overlay animations, settings UI expansion")

3. **Add a checklist to PR template** (or documented process):
   - Does this change touch `TileWorkCoordinator`, `SampleImageTile`, `MainWindow.xaml.cs` (render/generation section), `TileCacheBudget`, or `ZeroCopyBitmapFactory`?
   - If yes, is the concurrent-development policy current phase respected?
   - If working on a contract being migrated (e.g., `ICacheReservation`), are old consumers still supported?

### Acceptance Criteria

- ADR-0006 documents the concurrent-development policy for the current migration.
- The migration state is visible in `active-tasks.md` or a dedicated tracking doc.
- Any agent working on the repository can determine at a glance which subsystems are under active migration and what work is permitted.

## Files to Change

| File | Change |
|---|---|
| `docs/ADR/0006-viewport-aware-tile-work-scheduling.md` | Add concurrent-development policy section |
| `docs/tasks/active-tasks.md` | Add migration-state row |

## Validation

Manual review: read ADR-0006 and confirm policy is documented and consistent with the current `active-tasks.md` state.

## Related Tasks

- ICW-P0-SEQUENCING: epic restructuring (this ticket documents the policy for executing that sequence)
- ADR-0006: parent ADR (update with policy)
