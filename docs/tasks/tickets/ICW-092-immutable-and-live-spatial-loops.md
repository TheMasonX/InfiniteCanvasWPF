---
id: ICW-092
key: ICW-092
status: To Do
title: Replace manual loops with LINQ `.Where` in spatial index implementations where appropriate
type: Task
priority: P3
tags: [spatial, style]
---

Summary
- Sonar suggests using LINQ `.Where` to simplify loops in `ImmutableSpatialIndexService` and `LiveSpatialIndexService` query paths.

Scope
- Replace explicit iteration with `.Where(...).ToList()` or other low-allocation alternatives where readability wins without harming performance. Add microbenchmarks if necessary.

Validation
- Unit tests pass and performance regression is measured to be negligible for typical scene sizes.

Next step
- Submit a PR replacing the loops with concise LINQ calls, document performance assumptions in code comments.
