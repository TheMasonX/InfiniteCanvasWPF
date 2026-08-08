# External Material Inspection Viewport Readiness Delta

Date: 2026-08-07

Scope: Current readiness for an external application material inspection viewport.

Constraint: Findings only. This audit does not change source behavior.

## Executive Summary

The repository provides a reusable WPF canvas foundation. It is not ready to replace an external material inspection viewport.

The highest risk is semantic. The current frame revision identifies render order, but it does not identify the external source, layer revisions, display state, or selection state that produced the frame. The host also publishes the raster and overlay state through separate operations.

The background materializer exists, but the active demo render path still uses `SampleImageTile` factories and synthetic cache keys. The consumer-host tests prove construction and generic frame publication. They do not prove external material source parity or WPF runtime stress behavior.

This delta creates one readiness epic and four focused child tasks. It updates ICW-076 because that task already owns materializer migration. It does not reopen completed selection, tooltip, stale integer revision, mip selection, cache accounting, or buffer fencing work.

## Standards Findings

### S-001 Published frame and payload ownership is shallow

Severity: P1

Confidence: High

Classification: Contract defect and primitive ownership smell.

Evidence:

- `CanvasFrame` stores the caller's item list directly at [CanvasFrame.cs](src/InfiniteCanvas.Controls/CanvasFrame.cs#L76), although its contract describes one frozen frame.
- `BackgroundTilePayload` stores and returns the caller's writable byte array at [BackgroundTileContracts.cs](src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs#L139).
- The materializer publishes the payload into its resident cache at [BackgroundTileMaterializer.cs](src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L205).

Impact:

A caller can mutate item membership or resident pixels after publication. A host can then observe overlay state that differs from the accepted frame, or a renderer can read cache bytes while another component changes them. The current `IReadOnlyList` and `byte[]` types restrict the interface surface but do not provide ownership.

Required direction:

Define ownership at both boundaries. Snapshot the item sequence during frame construction. Expose payload bytes through an immutable or read-only representation, or copy at the cache boundary. Add mutation and concurrent-read regression tests.

Task: [ICW-338](../tasks/tickets/ICW-338-immutable-frame-and-payload-ownership.md).

## Spec Findings

### F-001 External source and frame identity are incomplete

Severity: P0

Confidence: High

Evidence:

