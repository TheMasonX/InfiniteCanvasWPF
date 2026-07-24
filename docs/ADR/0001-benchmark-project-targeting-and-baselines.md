# ADR-0001: Benchmark Project Targeting and Baselines

- Status: Accepted
- Date: 2026-07-23

## Context

Spatial indexing and snapshot rebuilds are platform-neutral, while `ZeroCopyBitmapFactory` depends on
Kernel32 and WPF. The design includes machine-sensitive throughput and frame-latency goals that are not
suitable for ordinary test assertions. The largest requested benchmark contains 10 million records and
is too expensive for routine validation.

## Decision

Use one BenchmarkDotNet project targeting both `net10.0` and `net10.0-windows`.

- Both targets expose STR-tree query, live-buffer query, and snapshot rebuild benchmarks.
- Only the Windows target compiles and exposes projection plus zero-copy bitmap generation.
- Use deterministic input generation and BenchmarkDotNet's memory diagnoser.
- Keep generated artifacts out of source control.
- Do not use benchmark results as unit-test or CI timing thresholds until a separate baseline policy
  defines hardware, runtime, variance, and regression tolerances.

## Consequences

Cross-platform contributors can measure the spatial pipeline without WPF. Windows contributors can run
the complete frame path from projection through frozen `InteropBitmap` creation. Full benchmark runs are
opt-in because the largest cases consume substantial time and memory. Performance changes can be compared
repeatably, but they do not automatically fail the build.
