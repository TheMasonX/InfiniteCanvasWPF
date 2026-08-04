---
id: ICW-191
key: ICW-191
status: To Do
title: Extract `DefectTemplateFactory` and simplify defect-pool creation
type: Task
priority: P3
tags: [rendering, refactor, allocation]
---

Summary
- Encapsulate defect-template pool creation into a `DefectTemplateFactory` to isolate bitmap/byte-array creation, centralize any platform differences, and make disposal/ownership explicit.

Scope
- Add `src/InfiniteCanvas.Rendering/DefectTemplateFactory.cs` exposing `Build(count, DeterministicRandom)` returning `IReadOnlyList<DefectTemplate>`. Move `CreateBitmapFromPixels` and platform-specific bits under the factory so disposal policy is explicit.

Validation
- Unit tests assert pool size, template dimension ranges, and pixel statistics for a fixed seed. Ensure no behavioral change in `GenerateSet` when the factory is used.

Next step
- Implement the factory, wire it into `GenerateSet`, and add a follow-up ticket to address bitmap disposal lifecycle if needed (eviction/dispose ownership).
