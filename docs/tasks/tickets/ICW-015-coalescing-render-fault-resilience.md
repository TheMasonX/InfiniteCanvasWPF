---
id: ICW-015-coalescing-render-fault-resilience
key: ICW-015
title: Make CoalescingAsyncAction resilient to action faults and preserve coalesced requests
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

Summary:
Ensure `_action` invocation in `CoalescingAsyncAction.ProcessAsync` is wrapped to catch exceptions and preserve coalesced requests that arrived while an action faulted.

Scope:
- `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs`
- Unit tests in `tests/InfiniteCanvas.Tests`

Acceptance criteria:
- Exceptions from `_action` are caught and routed to `_onActionFault` callback.
- Pending requests that arrive while an action faults are not dropped — they are serviced after the fault handling.
- Unit tests added to validate this behavior.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter CoalescingAsyncActionTests`

Estimated effort: Small
Risk: Low
Suggested owner: @core-team
