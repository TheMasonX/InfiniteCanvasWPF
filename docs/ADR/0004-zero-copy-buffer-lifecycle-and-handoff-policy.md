# ADR-0004: Zero-Copy Buffer Lifecycle and Handoff Policy

- Status: Proposed
- Date: 2026-07-24

## Context

The rendering path uses memory-mapped sections and `InteropBitmap` to avoid managed pixel-copy overhead. `ZeroCopyBitmapFactory` owns a mapped buffer and produces frozen bitmaps from that buffer. `MainWindow` currently rotates front/back factories to reduce allocation churn.

The policy is implemented in code but not explicitly captured as an architectural decision, and current backlog includes a stress-validation task for compositor-safe reuse.

## Decision

Adopt the following lifecycle and ownership policy for zero-copy buffers:

- one `ZeroCopyBitmapFactory` owns exactly one memory mapping and mapped view for its lifetime,
- pixel writes occur only through the owning factory, under its internal lifetime gate,
- every presented frame is created as a frozen `InteropBitmap`,
- ownership of active/pending factories is managed at the presenter level,
- recycling of previously presented mappings is allowed only under a documented safety policy validated by stress evidence,
- disposal order must preserve mapping validity while any presented bitmap may still be consumed by WPF composition.

Until stress-validation evidence is finalized, this ADR remains Proposed and non-final about minimum safe buffering depth.

## Consequences

Benefits:

- codifies invariants already relied on by rendering code,
- reduces risk of accidental ownership or disposal regressions,
- makes safety assumptions explicit for future optimizations.

Trade-offs:

- stricter lifecycle rules may limit aggressive reuse optimizations,
- additional validation effort is required before acceptance,
- potential increase in temporary memory use if policy requires deeper buffering.

Follow-ups:

- complete ICW-021 stress protocol and document outcomes,
- decide whether two-buffer reuse is sufficient or a safer delayed-recycle policy is needed,
- if approved, mark this ADR as Accepted and reference it from rendering docs.
