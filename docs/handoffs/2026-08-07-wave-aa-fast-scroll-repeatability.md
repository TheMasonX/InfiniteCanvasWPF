# Wave AA Handoff, Fast Scroll Benchmark Repeatability

Date: 2026-08-07
Status: Complete

## Review Result

Wave Z source changes are correct for the stated diagnostic contract. The new
outcomes are additive, and the focused counter test covers all outcome fields.
The remaining ICW-144 gap is benchmark evidence, not a rendering source defect.

## Delivered

- Added an explicit BenchmarkDotNet throughput job to
  `TileWorkCoordinatorBenchmarks`.
- Set three warmup iterations and ten measured iterations.
- Added `scripts/Run-FastScrollBenchmarks.ps1`.
- Recorded UTC time, git revision, operating system, processor, .NET version,
  framework, filter, and benchmark job in each run directory.
- Updated the benchmark guide and ICW-144 tracker records.

## Evidence

- Benchmark Release build passes for `net10.0` and `net10.0-windows`.
- Focused rendering tests pass 18/18.
- PowerShell script parsing passes.
- Task tracker validation passes for 225 task files.
- `git diff --check` passes.

## Residual Risk

The repeat-run script was not run on target hardware in this wave. No
performance claim is supported yet. ICW-144 remains In Review until an archived
run includes the generated BenchmarkDotNet result files and machine metadata.

## Next Step

Run `pwsh -NoProfile -File scripts/Run-FastScrollBenchmarks.ps1` on target
hardware. Review queue depth, useful completions, stale completions, cancellation
behavior, and reservation balance before closing ICW-144.
