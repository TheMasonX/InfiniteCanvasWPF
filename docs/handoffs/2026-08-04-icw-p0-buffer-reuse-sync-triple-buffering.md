# Handoff: ICW-P0-BUFFER-REUSE-SYNC Triple-Buffering + ICW-317 Persistent Frame Shell

- Date: 2026-08-04
- Status: Implementation complete, committed, awaiting visual verification
- Tickets: docs/tasks/tickets/ICW-P0-BUFFER-REUSE-SYNC.md, docs/tasks/tickets/ICW-317-persistent-frame-shell.md

## Summary

The canvas flashed black during fast scrolling. The user reported the symptom and suspected main-UI-thread invoke issues. Two mechanisms produced the flashes:

1. The double-buffer rotation in `MainWindow` reused a just-presented buffer as the next back buffer with no wait. WPF's composition thread reads the `InteropBitmap` backing section asynchronously. The next frame cleared and rewrote a section the compositor was still reading.
2. After the triple-buffer fix, `PublishFrame` still replaced the whole `Viewbox` child every frame. Each publish tore down and rebuilt the visual tree, leaving a teardown gap that flashed dark.

The user report is the empirical confirmation the ICW-021 audit tickets required.

## Findings

- `PublishFrame` moved the old front buffer straight back into the back slot. The next `RenderFrameAsync` cleared it with `NativeMemory.Clear` before the compositor finished sampling it.
- After the triple-buffer fix, the flash persisted occasionally. The remaining cause was the per-frame `Viewbox` child replacement (a new `Grid` + `Image` + overlays each publish).
- The CPU profile shows `presentationframework.dll` and `DispatchMessage` at the top. This is consistent with composition lag behind a busy render loop.
- The tile coordinator caps generation at 4 concurrent workers. The per-tile `Dispatcher.InvokeAsync` callbacks are not the primary cost.

## Changes

- `src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs` (new): owns front, back, and retired slots. A buffer is rewritten only after one full frame cycle.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs`: `AcquireBackBuffer` and `PublishFrame` delegate to the pool. `OnClosed` disposes the pool. The dead `_frontBitmapFactory` and `_backBitmapFactory` fields are removed.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs` (ICW-317): `EnsureFrameShell` attaches a stable `Grid` + `Image` + overlay shell once. `PublishFrame` swaps only `Image.Source` and repopulates the overlay canvases in place. `BuildFrameVisual` and `BuildTileGridLayer` removed. `FramePresenter.Child` is assigned exactly twice: shell attach and close detach.
- `tests/InfiniteCanvas.Windows.Tests/FrameBufferPoolTests.cs` (new): 6 tests cover no-immediate-reuse, full-frame-cycle recycle, never-two-slots-on-one-buffer, at-most-three-buffers, and size-mismatch paths.
- `tests/InfiniteCanvas.Tests/FrameShellWiringTests.cs` (new): 2 source-wiring tests assert the shell exists and the Viewbox child is assigned only twice.
- Docs: ADR-0004 marked Accepted with the rotation mechanism. The frame-stability and frame-surface-reuse invariants are marked DELIVERED. ICW-P0-BUFFER-REUSE-SYNC, ICW-021, and ICW-317 marked Done.

## Validation Evidence

- Windows tests: 18/18 pass, including 6 new pool tests.
- Core tests: 156/156 pass, including 2 new shell wiring tests.
- App Release build: 0 errors (app closed so the relink succeeded).

## Recommended Next Step

1. Rebuild and fast-scroll for 30+ seconds.
2. Confirm the remaining occasional flash is gone.
3. Watch for `AccessViolationException` in the app logs.
4. If any flash remains, the next lever is ICW-007 (pool the annotation overlay elements and persist the selection shape).
