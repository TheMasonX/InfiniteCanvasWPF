# ICW-027: GPU Pivot Criteria and Trigger Spike

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Define measurable thresholds and decision criteria for when the current InteropBitmap CPU pipeline should pivot to a GPU-backed path.

## Scope

- DesignDoc.md
- docs/tasks/JIRA.md
- benchmarks/InfiniteCanvas.Benchmarks
- src/InfiniteCanvas.Rendering

## Validation

- Pending:
  - Benchmark-based criteria draft reviewed with maintainers.

## Findings

- Design open question on GPU acceleration is not yet represented by a dedicated backlog item.

## Next Step

- Propose trigger metrics (frame latency, overdraw saturation, CPU utilization) and a minimal proof strategy.
