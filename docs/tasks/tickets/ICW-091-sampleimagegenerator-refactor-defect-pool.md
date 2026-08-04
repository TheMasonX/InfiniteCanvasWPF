---
id: ICW-091
key: ICW-091
status: To Do
title: Simplify defect template pool creation and remove redundant casts
type: Task
priority: P3
tags: [rendering, micro-opt]
---

Summary
- Sonar flagged a redundant cast to `byte` and complexity in defect pool creation inside `SampleImageGenerator`.

Scope
- Audit `BuildDefectTemplatePool` and `CreateTemplateFromBitmap` code paths; remove unnecessary casts and simplify buffer copies where safe.

Validation
- `dotnet test` passes and code inspection shows removed redundant casts with no behavioral change.

Next step
- Implement small focused refactor in `SampleImageGenerator` and add a targeted unit test for template pool integrity.
