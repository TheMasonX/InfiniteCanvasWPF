---
status: proposed
summary: Add CI workflow and enforce nullable/warning-as-error baseline
scope:
  - .github/workflows/ci.yml
  - Directory.Build.props
  - tests/**
validation_command: |
  dotnet build InfiniteCanvasWPF.slnx --configuration Release
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
findings_evidence: |
  - No `.github/workflows/` exists; repo has no automated CI (docs/audits/* pass2).
  - Individual projects set `<Nullable>enable</Nullable>` but no central `Directory.Build.props` enforces warnings-as-errors.
  - Risk: nullable regressions and analyzer warnings can accumulate unnoticed.
next_steps:
  - Add a minimal GitHub Actions workflow: build matrix for `net10.0` and `net10.0-windows` (Windows runner for Windows tests); run `dotnet build` and `dotnet test` for both test projects. Owner: @maintainer
  - Add `Directory.Build.props` at repo root enabling `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and a targeted `<WarningsAsErrors>nullable</WarningsAsErrors>` (or selectively enable full). Owner: @maintainer
  - Add caching for NuGet and restore; gate Windows-only tests behind windows runner condition. Owner: @maintainer
  - Validation: push branch, ensure workflow runs succeed and nullable diagnostics fail the build when introduced.
