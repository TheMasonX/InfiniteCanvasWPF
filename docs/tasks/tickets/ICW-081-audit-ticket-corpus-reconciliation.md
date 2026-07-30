id: ICW-081
key: ICW-081
title: Reconcile duplicate and orphaned audit tickets before adding more backlog work
status: Done
type: Task
priority: P1
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-30
---

## Summary

The audit corpus is not a reliable de-duplication index. The current ticket directory contains duplicate numeric identities, multiple incompatible metadata shapes, and ICW ticket files that are absent from both live trackers.

## Scope

- Inventory every file under `docs/tasks/tickets`.
- Normalize identity, status, and required frontmatter without discarding useful findings.
- Merge or supersede duplicate concerns, including duplicate numeric identities such as ICW-065.
- Register, close, or explicitly archive orphaned ICW ticket files, including ICW-061, ICW-062, and ICW-063.
- Extend tracker validation to fail on duplicate task identities and malformed required metadata.

## Acceptance Criteria

- Every active ICW ticket has one canonical identity and exactly one tracker row.
- Duplicate ticket files either merge into a canonical ticket or clearly point to the canonical successor.
- Orphaned tickets have an explicit status and disposition.
- The validation script detects duplicate IDs and missing required metadata.
- The audit and task trackers no longer contain stale claims that contradict the current source without an explicit correction note.

## Validation

- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- A PowerShell inventory script that reports duplicate IDs and tracker orphans.

Current evidence: the inventory found 85 ticket files, a duplicate ICW-065 identity, and ICW-061/ICW-062/ICW-063 ticket IDs absent from the live trackers. Full reconciliation is pending.

## Notes

This is a process-integrity task, not permission to delete findings casually. Preserve substantive evidence while choosing one canonical ticket per concern. The current ICW-305 cache-policy ticket is also being registered in the live trackers because its code finding remains valid.

## Related Tasks

- ICW-036: CI and nullable-enforcement baseline can host the validation gate once the reconciliation is complete.
