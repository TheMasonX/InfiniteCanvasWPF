---
id: ICW-P0-BUFFER-REUSE-SYNC
author: External Audit (Integration-1)
key: ICW-P0-BUFFER-REUSE-SYNC
title: Add synchronization or triple-buffering for InteropBitmap compositor handoff
status: Proposed
type: Bug
priority: P0
tags:
  - rendering
  - bitmap
  - compositor
  - synchronization
  - tearing
dependsOn: []
related:
  - ICW-021
  - ADR-0004
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-BUFFER-REUSE-SYNC — Add synchronization or triple-buffering for InteropBitmap compositor handoff

## Summary

**Critical gap:** `PublishFrame` (`MainWindow.xaml.cs:465-483`) recycles the just-retired front buffer as the *next* back buffer immediately when dimensions match, with **no synchronization** against WPF's composition thread. `RenderFrameAsync` can start writing new pixels into the shared memory-mapped file section (`NativeMemory.Clear` + `DrawTile` writes) while the compositor may still be reading the same `InteropBitmap` for the previous frame.

This is a distinct race from the defect-pool dispose finding (ICW-102). It affects every frame, not just regeneration boundaries.

**Confidence:** 85% (mechanism confirmed at exact lines; visible tearing not reproduced in static review — depends on compositor timing).

## Root Cause

`PublishFrame` does (simplified):

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

The retired `previousFront` is immediately reassigned as `_backBitmapFactory`. The next `RenderFrameAsync` call (triggered by the next camera change) clears and writes into this buffer via `NativeMemory.Clear` + `DrawTile`/`DrawDefectPatch`. But WPF's composition thread may still be reading the old `InteropBitmap` that points at the same native memory section for the previous frame's `Image.Source`. The composition is asynchronous — `FramePresenter.Child = new Image` does not block until the GPU has finished reading.

This is a classic double-buffering race where the "free" buffer is recycled before the consumer has finished with it.

## Scope

### Fix Options (pick one)

**Option A — Triple buffering (recommended for first implementation):**
- Keep 3 `ZeroCopyBitmapFactory` instances in rotation instead of 2.
- The retired front buffer goes into a "retired" pool, not back to `_backBitmapFactory`.
- `_backBitmapFactory` is always taken from the pool (or newly allocated).
- This gives the compositor a full frame of slack before a buffer is reused.
- Memory cost: one extra native section at viewport resolution (bounded by existing 4096x4096 clamp, ~64 MiB for BGRA32 at max resolution).
- Simpler than a fence because it does not depend on WPF composition internals.

**Option B — Explicit compositor fence (more precise, more code):**
- After `FramePresenter.Child = frameVisual`, use `CompositionTarget.Rendering` or `Dispatcher.Invoke` at `DispatcherPriority.Render` to defer marking the old front buffer reusable until *after* the next composed frame has been presented.
- More complex, but avoids the extra memory allocation of triple buffering.
- Risk: depends on WPF rendering pipeline timing which can vary.

### Test Requirements

- **Reference-counting wrapper:** Instrument `ZeroCopyBitmapFactory` in test builds with a reference count that increments when set as `Image.Source` and decrements when a new frame replaces it. Assert that no two live `Image.Source` references ever point at the same `ZeroCopyBitmapFactory` simultaneously.
- **Regression test (Option A):** Rapidly publish N frames with varying content, assert no `AccessViolationException` or torn-output pattern in `_frontBitmapFactory`'s memory.
- **ADR-0004 update:** Mark acceptance criteria contingent on this ticket landing.

### Acceptance Criteria

- The retired front buffer is not recycled as the next back buffer until the compositor has finished with it (triple-buffering or explicit fence).
- No `AccessViolationException` or torn-frame corruption during rapid pan/zoom sequences.
- Memory cost of the selected approach is bounded and documented.
- ADR-0004 updated to reflect the synchronization mechanism.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | `PublishFrame`: implement triple-buffering or compositor fence for buffer reuse |
| `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs` | Add reference-counting or pool support if needed |
| `docs/ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md` | Update acceptance criteria and document synchronization mechanism |

## Validation

No automated visual regression test available (WPF compositor timing). Manual validation:
- Rapid pan/zoom for 30+ seconds while monitoring for visual tearing.
- Assert no `AccessViolationException` in app logs.
- Run existing test suite to confirm no regressions:
  ```
  dotnet test tests/InfiniteCanvas.Tests --configuration Release
  dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release
  ```

## Related Tasks

- ICW-021: same InteropBitmap compositor-handoff race (template ticket — this is the concrete implementation plan for both)
- ADR-0004: zero-copy buffer lifecycle policy (update after this ticket lands)
