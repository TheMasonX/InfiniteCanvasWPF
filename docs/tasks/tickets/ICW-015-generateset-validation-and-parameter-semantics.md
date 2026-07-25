---
id: ICW-015-generateset-validation-and-parameter-semantics
author: Copilot
key: ICW
title: Icw 015 Generateset Validation And Parameter Semantics
status: Proposed
type: Task
priority: P2
tags:
  - task-tracker
  - icw
  - backlog
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-015-generateset-validation-and-parameter-semantics

## Summary

- Status: Done
- `GenerateSet` validates `objectsPerTile` against the shared safety cap.
- Explicit `rows` layouts require `imageCount == columns * rows`.

## Scope

- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- The task has a clear implementation goal.
- The task is linked to the relevant files or design notes.
- The validation command and outcome are recorded.
- Invalid generation values identify the responsible parameter.
- Explicit row semantics cannot silently discard `imageCount`.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: `runTests` on `SampleImageGeneratorTests.cs`: 13/13 passed.

## Notes

- `SampleImageGenerator.MaxObjectsPerTile` is the single policy constant used by the
  generator and mirrored by the MainWindow input validator.

## Related Tasks

- ICW-000
