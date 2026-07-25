---
id: ICW-030-generation-input-bounds-policy
key: ICW
title: Icw 030 Generation Input Bounds Policy
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

# ICW-030-generation-input-bounds-policy

## Summary

- Status: Done
- Generation now rejects `objectsPerTile` values above the explicit safety cap of 256.

## Scope

- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- The task has a clear implementation goal.
- The task is linked to the relevant files or design notes.
- The validation command and outcome are recorded.
- The same bound is enforced by both the generator and the runtime input validator.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: `runTests` on `SampleImageGeneratorTests.cs`: 13/13 passed.

## Notes

- The cap limits defect metadata and sparse image work while preserving zero as a valid
  no-annotation setting.

## Related Tasks

- ICW-000
