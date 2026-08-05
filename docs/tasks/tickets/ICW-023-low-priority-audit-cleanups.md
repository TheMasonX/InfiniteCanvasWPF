---
id: ICW-023-low-priority-audit-cleanups
key: ICW-023
title: Icw 023 Low Priority Audit Cleanups
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-023-low-priority-audit-cleanups

## Summary

- Status: To Do

## Scope

- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- The task has a clear implementation goal.
- The task is linked to the relevant files or design notes.
- The validation command and outcome are recorded.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: To be completed when implemented.

## Notes

- Add implementation details, blockers, or follow-up questions here.

## Audit Synthesis Batch (2026-08-04)

Add to this cleanup batch:

- `TryGetPixelValue` naming/restriction: it is a side-effectful read with a pure-sounding name and can start generation (finding C1-018).
- Pixelometer fallback allocation: list plus `OrderBy`/`ThenBy` under `_cacheGate` on the hover path (finding C2-020).
- Orphaned `GetClaimantIds()` with zero callers (finding C2-022).

Do NOT add the unused `sourceRow[sourceX * 3]` read here; it belongs to ICW-321 (same code region as the dead `DefectBitmap` sampling).

## Related Tasks

- ICW-000
