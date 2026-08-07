# Wave S CI and Nullability Baseline Handoff

Status: Complete
Date: 2026-08-07
Commit target: ICW-036

## Summary

Wave S adds a repository-wide Roslyn baseline and extends CI coverage.
Nullable warnings now fail the build. Other analyzer categories remain visible but do not fail this wave.

## Critical review findings

Wave Q was functionally correct, but its handoff recorded the earlier diagnostics commit `be0eca9` instead of the shipped Wave Q revision.
The benchmark build also exposed seven nullable warnings in BenchmarkDotNet lifecycle fields.
CI did not build benchmarks or validate task metadata and whitespace.

## Implementation

- Added root `Directory.Build.props`.
- Enabled SDK analyzers and nullable reference types.
- Pinned analysis level to `10.0`.
- Escalated nullable diagnostics to errors.
- Added narrow null guards to benchmark lifecycle fields.
- Added benchmark compilation to `.github/workflows/ci.yml`.
- Added task tracker validation to CI.
- Added `git diff --check` to CI.
- Marked ICW-036 Done in both task trackers.

## Validation

- Solution Release build passed.
- Core tests passed: `196/196`.
- Windows tests passed: `25/25`.
- Benchmark Windows build passed.
- Task tracker validation passed: `221` files validated, `5` legacy files skipped.
- `git diff --check` passed.
- Known residual: App Release build reports the existing unused `_frameClaimantId` warning.
- Analyzer review found existing CA findings in legacy source, vendor bindings, and benchmark naming. Wave S does not broaden enforcement to those categories.

## Next step

Keep nullable enforcement active. Create separate cleanup tasks before escalating individual CA rule families.
Correct the Wave Q handoff revision if benchmark provenance needs a final exact commit reference.
