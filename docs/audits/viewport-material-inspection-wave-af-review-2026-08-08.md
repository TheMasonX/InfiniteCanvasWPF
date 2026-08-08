# Viewport Material Inspection Wave AF Review

## Executive Summary

This review covers the reusable viewport, materializer, frame identity, and raster handoff changes in the working tree.

The wave fixes selected mip suppression, source-qualified raster payload lookup, semantic stale-frame rejection, frozen raster ownership, and pre-commit layer publication.

The wave does not prove external host parity, item state immutability, or runtime stress behavior.

Standards findings: one duplicated fallback-selection implementation remains as a P2 cleanup.

Spec findings: four existing P1 or P0 tasks remain open. No new task key is required.

## Standards Findings

### S-001. Fallback selection logic is duplicated

Severity: P2.
Confidence: 0.98.
Axis: Standards.
Status: Correction to ICW-076.

`BackgroundTileMaterializer.TryGetBestResident` and `ZeroCopyBitmapFactory.TryGetBestResidentPayload` implement the same source, tile, revision, distance, and tie-break rules.

The duplication can drift and produce different fallback payloads in cache diagnostics and raster composition.

Keep the materializer as the owner of policy, or extract one source-neutral selector used by both paths.

Evidence: `src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs:109` and `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:302`.

## Spec Findings

### F-001. Item state remains mutable after frame publication

Severity: P1.
Confidence: 0.99.
Axis: Spec.
Status: Existing ICW-338 finding.

`CanvasFrame` owns the item sequence, but it retains each caller-owned `ICanvasItem` instance.

A mutable item can change its ID or bounds after publication and invalidate selection, overlay placement, or frame identity.

Define a host-neutral immutable item snapshot or an explicit item stability contract before closing ICW-338.

Evidence: `src/InfiniteCanvas.Controls/CanvasFrame.cs:84` and `src/InfiniteCanvas.Core/ICanvasItem.cs:1`.

### F-002. Scene change delivery is typed but not control-owned

Severity: P1.
Confidence: 0.95.
Axis: Spec.
Status: Existing ICW-339 finding.

`ICanvasSceneSource.SceneChanged` now carries `CanvasFrameIdentity`, but `CanvasControl` does not subscribe to the source event.

A host must still translate source changes into render requests and can publish frames with stale source state.

Define the control subscription and lifetime behavior, or document the host obligation as part of the external contract.

Evidence: `src/InfiniteCanvas.Core/ICanvasSceneSource.cs:38` and `src/InfiniteCanvas.Controls/CanvasControl.xaml.cs:88`.

### F-003. Layer callback failure has no rollback contract

Severity: P1.
Confidence: 0.93.
Axis: Spec.
Status: Existing ICW-340 finding.

`CanvasControl.PublishFrame` invokes `FrameLayersPublishing` before raster and view-model updates.

Stale frames are rejected before the callback, but an exception or partial mutation inside the host callback has no rollback path.

Use an immutable layer-content plan, validate it before visual mutation, and define failure behavior for the host composer.

Evidence: `src/InfiniteCanvas.Controls/CanvasControl.xaml.cs:246` and `src/InfiniteCanvas.App/MainWindow.xaml.cs:732`.

### F-004. Same-epoch completion ordering lacks direct evidence

Severity: P1.
Confidence: 0.91.
Axis: Spec.
Status: Existing ICW-076 and ICW-341 finding.

The materializer rejects stale scene completions and coalesces equal keys.

No focused test proves the cancel and re-request ordering for two workers in the same scene epoch with one resident result and one callback.

Add the deterministic completion test before declaring the external material source boundary complete.

Evidence: `src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs:250` and `tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs:1`.

### F-005. Legacy tile-owned materialization remains reachable

Severity: P1.
Confidence: 0.98.
Axis: Spec.
Status: Existing ICW-076 finding.

The application render path uses `BackgroundTileMaterializer`, but `SampleImageTile` still exposes tile-owned resident and generation methods.

The legacy raster overload also remains public inside the rendering assembly.

Remove or isolate the legacy active path after existing compatibility tests migrate to the materializer contract.

Evidence: `src/InfiniteCanvas.Rendering/SampleImageTile.cs:263` and `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:129`.

## Corrections and Extensions to Existing Tasks

- ICW-076 now has active materializer requests, exact selected-mip requests, mip-zero pixelometer reads, complete-key raster lookup, and adapter coverage.
- ICW-338 now owns the item sequence, exposes read-only payload bytes, and rejects unfrozen rasters. Item state stability and concurrent-read evidence remain open.
- ICW-339 now carries source, scene, layer, display, selection, and render identity. Source-session replacement and semantic stale-frame tests pass.
- ICW-340 now invokes host layer composition inside the accepted control boundary. Layer rollback and immutable layer content remain open.
- ICW-341 remains an evidence gate. No runtime stress claim is made by this wave.

## Priority Order

1. P1: Define item state stability and concurrent-read evidence under ICW-338.
2. P1: Add same-epoch materializer completion evidence under ICW-076 and ICW-341.
3. P1: Define layer callback failure and rollback behavior under ICW-340.
4. P1: Decide whether `CanvasControl` subscribes to `SceneChanged` under ICW-339.
5. P2: Consolidate fallback selection policy under ICW-076.
6. P1: Run external host and WPF lifecycle stress evidence under ICW-341.

## Open Questions and Validation Gaps

- Product confirmation is still required for the external layer content model.
- The current test suite does not run a real WPF navigation, regeneration, failure, and close stress trace.
- The current test suite does not test concurrent mutation of an item implementation.
- Full solution validation, task validation, and whitespace validation remain pending for this wave.

Summary: Standards has one P2 duplication finding. Spec has five existing-task findings, with item state stability as the highest correctness gap.
