---
id: TESTS-001-coalescing-tests-determinism
key: TESTS-001
title: Make CoalescingAsyncAction tests deterministic and avoid Timeout.InfiniteTimeSpan
status: Proposed
type: Task
priority: P2
tags:
  - tests
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Summary:
Replace `Task.Delay(Timeout.InfiniteTimeSpan)` in tests with `TaskCompletionSource`-driven waits and explicit per-test timeouts to avoid CI hangs and flakiness.

Scope:
- `tests/InfiniteCanvas.Tests/CoalescingAsyncActionTests.cs`

Acceptance criteria:
- Tests no longer use infinite delays; they terminate deterministically even on failure.
- Tests include explicit timeout attributes or harness-level timeouts.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter CoalescingAsyncActionTests`

Estimated effort: Small
Risk: Low
Suggested owner: @test-maintainer
