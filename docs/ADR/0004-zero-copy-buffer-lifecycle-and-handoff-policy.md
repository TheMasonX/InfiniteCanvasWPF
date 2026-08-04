# ADR-0004: Zero-Copy Buffer Lifecycle and Handoff Policy

- Status: Accepted
- Date: 2026-07-24
- Updated: 2026-08-04

## Context

The rendering path uses memory-mapped sections and `InteropBitmap` to avoid managed pixel-copy overhead. `ZeroCopyBitmapFactory` owns a mapped buffer and produces frozen bitmaps from that buffer. `MainWindow` currently rotates front/back factories to reduce allocation churn.

The policy is implemented in code but not explicitly captured as an architectural decision, and current backlog includes a stress-validation task for compositor-safe reuse.

## Decision

Adopt the following lifecycle and ownership policy for zero-copy buffers:

- one `ZeroCopyBitmapFactory` owns exactly one memory mapping and mapped view for its lifetime,
- pixel writes occur only through the owning factory, under its internal lifetime gate,
- every presented frame is created as a frozen `InteropBitmap`,
- ownership of active/pending factories is managed by `FrameBufferPool` at the presenter level,
- a buffer that leaves the screen is moved to a retired slot and is rewritten only after one full frame cycle has been presented (triple-buffering, ICW-P0-BUFFER-REUSE-SYNC),
- recycling of previously presented mappings is allowed only under this documented rotation policy,
- disposal order must preserve mapping validity while any presented bitmap may still be consumed by WPF composition.

Until stress-validation evidence is finalized, this ADR remains Proposed and non-final about minimum safe buffering depth.

## Synchronization Mechanism (2026-08-04)

`MainWindow` rotates front/back factories directly. This reused a just-presented buffer as the next back buffer with no handoff wait. WPF's composition thread reads the `InteropBitmap` backing section asynchronously, so the next frame could clear and rewrite a section the compositor was still reading. The user reproduced the predicted symptom: black flashes during fast scrolling, mostly behind where tiles should be (ICW-021 / ICW-P0-BUFFER-REUSE-SYNC).

`FrameBufferPool` (src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs) now owns the rotation with three slots:

- front: the buffer currently presented to WPF,
- back: the buffer the next frame renders into,
- retired: the buffer that left the screen one frame ago, safe to recycle.

On publish, the old front moves to the retired slot. A buffer is reused as the back buffer only after one full frame cycle, which gives the compositor that slack. The rotation holds at most two native sections in steady state, the same as the old double-buffer, because the retired slot is promoted to the back slot on the next acquire. The delay, not a third allocation, is what removes the race. Worst-case transient memory stays bounded by the existing 4096x4096 viewport clamp (about 64 MiB per BGRA32 section).

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

- keep the rotation depth at three unless profiling shows composition still lags more than one frame,
- consider ICW-007 (retained overlay pooling) to reduce per-frame UI-thread visual rebuild cost, which worsens composition lag during fast scroll.
