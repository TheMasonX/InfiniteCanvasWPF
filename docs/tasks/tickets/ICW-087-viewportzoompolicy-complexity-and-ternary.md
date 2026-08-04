---
id: ICW-087
key: ICW-087
status: To Do
title: Reduce cognitive complexity and simplify nested ternary in `ViewportZoomPolicy.cs`
type: Task
priority: P1
tags: [core, maintainability, brain-overload]
---

Summary
- `ViewportZoomPolicy.ComputeWheelDeltas` has high cognitive complexity (27) with nested ternary expressions. Sonar flagged the ternary at line ~33.

Scope
- Extract sub-expressions into named local functions/variables, replace nested ternary with clearer branching, and add unit tests covering boundary conditions for clamped/unclamped axes.

Validation
- `dotnet test` passes and static analysis reduces the complexity metric below threshold. Behavior remains identical according to existing `ViewportZoomPolicyTests`.

Next step
- Implement the refactor in a small patch, run tests, and verify no behavior regression for clamped recovery logic.
