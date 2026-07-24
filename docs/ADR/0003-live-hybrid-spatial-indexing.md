# ADR-0003: Live Hybrid Spatial Indexing

- Status: Proposed
- Date: 2026-07-24

## Context

The architecture must support both static inspection scenes and continuously growing datasets. Query performance needs to remain high under frequent camera updates, while newly ingested data must become visible quickly without forcing full index rebuilds per update.

The repository already contains `LiveSpatialIndexService<T>` with:

- immutable snapshot index queries for the bulk of data,
- a hot buffer for recent additions,
- a publishing buffer during snapshot rebuild,
- lock-free state transitions with CAS updates,
- publish serialization via `Interlocked`.

This behavior is implemented but not yet captured in an ADR.

## Decision

Adopt a hybrid spatial indexing model for live data paths:

- keep a published immutable snapshot index as the primary query source,
- append recent writes to a hot in-memory buffer for immediate query visibility,
- move hot buffer items into a publishing buffer during snapshot rebuild,
- build the next snapshot off-thread and atomically publish it,
- query by merging results from snapshot, publishing buffer, and hot buffer,
- on publish failure, return publishing items to the hot buffer and preserve correctness.

`ISpatialIndexService<T>` remains the abstraction boundary so alternate builders (STR-tree, linear, domain-specific binning) can be evaluated without changing rendering or camera code.

## Consequences

Benefits:

- near-immediate visibility for newly ingested items,
- lock-free read path for the published dataset,
- amortized rebuild cost instead of per-item rebuild cost,
- compatibility with both static and live scenarios through one interface.

Trade-offs:

- merged query paths can produce additional per-query overhead while publishing,
- memory footprint temporarily increases during publish windows,
- duplicate detection policy is delegated to callers/data model if identical logical items are inserted repeatedly.

Follow-ups:

- confirm and document query ordering and duplicate expectations,
- add benchmark coverage for hot/publishing proportions representative of production,
- if approved, mark this ADR as Accepted and reference it from README architecture notes.
