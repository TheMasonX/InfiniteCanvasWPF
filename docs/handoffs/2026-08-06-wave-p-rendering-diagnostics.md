# Wave P Handoff, Rendering Diagnostics

Date: 2026-08-06
Status: Complete

## Critical review

Wave O is correct at the reviewed source boundary.

- Each persisted layer setting has an independent control.
- Each setting loads into its own control and render state.
- `ShowSparseImageTiles` reaches the sparse raster branch.
- `ShowBoxes` controls rectangle stroke and fill without hiding labels.
- The repository was clean and synchronized at commit `4c00562` before Wave P.

## Changes

- Added opt-in `RenderingDiagnostics` with an `AsyncLocal` activation scope.
- Added stage timings for native noise, Gray8 normalization, circle rasterization, tile composition, and sparse composition.
- Added per-mip counters for requested, generated, reused, rejected, failed, and evicted payloads.
- Added sample-count and resident-payload-byte fields.
- Added focused diagnostics tests.
- Updated ICW-132, the requirements registry, and both task trackers.

## Validation

- Focused core diagnostics tests pass 2/2.
- Full core suite passes 194/194.
- Full Windows suite passes 25/25.
- App Release build succeeds with the existing unused `_frameClaimantId` warning only.
- Task tracker validation passes 220 files, with 5 legacy markdown files skipped.
- `git diff --check` passes.
- ICW-133 remains open for repeated BenchmarkDotNet runs and archived hardware metadata.

## Next step

Run the full validation gates, then commit and push Wave P with `HEAD == origin/main`.