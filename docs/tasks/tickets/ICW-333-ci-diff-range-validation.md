---
id: ICW-333-ci-diff-range-validation
key: ICW-333
title: Make CI whitespace validation inspect the event change range
status: Done
type: Improvement
priority: P2
tags:
  - ci
  - github-actions
  - validation
dependsOn:
  - ICW-036
related:
  - ICW-332
links:
  - .github/workflows/ci.yml
  - docs/handoffs/2026-08-07-wave-t-ci-evidence-hardening.md
created: 2026-08-07
updated: 2026-08-07
---

## Summary

Make the CI whitespace check inspect the commits that triggered the workflow.
The previous command ran `git diff --check` without a range after checkout.
That command did not inspect the pushed change.

## Scope

- Fetch complete Git history in the workflow checkout.
- Use the pull request base SHA for pull requests.
- Use the push predecessor SHA for pushes.
- Use the parent commit when the event has no predecessor SHA.

## Acceptance Criteria

- CI checks the complete pull request change range.
- CI checks all commits introduced by a push event.
- The first push fallback checks the current commit against its parent.
- The workflow keeps the existing build, test, benchmark, and task validation steps.

## Validation

- Command: workflow contract check with PowerShell.
- Result: Passed. Checkout history and event-aware diff range are present.
- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release`.
- Result: Passed.
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-build --no-restore`.
- Result: Passed.
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-build --no-restore`.
- Result: Passed.
- Command: `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --no-restore`.
- Result: Passed.
- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`.
- Result: Passed.
- Command: `git diff --check HEAD^ HEAD`.
- Result: Passed.

## Findings

Wave S correctly added benchmark compilation and task validation.
Its whitespace command had no explicit range, so it did not validate the workflow change on a clean checkout.
The workflow now selects the event range and retains a first-push fallback.

## Next Step

Keep the event-aware range when modifying checkout depth or workflow triggers.
