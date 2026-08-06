# Handoff: Wave I — Cancellation Registration, Boundary Revision Wiring, and Allocation Cleanup

Date: 2026-08-06

## Status

Wave H (assembly extraction) was reviewed first and is fully landed: the
working tree builds with 0 errors and both suites pass at their Wave H
baseline (core 181/181, Windows 21/21). The review did not find a regression
introduced by Wave H; instead it surfaced four live findings from the
untracked 2026-08-05 audit reports, which became the Wave I scope. All four
are now fixed, tested, and validated.

## What Landed

### ICW-327 — Refresh the claimant cancellation registration on `AddClaimant` re-coalesce

`TileWorkItem.AddClaimant` updated only callbacks when a claimant re-coalesced
onto an already-tracked work item. The registration stayed bound to the
original frame token, which the host cancels every frame. A spent registration
can never fire again, so the claimant became permanently uncancellable. This
defeated claimant-token cancellation for exactly the multi-frame generations
ICW-204 was built to handle.

The re-coalesce branch now disposes the old registration, registers the newest
token, and handles the synchronous-fire case exactly like the ICW-320 F-014
first-add path. New regression test
`ReCoalescedClaimant_RegistersNewestToken_CancelStopsWork` was verified to
fail on the buggy shape (temporary revert) and passes with the fix.

### ICW-328 — Wire `CanvasFrame.Revision` as a real stale-frame guard

`CanvasFrame.Revision` was documented as the stale-frame revision identity
(ICW-316A) but was always zero and never consumed. The host now threads the
`RenderRequestTracker` request version into the frame, and the control
discards any frame older than the last one displayed. Equal revisions are
accepted as an idempotent republish, and the first frame (even with the
default revision of zero) is always accepted. This makes the ICW-316A
"revision identity" acceptance claim behaviorally true. New consumer-host
test `ConsumerHost_StaleFrameRevision_IsDiscarded`.

### ICW-329 — Remove allocate-and-sort-under-lock in `TryGetBestResidentMip`

The pixelometer fallback read path allocated a `List` and sorted it with LINQ
under `_cacheGate`. The method is reachable from the intended long-term
pixelometer path `TryGetResidentPixels` for the mip-not-resident case. Replaced
with a single-pass scan over the bounded mip dictionary. Selection order is
preserved: smallest absolute mip distance, then lower mip level (higher
resolution) at equal distance. New tiebreak parity test
`ResidentRead_EqualDistance_PrefersHigherResolutionMip`.

### ICW-330 — Clarify the coordinator lock contract and `SetRunning` query semantics

Three low-risk cleanup items:

1. `CancelWorkItem` reused the mutating `SetRunning()` transition purely to
   query prior state, flipping a queued item's `_running` flag as a side
effect. Added a non-mutating `IsRunning()` and used it for the query.
   Historical pass6 finding #4 (2026-07-27), which had survived Wave F and
   Wave G hardening.
2. Documented the caller-held-lock contract on `StartWorkItem` and
   `CancelWorkItem` (ICW-322 documentation pattern).
3. Made the `Request` coalesce comment name the real stale-result discard
   mechanism (`_pixels is null` first-writer check plus the epoch comparison;
   the eviction case does not bump the epoch).

## Files Touched

- src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs (ICW-327, ICW-330)
- src/InfiniteCanvas.Rendering/SampleImageTile.cs (ICW-329)
- src/InfiniteCanvas.Controls/CanvasFrame.cs (ICW-328 contract doc)
- src/InfiniteCanvas.Controls/CanvasControl.xaml.cs (ICW-328 guard)
- src/InfiniteCanvas.App/MainWindow.xaml.cs (ICW-328 revision wiring)
- tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs (ICW-327 test)
- tests/InfiniteCanvas.Tests/SampleImageTileTests.cs (ICW-329 tiebreak test)
- tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs (ICW-328 test)
- docs/tasks/tickets/ICW-327/328/329/330 (new or updated)
- docs/tasks/active-tasks.md, docs/tasks/JIRA.md (tracker)
- docs/audits/ untracked 2026-08-05 audit reports (committed this wave)

## Validation Evidence

- Solution Release build: 0 errors.
- Core suite: 183/183 pass (was 181 at Wave H; +1 ICW-327 regression test, +1 ICW-329 tiebreak test).
- Windows suite: 22/22 pass (was 21 at Wave H; +1 ICW-328 stale-frame discard test).
- ICW-327 regression test confirmed to fail on the buggy shape before the fix
  was restored.

## Decisions Taken

- Wire `CanvasFrame.Revision` for real rather than remove it, so the ICW-316A
  acceptance claim becomes behaviorally true.
- The stale-frame guard discards only strictly older revisions
  (`frame.Revision < _lastPublishedRevision`). Equal revisions are accepted as
  an idempotent republish; this keeps hosts that pass a constant revision
  working while still rejecting out-of-order frames.
- `_lastPublishedRevision` starts at `int.MinValue` so a host's first frame
  with the default revision of zero is accepted.

## Open Items and Recommended Next Step

- ICW-313 (IInputHandler abstraction) and ICW-314 (selection and tooltip
  ownership) remain the ADR-0007 next steps. Both are user-deferred.
- ICW-324 (seamless-noise decision) and ICW-325 (anisotropic mip selection)
  still need product decisions.
- ICW-144 needs fresh fast-scroll BenchmarkDotNet evidence on target hardware.
- The remaining untracked audit findings are now all committed with this wave.
  The next audit session can start from a clean tree.

