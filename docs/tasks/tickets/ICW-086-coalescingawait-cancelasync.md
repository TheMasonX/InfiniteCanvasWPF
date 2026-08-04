---
id: ICW-086
key: ICW-086
status: To Do
title: Replace `await CancelAsync` patterns with `CancelAsync` in async disposals and handlers
type: Task
priority: P2
tags: [async, reliability]
---

Summary
- SonarQube flagged `Await CancelAsync instead` warnings in `MainWindow.xaml.cs` and `CoalescingAsyncAction.cs` indicating misuse of cancellation patterns.

Scope
- Review flagged sites and follow the recommended `CancelAsync` invocation pattern where appropriate. Ensure cancellation semantics remain correct and add comments where behavior is intentional.

Validation
- Build and run tests; review any race conditions introduced by cancellation changes and add regression tests that assert disposal/cancellation ordering.

Next step
- Create a branch, update call sites, run `dotnet build` and tests, and open a PR referencing this ticket.
