# ICW-024: DesignDoc to ADR and Task Reconciliation

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Reconcile `DesignDoc.md` architecture intent against accepted ADRs and tracked backlog, then convert uncovered items into durable tasks and ADR follow-ups.

## Scope

- DesignDoc.md
- docs/ADR/0001-benchmark-project-targeting-and-baselines.md
- docs/ADR/0002-inspection-raster-and-annotation-layers.md
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Coverage Findings

- Implemented and captured:
  - Benchmark harness and baseline policy direction (ADR-0001, ICW-001).
  - Inspection raster plus annotation layering and immutable-per-frame camera snapshot (ADR-0002, ICW-002/003/006/008/009/010/011/012/013).
  - Hybrid live indexing abstraction and publish flow exists in code via `LiveSpatialIndexService<T>` and `ISpatialIndexService<T>` abstraction.
- Captured but still open:
  - Overdraw measurement (ICW-004).
  - Resize and surface policy (ICW-005).
  - Overlay pooling and continuity (ICW-007 and ICW-019).
  - Back-buffer reuse safety validation (ICW-021).
- Missing from ADR/task durability before this reconciliation:
  - Explicit ADR for live hybrid index model used by implementation.
  - Explicit ADR for zero-copy buffer lifecycle and handoff safety policy.
  - Explicit spike for GPU pivot criteria and trigger policy from design open questions.

## Validation

- Completed evidence review from current ADRs, active tasks, JIRA, and source layout.

## Next Step

- Track ADR and spike follow-up via ICW-025, ICW-026, and ICW-027.
