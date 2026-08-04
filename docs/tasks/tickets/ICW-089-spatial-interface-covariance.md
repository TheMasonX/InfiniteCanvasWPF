---
id: ICW-089
key: ICW-089
status: To Do
title: Review `ISpatialIndexService<T>` variance and query contract
type: Task
priority: P2
tags: [spatial, api-design]
---

Summary
- Sonar suggests making `T` covariant with `out` in `ISpatialIndexService<T>` to improve API flexibility. Evaluate whether `Query` semantics remain type-safe.

Scope
- Audit implementations (`ImmutableSpatialIndexService`, `LiveSpatialIndexService`) for compatibility with covariance, update interface if safe, and add interface-level unit tests.

Validation
- Interface updated to `ISpatialIndexService<out T>` only if implementations do not rely on T in input positions; all builds and tests pass.

Next step
- Create an analysis PR that adds the `out` variance if safe, otherwise document rationale in an ADR and suppress Sonar rule with a targeted justification.
