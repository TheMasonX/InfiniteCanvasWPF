---
id: ICW-084
key: ICW-084
status: To Do
title: Refactor `scripts/Validate-TaskTracker.ps1` to reduce cognitive complexity
type: Task
priority: P1
tags: [scripts, maintainability]
---

Summary
- The PowerShell validation script `scripts/Validate-TaskTracker.ps1` contains a function whose cognitive complexity exceeds the repository threshold (22 vs 15). Refactor to improve readability and testability.

Scope
- Extract smaller helper functions for file enumeration, front-matter parsing, and field validation. Add early-return guards and Pester tests for edge cases.

Validation
- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks` returns success and a small Pester test suite covers the refactored functions.

Next step
- Create a short-lived branch, implement helpers, add tests, and replace the complex function with an orchestration routine.
