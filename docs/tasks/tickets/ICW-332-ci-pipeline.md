---
id: ICW-332
author: Copilot
key: ICW-332
title: Add GitHub Actions build and test pipeline
status: Done
type: Improvement
priority: P1
tags:
  - ci
  - github-actions
  - testing
dependsOn: []
related: []
links:
  - .github/workflows/ci.yml
  - README.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-332 - Add GitHub Actions build and test pipeline

## Summary

GitHub has no repository build or test workflow for `main`. Add a Windows workflow that uses .NET 10 and validates the solution and both test projects.

## Scope

- Add `.github/workflows/ci.yml`.
- Build `InfiniteCanvasWPF.slnx` in Release mode.
- Run the cross-platform and Windows test projects on `windows-latest`.

## Acceptance Criteria

- GitHub starts CI for pushes to `main` and pull requests targeting `main`.
- The workflow builds the Release solution.
- The workflow runs both test projects.
- The local build and test commands pass before the change is reported complete.

## Validation

- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Result: Pass. The solution builds, 188 core tests pass, 22 Windows tests pass, and task tracking validation passes.

## Notes

- GitHub reported no build or test workflow before this change.
- The Windows runner is required for WPF compilation and tests.
- GitHub needs the workflow commit before it can report a new CI run.

## Related Tasks

- None.