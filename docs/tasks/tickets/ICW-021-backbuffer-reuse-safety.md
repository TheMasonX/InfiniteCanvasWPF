---
id: ICW-021-backbuffer-reuse-safety
author: External Audit (Integration-1)
key: ICW-021
title: Validate compositor-safe back-buffer reuse and add guard if needed
status: Done
type: Bug
priority: P1
tags:
  - rendering
  - compositor
  - bitmap
  - synchronization
dependsOn: []
related:
  - ICW-P0-BUFFER-REUSE-SYNC
  - ADR-0004
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/tasks/tickets/ICW-P0-BUFFER-REUSE-SYNC.md
created: 2026-07-25
updated: 2026-07-30
---

# ICW-021 — Validate compositor-safe back-buffer reuse and add guard if needed

## Summary

**Race condition admitted by current double-buffering pattern:** `PublishFrame` (`MainWindow.xaml.cs:465-483`) recycles the just-retired front buffer as the *next* back buffer immediately when dimensions match, with **no synchronization** against WPF's composition thread. The next `RenderFrameAsync` can start writing into the shared file-mapping view while the compositor may still be reading it for the previous frame's `Image.Source`.

**Confidence:** 85% (mechanism confirmed at exact lines; visible tearing not reproduced in static review — depends on compositor timing).

**Linked to:** ICW-P0-BUFFER-REUSE-SYNC — this ticket and ICW-P0-BUFFER-REUSE-SYNC address the same InteropBitmap compositor-handoff race. ICW-P0-BUFFER-REUSE-SYNC contains the detailed implementation plan. This ticket serves as the tracking placeholder and should be closed when ICW-P0-BUFFER-REUSE-SYNC is done.

## Detailed Mechanism

`PublishFrame` does (simplified from lines 465-483):
```csharp
var previousFront = _frontBitmapFactory;
_frontBitmapFactory = _backBitmapFactory;
// If dimensions match, reuse old front as new back immediately:
if (previousFront.Width == _frontBitmapFactory.Width && ...)
    _backBitmapFactory = previousFront;
else
    _backBitmapFactory = new ZeroCopyBitmapFactory(...);
FramePresenter.Child = new Image { Source = _frontBitmapFactory.Bitmap };
```

The retired `previousFront` is immediately reassigned as `_backBitmapFactory`. The next render clears and writes into this buffer. But WPF's compositor may still be reading the old `InteropBitmap` that points at the same native memory section. `FramePresenter.Child = new Image` does not block until the GPU has finished.

## Fix Options

See ICW-P0-BUFFER-REUSE-SYNC for full implementation plan:
- **Option A — Triple buffering** (recommended first): 3 `ZeroCopyBitmapFactory` instances in rotation, one extra at viewport resolution (~64 MiB at 4096x4096 max).
- **Option B — Explicit compositor fence**: `CompositionTarget.Rendering` or `Dispatcher.Invoke` at `DispatcherPriority.Render` to defer reuse.

## Acceptance Criteria

(From ICW-P0-BUFFER-REUSE-SYNC)
- The retired front buffer is not recycled as the next back buffer until the compositor has finished with it.
- No `AccessViolationException` or torn-frame corruption during rapid pan/zoom sequences.
- ADR-0004 updated to reflect the synchronization mechanism.

## Validation

No automated visual regression test available (WPF compositor timing). Manual rapid pan/zoom for 30+ seconds while monitoring for visual tearing. Existing test suite must pass.

## Related Tasks

- ICW-P0-BUFFER-REUSE-SYNC: concrete implementation plan (landed 2026-08-04, this ticket closes with it)
- ADR-0004: zero-copy buffer lifecycle policy (updated after fix)

## Outcome (2026-08-04)

Closed with ICW-P0-BUFFER-REUSE-SYNC. Option A (triple buffering) landed via `FrameBufferPool`. User-reproduced black flashes during fast scroll provided the empirical confirmation this ticket required. Windows 18/18, core 154/154.
