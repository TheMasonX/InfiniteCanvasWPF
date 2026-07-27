# ADR-0006: Viewport-Aware Tile Work Scheduling and Cancellation

- Status: Proposed
- Date: 2026-07-26

## Context

Fast panning and zooming can request visible background tile payloads faster than the current per-tile `Task.Run` generation path completes them. The current tile flags and generation epoch prevent duplicate publication, but they do not remove queued work or cancel generation that is no longer relevant. The render coalescer coalesces frame requests, not tile materialization requests, so a frame can remain blank or stale while workers spend time on tiles outside the current viewport.

The mip materializer/cache also has shared-fill semantics: one frame's cancellation must not interrupt a cache fill still needed by another current waiter. Queue cancellation therefore needs to distinguish request interest from ownership of the underlying shared generation operation.

## Decision

Introduce a source-neutral, bounded tile-work coordinator/materializer with these rules:

- Tile work is identified by the complete source-scoped cache key, including tile identity, content revision, and mip level.
- A viewport update publishes a generation/interest snapshot containing the visible set, an optional small prefetch margin, camera center, and request epoch.
- Queued requests whose key is no longer claimed by the current interest snapshot are removed or marked canceled before execution. In-flight work receives cancellation when its last current claimant leaves; shared fills remain alive while another claimant remains.
- Work admission is bounded by a configurable concurrency limit. The queue prioritizes current visible requests by viewport relevance, with center distance and mip suitability as deterministic tie-breakers. Prefetch work is lower priority than visible work.
- Equal cache-key requests coalesce into one underlying fill. Completion publishes only if the cache key/revision and request epoch are still valid; stale completion may populate a still-valid cache entry but must not trigger stale frame publication.
- Cache reservations are acquired at admission and released on cancellation, failure, or rejected admission according to the actual payload variant cost. Cancellation and disposal must not leak reservations or fault the UI pipeline.
- The renderer remains synchronous and non-blocking: it samples a resident payload or documented placeholder/fallback while the coordinator works asynchronously.

## Consequences

This reduces queue bloat and stale CPU work during rapid navigation, while adding coordinator state, priority comparisons, cancellation ownership, and diagnostics. It preserves the existing resident-mip fallback and source-agnostic materializer boundaries. Exact prefetch distance, concurrency default, and cancellation grace period remain benchmark-driven configuration choices rather than hard-coded architecture rules.

## Implementation Sequence

1. Define request/interest ownership and coordinator diagnostics with deterministic unit tests.
2. Replace fire-and-forget tile generation admission with bounded, deduplicated, cancellable work ownership.
3. Add viewport culling and relevance ordering, preserving resident fallback and cache reservation correctness.
4. Add fast-scroll stress benchmarks and runtime counters for queue depth, cancellation, stale completion, coalescing, and useful completion rate.

## Related Tasks

- ICW-076: source-agnostic background tile mip materialization
- ICW-096: resident imagery during mip transitions
- ICW-132: stage-level rendering instrumentation
- ICW-133: rendering benchmark matrix
- ICW-141 through ICW-144: implementation plan for this ADR
