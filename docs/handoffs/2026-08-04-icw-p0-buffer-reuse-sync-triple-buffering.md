# Handoff: ICW-P0-BUFFER-REUSE-SYNC Triple-Buffering for Compositor Handoff

- Date: 2026-08-04
- Status: Implementation complete, not yet committed
- Ticket: docs/tasks/tickets/ICW-P0-BUFFER-REUSE-SYNC.md

## Summary

The canvas flashed black during fast scrolling. The user reported the symptom and suspected main-UI-thread invoke issues. The root cause is the double-buffer rotation in `MainWindow`. A just-presented buffer was reused as the next back buffer with no wait. WPF's composition thread reads the `InteropBitmap` backing section asynchronously. The next frame cleared and rewrote a section the compositor was still reading. The user report is the empirical confirmation the ICW-021 audit tickets required.

## Findings

- `PublishFrame` moved the old front buffer straight back into the back slot. The next `RenderFrameAsync` cleared it with `NativeMemory.Clear` before the compositor finished sampling it.
- The black flash matches the compositor reading a cleared or partially drawn section.
- The CPU profile shows `presentationframework.dll` and `DispatchMessage` at the top. This is consistent with composition lag behind a busy render loop.
- The tile coordinator caps generation at 4 concurrent workers. The per-tile `Dispatcher.InvokeAsync` callbacks are not the primary cost. The buffer reuse race is.

## Changes

- `src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs` (new): owns front, back, and retired slots. A buffer is rewritten only after one full frame cycle.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`: `AcquireBackBuffer` and `PublishFrame` delegate to the pool. `OnClosed` disposes the pool. The dead `_frontBitmapFactory` and `_backBitmapFactory` fields are removed.
- `tests/InfiniteCanvas.Windows.Tests/FrameBufferPoolTests.cs` (new): 6 tests cover no-immediate-reuse, full-frame-cycle recycle, never-two-slots-on-one-buffer, at-most-three-buffers, and size-mismatch paths.
- Docs: ADR-0004 marked Accepted with the rotation mechanism. The frame-surface-reuse invariant is marked DELIVERED. ICW-P0-BUFFER-REUSE-SYNC and ICW-021 marked Done.

## Validation Evidence

- Windows tests: 18/18 pass, including 6 new pool tests.
- Core tests: 154/154 pass.
- App Release build: compiles with no CS errors. The final DLL copy fails only because the running app locks its output DLLs. Relink after the app closes.

## Recommended Next Step

1. Close the running app.
2. Rebuild and fast-scroll for 30+ seconds.
3. Confirm the black flashes are gone.
4. Watch for `AccessViolationException` in the app logs.
5. Commit this batch with the docs and handoff note.
