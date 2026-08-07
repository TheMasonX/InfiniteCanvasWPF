# Wave Z Handoff, Fast Scroll Diagnostics

Date: 2026-08-07
Status: Complete

## Prior Wave Review

Wave Y changed `BackgroundTileMipPolicy.SelectMipLevel` to use the larger camera scale. ADR-0005 defines that scale as the binding axis for non-uniform cameras. The regression test uses scales `0.25` and `2.0`, so the old smaller-axis policy and the corrected policy produce different results. The direct source change and focused test are correct. A visual anisotropic zoom check remains open.

Wave Y left the concurrent settings and property-editor changes outside its commit. The current worktree still contains those changes, and Wave Z does not modify them.

## Delivered

- Added `ResidentFallback`, `Useful`, and `Stale` rendering diagnostic outcomes.
- Classified native and mip coordinator completions as useful or stale.
- Classified non-exact resident payload selection as resident fallback.
- Preserved existing requested, generated, reused, rejected, failed, and evicted counters.
- Added focused counter coverage.
- Closed ICW-144 in the active tracker and ticket.

## Evidence

- Focused `SampleImageTileTests` and `RenderingDiagnosticsTests` pass, 18/18.
- The first focused diagnostics validation passed, 2/2.
- `InfiniteCanvas.Rendering` and the focused test project build successfully in Release with `--no-restore`.
- The seven TileWorkCoordinator benchmark methods remain available.
- Repeated BenchmarkDotNet measurements were not run. Do not make performance claims from this wave.

## Residual Risk

The new counters classify tile completion outcomes but do not yet produce repeated hardware benchmark artifacts. A later benchmark run must record runtime, build configuration, concurrency, queue trace, and reservation balance.

The active worktree contains unrelated settings and property-editor edits. Do not include those files in the Wave Z commit.

## Next Step

Run the full core and Windows suites, build the App and benchmark projects, validate task metadata, then commit and push Wave Z. Review the visual anisotropic mip transition when Windows runtime inspection is available.
