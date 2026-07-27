# 2026-07-26 Viewport-Aware Tile Scheduling Plan Handoff

## Summary

Created the planning and architectural foundation for a viewport-cancellable tile materialization system that addresses the fast-scroll backlog problem (tile request rate > generation rate during rapid navigation). The current per-tile fire-and-forget `Task.Run` generation path uses flags and epochs to prevent stale *publication*, but it does not bound queued work, cancel unclaimed in-flight generation, or prioritize visible tile work over stale requests.

## Problem

When the user pans quickly, the existing pipeline generates a massive backlog of tile generation work. Each tile independently starts a `Task.Run` via `EnsurePixelsGenerationStarted` / `EnsureMipPixelsGenerationStarted`. The render coalescer (`CoalescingAsyncAction`) only coalesces frame requests — it has no visibility into tile-level work. The result is that workers spend CPU time generating tiles that are already off-screen.

## Deliverables created

| Artifact | Purpose |
| --- | --- |
| [ADR-0006](../docs/ADR/0006-viewport-aware-tile-work-scheduling.md) | Architecture decision: bound, prioritize, and cancel tile work by viewport interest without breaking shared cache fills |
| [ICW-141](../docs/tasks/tickets/ICW-141-viewport-aware-tile-work-scheduling.md) | Parent epic: split the problem into three child tasks |
| [ICW-142](../docs/tasks/tickets/ICW-142-bounded-cancellable-tile-materialization.md) | Implementation: bounded, deduplicated, cancellable materialization with shared-fill claimant ownership |
| [ICW-143](../docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md) | Implementation: viewport interest snapshots, culling stale requests, visible-first priority |
| [ICW-144](../docs/tasks/tickets/ICW-144-fast-scroll-tile-queue-stress-validation.md) | Spike: repeatable fast-scroll telemetry and benchmarks to measure queue/cancellation behavior |
| [Requirements registry update](../docs/requirements/functional-requirements-and-invariants.md) | Added "Viewport-aware tile work" invariant requiring bounded, deduplicated, viewport-culled work |

## Key architectural constraint from ADR-0006

The design distinguishes **request interest** from **shared cache-fill ownership**:

- A frame's viewport update publishes an interest snapshot; only the claimants (current visible frames) own the request.
- Cancellation of a stale claimant removes its interest but *must not* cancel the underlying generation if another frame's claimant still needs it.
- The renderer remains synchronous and non-blocking — it samples a resident payload or placeholder while the coordinator works asynchronously.
- Cache reservations are acquired at admission and released exactly once on cancellation, failure, or rejected admission.

This constraint is critical: a naive `CancellationTokenSource`-per-tile that fires on every viewport change would break shared cache fills needed by a nearby viewport frame.

## Validation

- All four new ticket files pass the individual task validator: `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks/tickets/ICW-14*`
- The repository-wide validator reports pre-existing legacy ticket metadata issues (missing `key`, `title`, `type` fields in older tickets) that are unrelated to these additions.
- No source files were modified.

## Recommended next steps

1. **Implement ICW-142 first** — the bounded cancellable materialization is the foundation. It introduces the coordination abstraction that ICW-143's viewport culling feeds into. Without it, there is nowhere to route cancellation signals.
2. **Follow with ICW-143** — wire the viewport interest snapshot from `MainWindow.RenderFrameAsync` into the coordinator and add priority ordering.
3. **End with ICW-144** — the stress spike should confirm that queue depth stays bounded, stale completion drops, and useful current-viewport completion improves. Do not use one-iteration Dry runs for percentage claims.
4. **Coordinate with ICW-076** (mip materializer) and ICW-096 (resident-mip fallback) — the coordinator must be source-neutral and preserve existing fallback behavior.
5. **Align diagnostics with ICW-132** (stage instrumentation) and **ICW-133** (benchmark matrix) so the fast-scroll traces share the same counter definitions.
