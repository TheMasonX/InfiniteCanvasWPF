# Wave AB Handoff, Fast Scroll Evidence

Date: 2026-08-07
Status: Complete

## Review Result

Wave AA exposed a script defect. BenchmarkDotNet rejected the comma-packed exporter value, but the script reported success because the command returned exit code zero. The script did not prove that result files existed.

## Delivered

- Passed each BenchmarkDotNet exporter as a separate argument.
- Directed BenchmarkDotNet artifacts into the run directory.
- Added a result-file check that fails when the run produces no report.
- Removed the invalid preliminary run from the worktree.
- Closed ICW-144 after collecting target-host evidence.

## Evidence

- Archived run: `docs/benchmarks/runs/20260807-160953`
- Reports: CSV, JSON, HTML, and GitHub Markdown.
- Job: Release, `net10.0-windows`, three warmups, ten measured iterations.
- Host: Windows 10.0.19045, Intel Core i5-6600K, .NET 10.0.302.
- Git revision: `2a7df467ed24381f4c2e6bf9c3ef0ea38ad8b26e`.
- PowerShell parsing passed.
- The corrected benchmark command completed and produced all four reports.

## Residual Risk

The run measures one host. It does not support cross-machine performance claims. The benchmark scenario shape remains a future compatibility contract.

## Next Step

Repeat the script on additional target hardware before comparing throughput across machines.
