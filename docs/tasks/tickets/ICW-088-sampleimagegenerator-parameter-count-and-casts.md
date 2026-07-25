---
id: ICW-088
status: To Do
title: Reduce parameter count and unnecessary casts in `SampleImageGenerator` methods
type: Task
priority: P3
tags: [rendering, api-design, clumsy]
---

Summary
- Sonar flagged `SampleImageGenerator` methods with high parameter counts (up to 11) and unnecessary casts to `byte`.

Scope
- Consolidate related parameters into small configuration objects (e.g., `GeneratorOptions`), remove redundant casts, and add unit tests.

Validation
- Public generator API is preserved or updated with a clear migration path; `dotnet build` and unit tests pass.

Next step
- Propose a small API wrapper type `GeneratorOptions` and migration GUID; implement in a single PR to minimize churn.
