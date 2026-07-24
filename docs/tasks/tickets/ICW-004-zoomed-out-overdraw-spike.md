# ICW-004: Zoomed-Out Pixel Overdraw Spike

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Measure zoomed-out rendering overdraw and evaluate inner-loop math cost so optimization decisions are based on benchmark evidence.

## Scope

- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- benchmarks/InfiniteCanvas.Benchmarks
- docs/tasks/JIRA.md

## Validation

- Audit capture only in this pass.
- Investigation validation command (planned):
  - `dotnet run --project .\benchmarks\InfiniteCanvas.Benchmarks\InfiniteCanvas.Benchmarks.csproj --configuration Release --framework net10.0-windows --no-build -- --filter "*ProjectionAndBitmapBenchmarks*"`

## Findings

- DrawTile and DrawDefectPatch perform per-pixel division in hot loops.
- Audit recommends comparing current division-heavy path with incremental world-coordinate stepping.
- Results should guide deduplication, accumulation, or alternate rendering strategy selection.

## Next Step

- Add benchmark variants for division versus incremented stepping and capture perf deltas across representative zoom levels.
