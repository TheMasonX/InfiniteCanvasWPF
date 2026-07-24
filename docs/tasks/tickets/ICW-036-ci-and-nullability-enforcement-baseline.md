# ICW-036: CI And Nullability Enforcement Baseline

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent
- Priority: P2

## Summary

Add minimal repository automation and central compile-policy enforcement so build/test and nullable hygiene are verified continuously instead of manually.

## Scope

- .github/workflows
- Directory.Build.props
- src/**/*.csproj
- tests/InfiniteCanvas.Tests
- tests/InfiniteCanvas.Windows.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - CI run on push/PR for `dotnet build` and both test projects
  - Local smoke: `dotnet test .\InfiniteCanvasWPF.slnx --configuration Release`

## Findings

- Repository currently has no workflow automation under `.github/workflows`.
- Nullable context is enabled in project files but warning enforcement is decentralized and non-blocking.
- Manual validation claims in task logs are not independently enforced by branch automation.

## Next Step

- Add a Windows CI workflow and a root `Directory.Build.props` policy for warnings-as-errors (or nullable warnings-as-errors), then verify solution behavior under the new policy.
