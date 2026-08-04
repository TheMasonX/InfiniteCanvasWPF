# Handoff: ICW-P0-BUFFER-REUSE-SYNC Triple-Buffering + ICW-317 Persistent Frame Shell

- Date: 2026-08-04
- Status: Implementation complete, committed, awaiting visual verification
- Tickets: docs/tasks/tickets/ICW-P0-BUFFER-REUSE-SYNC.md, docs/tasks/tickets/ICW-317-persistent-frame-shell.md

## Summary

The canvas flashed black during fast scrolling. The user reported the symptom and suspected main-UI-thread invoke issues. Two mechanisms produced the flashes:

1. The double-buffer rotation in `MainWindow` reused a just-presented buffer as the next back buffer with no wait. WPF's composition thread reads the `InteropBitmap` backing section asynchronously. The next frame cleared and rewrote a section the compositor was still reading.
2. After the triple-buffer fix, `PublishFrame` still replaced the whole `Viewbox` child every frame. Each publish tore down and rebuilt the visual tree, leaving a teardown gap that flashed dark.
3. After the persistent shell (ICW-317), black horizontal bands remained during fast scroll. The bands are the compositor sampling a buffer section mid-write. The fixed-delay rotation was probabilistic; the compositor can lag more than one frame. ICW-318 fences reuse on two real composition passes via `CompositionTarget.Rendering`.

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
- `src/InfiniteCanvas.Rendering/FrameBufferPool.Windows.cs` (ICW-318): two-stage retiring/confirmed/reusable pipeline. `OnCompositionFrame` advances one stage per composition pass. `AcquireBackBuffer` reuses only confirmed buffers and disposes size-mismatched ones.
- `src/InfiniteCanvas.App/MainWindow.xaml.cs` (ICW-318): subscribes to `CompositionTarget.Rendering` and drives `OnCompositionFrame`; unsubscribes in `OnClosed`.
- `tests/InfiniteCanvas.Windows.Tests/FrameBufferPoolTests.cs` (ICW-318): rewritten for the pipeline; two-pass reusability test, rotation reuse test, size-mismatch disposal test.
- Docs: ADR-0004 marked Accepted with the rotation mechanism. The frame-stability and frame-surface-reuse invariants are marked DELIVERED. ICW-P0-BUFFER-REUSE-SYNC, ICW-021, ICW-317, and ICW-318 marked Done.

## Validation Evidence

- Windows tests: 18/18 pass, including the rewritten pool pipeline tests.
- Core tests: 156/156 pass, including 2 shell wiring tests.
- App Release build: compiles with no CS errors. Relink succeeds when the app is closed.

## Recommended Next Step

1. Close the running app.
2. Rebuild and fast-scroll for 30+ seconds.
3. Confirm the black bands are gone.
4. Watch for `AccessViolationException` in the app logs.
5. If any band remains, the next lever is ICW-007 (pool the annotation overlay elements).
