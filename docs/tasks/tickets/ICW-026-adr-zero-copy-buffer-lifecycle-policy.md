# ICW-026: ADR for Zero-Copy Buffer Lifecycle Policy

- Status: In Review
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Document the intended memory-section ownership, front/back reuse policy, and compositor safety assumptions for zero-copy rendering.

## Scope

- docs/ADR
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- docs/tasks/tickets/ICW-021-backbuffer-reuse-safety.md

## Validation

- Draft ADR created: `docs/ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md`
- Pending approval review.

## Findings

- Rendering policy exists implicitly in code but not in an ADR.

## Next Step

- Review proposal wording, then resolve ICW-021 evidence before marking Accepted.
