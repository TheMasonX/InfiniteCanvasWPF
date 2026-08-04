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
- a buffer that leaves the screen is rewritten only after WPF has composed at least two more frames (composition fence, ICW-318),
- recycling of previously presented mappings is allowed only under this composition-fenced policy,
- disposal order must preserve mapping validity while any presented bitmap may still be consumed by WPF composition.

Until stress-validation evidence is finalized, this ADR remains Proposed and non-final about minimum safe buffering depth.

## Synchronization Mechanism (2026-08-04)

`MainWindow` rotates front/back factories directly. This reused a just-presented buffer as the next back buffer with no handoff wait. WPF's composition thread reads the `InteropBitmap` backing section asynchronously, so the next frame could clear and rewrite a section the compositor was still reading. The user reproduced the predicted symptom: black flashes during fast scrolling, mostly behind where tiles should be (ICW-021 / ICW-P0-BUFFER-REUSE-SYNC).

`FrameBufferPool` (src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs) owns the buffer lifecycle with a composition fence:

- front: the buffer currently presented to WPF,
- back: the buffer the next frame renders into (reused from a stale frame, or newly allocated),
- retiring and confirmed: two handoff stages a buffer must pass through after it leaves the screen.

`MainWindow` subscribes to `CompositionTarget.Rendering` and calls `FrameBufferPool.OnCompositionFrame()` once per pass. A retired buffer moves from retiring to confirmed on the first pass and from confirmed to reusable on the second. `AcquireBackBuffer` reuses only confirmed buffers and disposes any whose size no longer matches the viewport.

The fixed one-frame delay used earlier (triple buffering, ICW-P0-BUFFER-REUSE-SYNC) was probabilistic. WPF composition can lag more than one frame when the render loop is saturated, and the user still saw black horizontal bands during fast scroll. The two-pass fence makes reuse conditional on real composition progress. Steady-state memory stays at two or three native sections; worst-case transient memory stays bounded by the existing 4096x4096 viewport clamp (about 64 MiB per BGRA32 section).

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

- keep the two-pass fence unless profiling shows composition lag exceeds two frames under load,
- consider ICW-007 (retained overlay pooling) to reduce per-frame UI-thread visual rebuild cost, which worsens composition lag during fast scroll.