- `ICanvasSceneSource` exposes scene bounds, counts, queries, resident pixels, and an unqualified `SceneChanged` event. It has no source identity, source health, layer revision, or revision vector at [ICanvasSceneSource.cs](src/InfiniteCanvas.Core/ICanvasSceneSource.cs#L8).
- `CanvasFrame` carries only an integer render revision at [CanvasFrame.cs](src/InfiniteCanvas.Controls/CanvasFrame.cs#L112).
- `CanvasControl` rejects only lower integer revisions at [CanvasControl.xaml.cs](src/InfiniteCanvas.Controls/CanvasControl.xaml.cs#L225).

Impact:

A newer integer frame can still contain an older material source or stale layer. The canvas cannot prove that raster pixels, annotations, pixelometer data, and selection state describe the same external material revision.

Required direction:

Add source-qualified session identity and a semantic revision vector. Include source, scene, layer, display, selection, and render sequence identity in the accepted frame. Change notifications and stale-frame checks must use that identity.

Task: [ICW-339](../tasks/tickets/ICW-339-semantic-material-viewport-identity.md).

### F-002 Raster and overlay publication is not atomic

Severity: P0

Confidence: High

Evidence:

- `PublishFrame` assigns the raster and applies view-model state before raising `FramePublished` at [CanvasControl.xaml.cs](src/InfiniteCanvas.Controls/CanvasControl.xaml.cs#L216).
- The application composes tile-grid and annotation layers from that event at [MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L710), [MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L715), and [MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L716).
- The host obtains internal overlay canvases through `GetOverlayHost` at [CanvasControl.xaml.cs](src/InfiniteCanvas.Controls/CanvasControl.xaml.cs#L163).

Impact:

The control can accept a raster and item list while host overlay work is still pending. A rejected or superseded frame can also leave host-composed visuals from a different accepted state unless every callback is guarded independently.

Required direction:

Build one immutable layer render plan from one captured viewport snapshot. Publish raster, ordered layer inputs, pixelometer provenance, and frame identity as one accepted frame. Rejected frames must update no visual layer.

Task: [ICW-340](../tasks/tickets/ICW-340-atomic-material-layer-plan-publication.md).

### F-003 The source-neutral materializer is not the active material path

Severity: P0

Confidence: High

Evidence:

- `BackgroundTileMaterializer` exists as a source-neutral service at [BackgroundTileMaterializer.cs](src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L6).
- `MainWindow` still creates `BackgroundTileCacheKey` values with the literal `synthetic` source identity at [MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L541).
- `SampleImageTile` still creates synthetic cache keys and owns pixel factories at [SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L390), [SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L550), and [SampleImageTile.cs](src/InfiniteCanvas.Rendering/SampleImageTile.cs#L758).

Impact:

An external source cannot replace the active tile path without bypassing the materializer or changing demo-owned rendering objects. The source-neutral contracts therefore do not yet demonstrate external material inspection readiness.

Required direction:

Complete ICW-076. Route active tile requests through the materializer, remove synthetic identity from reusable render inputs, and make the Windows raster path consume validated resident payload dimensions.

Task: [ICW-076](../tasks/tickets/ICW-076-background-tile-mip-levels.md), extended in this audit.

### F-004 External host parity and runtime stress evidence are missing

Severity: P1

Confidence: High

Evidence:

- The consumer-host test publishes a generic raster and one generic item at [CanvasControlConsumerHostTests.cs](tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs#L17).
- The same fixture verifies integer stale-frame rejection, tooltip cleanup, and point selection. It does not provide an external material source, ordered material layers, semantic revisions, or a runtime host stress loop.
- ICW-144 covers coordinator and queue benchmarks. It does not exercise the WPF control, raster and overlay publication, resize, close, or scene replacement together.

Impact:

Unit and source-level evidence can pass while the external host still shows stale layers, resource leaks, unobserved exceptions, or frame instability under navigation and regeneration.

Required direction:

Add an application-like Windows host fixture and a repeatable runtime stress harness. Cover fast scroll, zoom, resize, scene regeneration, tile failure, and close during generation. Archive machine, build, and result metadata.

Task: [ICW-341](../tasks/tickets/ICW-341-external-host-parity-and-runtime-stress.md).

## Corrections and Extensions to Existing Tasks

- ICW-076 remains In Progress. The source-neutral materializer slice is real, but the active `SampleImageTile` and Windows raster path still use synthetic adapters. The next step in its ticket now includes the current source evidence and this audit.
- ICW-316A is complete for count validation, view-model invariants, lifecycle cleanup, and integer revision ordering. Its `CanvasFrame` immutability claim is incomplete because the constructor does not own the item list. ICW-338 narrows that correction without reopening the completed task.
- ICW-312 is complete for generic scene injection and resident-only pixel reads. F-001 extends the contract for an external material session. ICW-339 owns that extension.
- ICW-328 is complete for lower integer revision rejection. F-001 requires semantic source and layer identity in addition to that guard. ICW-339 does not replace ICW-328.
- ICW-314 and ICW-334 are not findings in this audit. Selection and tooltip lifecycle ownership now exist in `CanvasControl`.
- Existing master audit findings MA-P0-002, MA-P0-003, MA-P0-005, and MA-P0-006 remain valid. ICW-337 through ICW-341 convert their current-source acceptance direction into tracked work without creating duplicate keys.

## Priority Order

1. P0, ICW-339, define source, layer, display, selection, and render identity.
2. P0, ICW-340, publish raster and ordered layer state atomically.
3. P0, ICW-076, connect the active tile path to the source-neutral materializer.
4. P1, ICW-338, enforce immutable ownership for frame and payload inputs.
5. P1, ICW-341, prove external host parity and runtime behavior on Windows.

## Open Questions and Validation Gaps

- The external layer list, ordering, visibility rules, and dirty-layer policy need a fixed source of truth before ICW-340 implementation.
- The revision vector must define whether display settings and selection changes create a new material frame or only a new interactive overlay plan.
- Zero-copy surface lifetime still needs an explicit lease contract at the frame boundary. Existing composition fencing prevents the known reuse race, but it does not by itself define host ownership.
- This audit did not run source builds or tests before editing because the requested output is documentation and backlog analysis. The tracker and whitespace checks remain the required validation for this change set.

Finding count: Standards 1, Spec 4. Worst Standards issue: shallow ownership lets callers mutate published frame and cache data. Worst Spec issue: the current frame identity cannot prove that raster and overlays describe the same external material revision.