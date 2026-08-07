---
id: ICW-036-ci-and-nullability-enforcement-baseline
key: ICW-036
title: Icw 036 Ci And Nullability Enforcement Baseline
status: Done
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
updated: 2026-08-07
---

## Summary

Establish a repository-wide Roslyn analyzer and CI baseline before adding specialized scanners. All eight projects target .NET 10 (with WPF-specific `net10.0-windows` targets where required), so the .NET SDK supplies the primary analyzer engine. The baseline pins analysis level 10.0 and stages broader rule enforcement after legacy findings are triaged.

## Scope

- Add a root `Directory.Build.props` and analyzer configuration that centralize nullable settings, a pinned analysis level, and staged warning enforcement.
- Add Windows GitHub Actions coverage for the solution build, cross-platform tests, and Windows/WPF tests.
- Measure the initial diagnostic set before elevating broad categories to errors; preserve explicit suppressions for intentional interop and performance exceptions.
- Keep specialized tools as opt-in audit lanes rather than unconditional build dependencies.
- Build the benchmark project in CI so benchmark-only nullable warnings remain visible.
- Validate task metadata and whitespace in CI.

## Acceptance Criteria

- Roslyn analyzers run from the .NET 10 SDK with a repository-wide, reviewable configuration; nullable diagnostics are enforced in CI.
- CI runs `dotnet build InfiniteCanvasWPF.slnx --configuration Release`, the cross-platform NUnit project, and the Windows NUnit project on a Windows runner.
- The baseline records analyzer findings and has no unexplained new warnings before enforcement is broadened.
- The selected tool policy is documented: Roslyn first; SonarQube/SonarAnalyzer as an optional periodic structural audit; Security Code Scan, Semgrep, and StyleCop are not default dependencies.
- Nullable warnings fail the build through the root `Directory.Build.props` baseline. Other analyzer categories remain visible but do not fail this wave.

## Validation

- Command: `dotnet build .\\InfiniteCanvasWPF.slnx --configuration Release --no-restore -v:minimal`
- Result: Passed on 2026-07-25; all eight projects built successfully with no reported warnings or errors.
- Command: `dotnet list .\\InfiniteCanvasWPF.slnx package --include-transitive`
- Result: No Sonar, Security Code Scan, Semgrep, or StyleCop package is installed; NUnit analyzers are present only in the test projects.

## Notes

- Tool applicability research completed 2026-07-25:
  - **Roslyn/.NET SDK analyzers: adopt first.** Microsoft documents that analysis is enabled by default for .NET 5+; the SDK's default mode is intentionally limited, while `AnalysisMode`/`AnalysisLevel`, `EnforceCodeStyleInBuild`, `.editorconfig`, and warning escalation provide the needed controls. This directly covers the repository's WPF interop, reliability, performance, and nullable risks without adding a package.
  - **SonarQube/SonarAnalyzer: optional second lane.** The current Sonar C# analyzer advertises broad quality/security rules, metrics, duplication, and coverage import. It is useful for periodic whole-repository audits and trend reporting, but a server/scanner introduces operational overhead and the analyzer repository is licensed under Sonar Source-Available License v1.0, so it should not be described simply as open source or made a default build dependency without license review.
  - **Security Code Scan: defer.** It is aimed primarily at taint/injection patterns common to web and data-access applications, which are not the dominant risks in this local WPF renderer. Its latest NuGet release is 5.6.7 from 2022-09-05 and its repository shows no recent release cadence; the original recommendation to install it first is therefore not justified for this .NET 10 desktop codebase.
  - **Semgrep Community: defer unless custom rules are needed.** It can be valuable for repository-specific pattern checks, but C# scanning would add a separate CLI/WSL or container toolchain and duplicate Roslyn coverage for the current audit goals. Reconsider for a narrowly defined custom rule set after CI exists.
  - **StyleCop.Analyzers: optional and low priority.** It is MIT-licensed and integrates through NuGet, but the latest GitHub release shown by the project is 1.1.118 from 2018. It addresses convention rather than the lifecycle, interop, spatial, and rendering risks that matter most here; use only after the team chooses a style policy and is willing to manage legacy rule noise.
- Official references reviewed: Microsoft .NET code analysis overview/configuration, SonarSource `sonar-dotnet`, Security Code Scan GitHub/NuGet, and StyleCop Analyzers GitHub. The Semgrep page supplied by the candidate list currently redirects/returns 404, so no stronger Semgrep capability claim is made here.

## Related Tasks

- ICW-014
- ICW-021
- ICW-034

## Findings

Wave S found seven nullable warnings in benchmark setup and execution paths. The warnings came from fields initialized by BenchmarkDotNet lifecycle methods. The implementation adds narrow null guards and keeps benchmark setup behavior unchanged. The pinned analyzer baseline also reports existing CA findings, including vendor and benchmark naming rules. Wave S does not broaden enforcement to those findings.

## Next Step

Review future analyzer warnings as separate tasks. Do not enable broad warnings-as-errors categories until the baseline remains clean across all projects.
