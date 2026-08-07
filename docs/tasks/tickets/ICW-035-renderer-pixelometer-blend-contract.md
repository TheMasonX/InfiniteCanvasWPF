---
id: ICW-035-renderer-pixelometer-blend-contract
key: ICW-035
title: Unify renderer and pixelometer defect blending and sampling contract
status: Done
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
updated: 2026-08-07
---

# ICW-035 - Unify renderer and pixelometer defect blending and sampling contract

## Summary

The pixelometer and the defect overlay renderer were using different sampling assumptions. This task makes them share one explicit defect-overlay sampling contract so the readout matches what the user sees.

## Scope

- Review the defect overlay path in the renderer and the pixelometer readout.
- Introduce a shared sampler/helper for defect overlay values.
- Add regression coverage around overlay sampling semantics.
- Define overlap precedence as last applicable annotation wins.
- Verify the Windows renderer emits the same value that the sampler resolves.

## Acceptance Criteria

- The pixelometer and defect renderer use the same defect sampling helper.
- Defect values are resolved consistently for both matching and non-matching world coordinates.
- Overlapping annotations use last-applicable-wins precedence.
- A Windows pixel assertion proves the renderer and sampler agree.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~SampleImageTileTests"`
- Result: Core sampler coverage and focused Windows renderer assertions pass. The App Release build also passes.

## Notes

- The initial implementation routes both paths through a shared sampler in the rendering layer.
- Wave AC adds the renderer-level overlap assertion and aligns the source read result with the same sampler.

## Wave AC Update, 2026-08-07

The source read now reports defect values through `DefectOverlaySampler`, matching
the renderer's last-applicable-wins precedence. A Windows pixel regression renders
two overlapping annotations and compares the emitted Gray8 value with the sampler.

## Related Tasks

- ICW-014
- ICW-094
