---
status: draft
summary: Add CI workflow and repository nullable enforcement plan
scope: |
  - Add `.github/workflows/ci.yml` (draft) to build solution and run tests on PRs and pushes.
  - Add `Directory.Build.props` with shared settings: `Nullable` enabled, `LangVersion`, and guidance for staged `TreatWarningsAsErrors` rollout.
  - Document plan for staged enforcement of nullable-as-errors and notify teams.
files_to_change:
  - .github/workflows/ci.yml (new)
  - Directory.Build.props (new)
validation_command: |
  dotnet build InfiniteCanvasWPF.slnx --configuration Release
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
next_step: |
  - Create minimal CI workflow that builds and runs tests; add Directory.Build.props with notes for staged enforcement.
---

Background

There is no repository-wide CI or nullable enforcement. Adding these reduces regressions and provides consistent build settings.

Acceptance criteria

- CI workflow builds and runs tests on PRs.
- `Directory.Build.props` exists with `Nullable` enabled and instructions for staged `TreatWarningsAsErrors` adoption.
