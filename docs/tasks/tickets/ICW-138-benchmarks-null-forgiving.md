---
id: ICW-138
key: ICW-138
title: Remove unnecessary null-forgiving operators in benchmark projects
type: Task
priority: P3
status: In Progress
summary: Remove unnecessary null-forgiving operators in benchmark projects
scope:
  - benchmarks/**
owner: Unassigned
tags:
  - maintenance
  - nullable
  - ci

evidence:
  - SonarQube reported multiple uses of the null-forgiving operator (`!`) in benchmark source files (e.g. LiveSpatialQueryBenchmarks.cs, SnapshotBuildBenchmarks.cs, StrTreeQueryBenchmarks.cs) while nullable warnings are disabled for those projects.
  - Leaving `!` suppressions in benchmark code hides potential nullability problems and prevents enabling repository-wide nullable enforcement.
findings:
  - Replaced five `= null!` initializers with nullable field declarations initialized to `null` in benchmark sources as a low-risk first pass.
  - Updated `SampleImageGenerator.CreateFastNoise` to remove an unused `seed` parameter and adjusted its caller to reduce unused-parameter warnings.

next_steps:
  - Open PR(s) with the above edits (already applied locally). Run full test matrix and benchmark build to confirm no behavioral changes.
  - Optionally enable nullable analysis for benchmark projects and address any further warnings.

description: |
  Benchmarks currently contain a small number of null-forgiving operator usages that were introduced either to silence nullable analysis or during refactors. Because benchmark projects are part of the repository and can drift out of sync with core runtime assumptions, these suppressions reduce signal in nullable audits and can mask real issues when the rest of the codebase enables nullable enforcement.

acceptance_criteria:
  - All `!` null-forgiving operators in `benchmarks/` are audited and either removed, replaced with safe guards, or documented with a short comment explaining why the suppression is necessary.
  - If feasible, enable C# nullable analysis for benchmark projects and confirm build/test baseline remains green, or add a specific `Directory.Build.props` override documenting the rationale.
  - Add a CI check or analyzer suppression justification comment pattern for any remaining, unavoidable `!` uses.

validation:
  - Run `rg "!\)" benchmarks -n` and review remaining matches (expected 0 after completion).
  - `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release` should succeed with no nullable-suppression warnings in the benchmark project if nullable is enabled.

next_steps:
  - Audit benchmark source files for `!` usages and create focused PRs removing each suppression with minimal behavioral change.
  - If enabling nullable for benchmarks, update `Directory.Build.props` and record the baseline in the task file.
  - Add a CI lane or analyzer configuration to catch new suppressions.

estimated_effort: 1d

---

