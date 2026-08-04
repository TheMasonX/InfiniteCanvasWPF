---
id: ICW-190
key: ICW-190
status: To Do
title: Extract `AnnotationGenerator` from `SampleImageGenerator`
type: Task
priority: P3
tags: [rendering, design, decomposition]
---

Summary
- Move annotation generation logic out of `SampleImageGenerator.GenerateSet` into a focused `AnnotationGenerator` class to reduce `SampleImageGenerator` responsibility and improve testability.

Scope
- Create `src/InfiniteCanvas.Rendering/AnnotationGenerator.cs` with a public `GenerateAnnotations(tileId, tileBounds, count, DeterministicRandom, DefectTemplatePool)` API, update `SampleImageGenerator` to call it, and add unit tests for deterministic outputs.

Validation
- `AnnotationGenerator` unit tests cover deterministic id, bounds, and feature fields for a few fixed seeds; `dotnet test` remains green.

Next step
- Implement `AnnotationGenerator`, move the logic and tests, and run the core test suite. Keep behavior identical and converge on a clean API shape.
