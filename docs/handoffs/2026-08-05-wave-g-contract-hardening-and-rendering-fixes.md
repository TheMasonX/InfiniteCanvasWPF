# Handoff: Wave G — Contract Hardening and Rendering Fixes

Date: 2026-08-05

## Status

Seven audit-synthesis backlog tickets implemented and validated. The canvas
boundary is hardened in place and its public surface is method-based, so the
ICW-316 physical assembly move is unblocked.

## What Landed

### Canvas boundary (ICW-316A, ICW-319)

- ICW-316A: single item-query authority. `ICanvasSpatialQuerySource` is
  deleted (F-001). `ICanvasSceneSource` is the one authority and gains the
  named `QueryPoint` point-query contract.
- ICW-316A: `CanvasFrame` validates item counts, raster dimensions against
  `ImageSource` metadata, and carries a `Revision` identity.
- ICW-316A: `CanvasViewModel` setters are private. `ApplyFrame` is the only
  mutation path and validates `VisibleItemCount <= TotalItemCount` plus
  items-list equality. Frame state raises one notification batch.
- ICW-316A: `CanvasControl` releases the anchor-pan timer, mouse capture,
  override cursor, pointer state, and scrollbar drag on `Unloaded`.
- ICW-316A: the host pixel read uses `QueryPoint` and scene-derived tile
  dimensions. The `0.01 x 0.01` probe and the `_tiles[0]`/`_tileColumns`
  layout assumptions are gone from the read sites.
- ICW-319: all 9 raw element members removed from the control public surface.
  MainWindow routes through `SetLoadingState`, `SetBusyIndicatorVisible`,
  `SetPixelometerReadout`, `ClearFrame`, `SetViewportSize`, `GetViewportSize`,
  `GetViewportPointer`, and the internal `GetOverlayHost`. The loading overlay
  is centered by layout, not a hardcoded margin.

### Rendering core (ICW-320, ICW-321, ICW-322, ICW-323, ICW-326)

- ICW-320: coordinator cancel-and-re-request window hardened (F-006, F-007,
  F-014). `Request` does not coalesce onto a terminal-state item,
  `HandleWorkStopped` removes only on reference equality, and `AddClaimant`
  adds before registering the token callback. Three regression tests, all
  failing on HEAD.
- ICW-321: dead `DefectBitmap` LockBits sampling removed from
  `DrawDefectPatch` (F-008). Output is byte-identical; display value comes
  from `DefectPixels` via the sampler.
- ICW-322: reentrant lock chain documented at all three sites (F-009).
  Assumption A-1 verified against the runtime: `System.Threading.Lock.Enter`
  re-enters for the current thread on net-10.0.
- ICW-323: `EpochWiringTests` guards the `BeginRequest`/`IsCurrent`/`Advance`
  wiring in `RenderFrameAsync` (F-013). It fails on the 2026-07-26 revert
  shape.
- ICW-326: the tile-grid overlay builds from the camera-visible tile set and
  skips unchanged rebuilds (F-012). `UpdateTileGridLayer` no longer touches
  `_tiles`.

## Validation Evidence

- Core suite: 179/179 pass (was 170 at HEAD; +9 net-new tests).
- Windows suite: 18/18 pass.
- Full solution Release build: 0 errors (pre-existing unused-field warning
  only).
- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`: clean.

## Decisions Taken

- Item-query authority (Q-2): consolidated on `ICanvasSceneSource`; the
  duplicate interface is deleted. Evidence: it had zero consumers at HEAD and
  ADR-0007 makes the scene source the content boundary. ICW-314 hit-testing
  consumes `QueryPoint`.
- ICW-322 chose documentation over the callback-outside-lock restructure. The
  chain is not blocking today and the restructure changes the reservation
  delegate shape; it is re-checked before ICW-P0-LEASE-RELEASE.

## Open Items and Recommended Next Step

- ICW-316 (physical move) is unblocked. Sequence it after the still-open
  ICW-324 (seamless-noise decision) and ICW-325 (anisotropic mip decision),
  which need product decisions. Update `CanvasScrollbarWiringTests` paths
  atomically during the move.
- ICW-320 landed before ICW-144 closes, so ICW-144 benchmark evidence now
  measures the fixed coordinator. Close ICW-144 with fresh fast-scroll
  BenchmarkDotNet evidence on target hardware.
- The remaining synthesis backlog is ICW-316 (move), ICW-324 (blocked on
  decision), ICW-325 (blocked on ADR-0005 alignment), plus the older ICW-081
  corpus batch.
