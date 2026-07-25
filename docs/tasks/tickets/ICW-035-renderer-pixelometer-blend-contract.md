---
id: ICW-035-renderer-pixelometer-blend-contract
key: ICW-035
title: Unify renderer and pixelometer defect blending and sampling contract
status: In Progress
type: Task
priority: P2
tags:
  - icw
  - task-tracker
  - rendering
  - pixelometer
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-035 - Unify renderer and pixelometer defect blending and sampling contract

## Summary

The pixelometer and the defect overlay renderer were using different sampling assumptions. This task makes them share one explicit defect-overlay sampling contract so the readout matches what the user sees.

## Scope

- Review the defect overlay path in the renderer and the pixelometer readout.
- Introduce a shared sampler/helper for defect overlay values.
- Add regression coverage around overlay sampling semantics.

## Acceptance Criteria

- The pixelometer and defect renderer use the same defect sampling helper.
- Defect values are resolved consistently for both matching and non-matching world coordinates.
- Regression tests prove the contract is stable.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~SampleImageTileTests"`
- Result: Passed (9/9 tests, 0 failures)

## Notes

- The initial implementation now routes both paths through a shared sampler in the rendering layer.
- Follow-up work can extend this to a full renderer-level pixel assertion if the overlay path is exercised more broadly.

## Related Tasks

- ICW-014
- ICW-094
