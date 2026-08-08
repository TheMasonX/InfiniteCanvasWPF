# External Material Source and Annotation Readiness Audit

**Description:** Net-new readiness review for image-source neutrality, overlapping scanner tiles, external annotations, and sample-data isolation.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** Current working tree after the external material inspection readiness audit.
**ID Hash:** `external-material-source-annotation-readiness-2026-08-08`
**Author:** GitHub Copilot, InfiniteCanvas Agent
**Review mode:** Findings only. This audit extends the 2026-08-08 external material readiness audit.
**Scope:** Background tile source boundaries, horizontal overlap composition, camera-column geometry, annotation inputs, and deterministic demo ownership.

## Executive Summary

The repository has a source-neutral tile cache foundation.
The active render path is not image-source agnostic at its type boundary.
It still consumes `SampleImageTile` and `SampleAnnotation`.

The current sample layout uses abutting rectangular tiles.
The renderer draws intersecting tiles in input order.
No explicit left or right overlap preference exists.
No camera-column rule rejects vertical overlap.

The generic `ICanvasItem` contract carries identity and bounds only.
The active raster, tooltip, and defect paths still require sample annotation data.
The application also invokes the deterministic sample generator directly.

The repository is not ready for an external image and annotation host.
The highest-risk new gaps are overlap policy and the annotation adapter boundary.

## Review Method and Coverage

The review read the source-neutral tile contracts, materializer, sample generator, sample tile, raster compositor, scene source, canvas frame, and annotation paths.
The review compared the current source with ADR-0005, ADR-0007, the requirements registry, and ICW-076 through ICW-341.
The review did not modify source code or run Core tests, Windows tests, the App build, or WPF runtime evidence.
Documentation validation is the only validation performed for this audit.

## Table of Findings

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task |
| --- | --- | --- | --- | --- | --- | --- | --- |
| S-002 | Sample ownership remains in reusable Rendering | Standards | New | Confirmed | P1 | 98% | ICW-343, ICW-076 |
| F-004 | Active composition is not source agnostic | Spec | New | Confirmed | P0 | 98% | ICW-076, ICW-339, ICW-340, ICW-343 |
| F-005 | Scanner overlap has no deterministic policy | Spec | New | Confirmed | P0 | 98% | ICW-340, ICW-341, ICW-343 |
| F-006 | External heterogeneous annotations lack an adapter boundary | Spec | New | Confirmed | P0 | 98% | ICW-314, ICW-340, ICW-341, ICW-343 |
| F-007 | Deterministic demo data is not fully extracted | Spec | New | Confirmed | P1 | 98% | ICW-050, ICW-343 |

## Findings

### S-002 Sample Ownership Remains in Reusable Rendering

**Axis:** Standards
**Task disposition:** New task and ICW-076 extension
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 98%

`SampleImageGenerator`, `SampleImageTile`, `SampleImageTileSource`, and `SampleAnnotation` remain in `InfiniteCanvas.Rendering`.
The active application creates these objects directly.
The renderer accepts the sample tile and annotation types directly.

This boundary creates divergent change risk.
An external source requires edits to reusable rendering code instead of an adapter.
The sample generator also changes when the reusable source contract changes.

**Evidence:**

- [SampleImageGenerator.cs](../../src/InfiniteCanvas.Rendering/SampleImageGenerator.cs) owns scene generation and random data.
- [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs) contains `SampleAnnotation` and tile-owned materialization.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) calls `SampleImageGenerator.GenerateSet` and flattens sample annotations.
- [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs) accepts `SampleImageTile` and `SampleAnnotation`.

**Recommendation:** Complete ICW-343 and keep deterministic sample adapters in the App or test fixture boundary.
Keep ICW-076 responsible for one source-neutral materialization owner.

### F-004 Active Composition Is Not Source Agnostic

**Axis:** Spec
**Task disposition:** New task and existing task extension
**Verification:** Confirmed
**Severity:** P0
**Confidence:** 98%

The materializer uses source-qualified requests internally.
The active composition path still receives `IReadOnlyList<SampleImageTile>` and `Dictionary<string, BackgroundTilePayload>`.
The rasterizer looks up payloads by `tile.Id`.
The scene source indexes concrete `SampleAnnotation` values.

The cache contract therefore supports external sources only before the raster boundary.
An external source cannot replace the sample source without changing active rendering types.
Equal tile IDs also remain unsafe when sources, revisions, or mip levels differ.

**Evidence:**

- [BackgroundTileContracts.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs) defines the neutral descriptor and complete cache key.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) creates the tile-ID-only resident payload map.
- [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs) retrieves resident payloads by tile ID.
- [ICanvasItem.cs](../../src/InfiniteCanvas.Core/ICanvasItem.cs) exposes only stable identity and bounds.

**Recommendation:** Preserve complete tile identity through the frame and raster inputs.
Replace sample-specific render inputs with neutral tile and annotation render records.

### F-005 Scanner Overlap Has No Deterministic Policy

**Axis:** Spec
**Task disposition:** New task extension
**Verification:** Confirmed
**Severity:** P0
**Confidence:** 98%

The sample generator places each tile at a column and row multiple of its native width and height.
The layout therefore has no horizontal overlap.
The compositor draws every visible tile into the same destination pixels in input order.
No contract carries camera-column identity, overlap preference, or seam ownership.

Two side-by-side scanner cameras cannot define whether the left or right camera wins in shared coverage.
The result can change when a source changes enumeration order.
The code also has no validation that tiles in one camera column do not overlap vertically.

**Evidence:**

- [SampleImageGenerator.cs](../../src/InfiniteCanvas.Rendering/SampleImageGenerator.cs) uses `tileX = column * PixelWidth` and `tileY = row * PixelHeight`.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) passes all visible tiles to the compositor without an overlap plan.
- [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs) writes each resident tile directly into the destination.
- No existing task or requirement defines left preference, right preference, or camera-column vertical validation.

**Recommendation:** Add camera-column metadata and explicit left or right precedence to the material layer plan.
Use the same policy for raster composition, pixelometer reads, visibility, selection, and published layer inputs.
Reject vertical overlap within one camera column.

### F-006 External Heterogeneous Annotations Lack an Adapter Boundary

**Axis:** Spec
**Task disposition:** New task extension
**Verification:** Confirmed
**Severity:** P0
**Confidence:** 98%

The canvas can carry `ICanvasItem` values, but the active annotation path remains concrete.
Raster defect patches, tooltip content, feature rows, and selection writeback use `SampleAnnotation`.
The item contract has no kind, draw order, style, label policy, tooltip payload, or optional image data.

An external host cannot provide defects, markers, and regions through one neutral adapter.
The host would need to change reusable rendering code or convert all objects into the sample type.

**Evidence:**

- [ICanvasItem.cs](../../src/InfiniteCanvas.Core/ICanvasItem.cs) exposes only `Id` and `Bounds`.
- [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs) defines the sample annotation data and defect pixel payload.
- [DeferredAnnotationToolTip.cs](../../src/InfiniteCanvas.Rendering/DeferredAnnotationToolTip.cs) accepts `SampleAnnotation`.
- [AnnotationFeaturePresenter.cs](../../src/InfiniteCanvas.Rendering/AnnotationFeaturePresenter.cs) accepts `SampleAnnotation`.
- [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs) draws defect patches from `SampleAnnotation`.

**Recommendation:** Define a host adapter that supplies neutral annotation identity, bounds, kind, order, display settings, and optional visual or tooltip data.
Keep domain objects outside Core, Controls, and source-neutral rendering contracts.

### F-007 Deterministic Demo Data Is Not Fully Extracted

**Axis:** Spec
**Task disposition:** New task
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 98%

The deterministic generator is useful for tests and benchmarks.
The application also invokes it as the production scene source.
The generator owns sample classifications, defect templates, labels, noise, tile geometry, and annotation creation.

This coupling prevents a clean external-host contract.
It also makes source-neutral rendering changes depend on demo settings and sample object construction.

**Evidence:**

- [SampleImageGenerator.cs](../../src/InfiniteCanvas.Rendering/SampleImageGenerator.cs) owns random seeds, tile geometry, noise, defect templates, and annotations.
- [AnnotationGenerator.cs](../../src/InfiniteCanvas.Rendering/AnnotationGenerator.cs) creates concrete sample annotations.
- [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) invokes the generator during scene regeneration.
- [ICW-050](../tasks/tickets/ICW-050-deterministic-threadsafe-tile-generation.md) records deterministic generation, but does not isolate it from reusable rendering.

**Recommendation:** Move sample generation behind an application or test fixture adapter.
Retain deterministic serial and parallel parity tests.
Do not make reusable contracts depend on random sample data.

## Readiness Assessment

| Capability | Status | Evidence |
| --- | --- | --- |
| Source-qualified cache identity | Partial | Materializer and cache key preserve source, tile, revision, and mip internally. |
| External tile composition | Not ready | Active raster inputs use `SampleImageTile` and tile-ID-only payload lookup. |
| Horizontal scanner overlap | Not ready | No precedence, camera-column identity, or vertical non-overlap validation exists. |
| External annotation input | Not ready | Active raster and tooltip paths require `SampleAnnotation`. |
| Deterministic demo fixture | Available but coupled | Generator is deterministic, but App and Rendering use it directly. |
| External host evidence | Not ready | Existing host tests do not cover overlap or heterogeneous annotations. |

## Recommended Sequence

1. Complete ICW-076 and ICW-339 for source-qualified identity and one materialization owner.
2. Extend ICW-340 with overlap metadata, explicit precedence, and atomic layer-plan publication.
3. Implement ICW-343 for the neutral annotation adapter and sample-data extraction.
4. Extend ICW-341 with two overlapping scanner columns and defect, marker, and region fixtures.

## Validation

- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Result: Passed after the initial durable records. Source and runtime tests were not run because this audit makes documentation changes only.

## Related Artifacts

- [External material inspection readiness audit](external-material-inspection-readiness-audit-26-08-08-12-35-58.md)
- [ICW-076](../tasks/tickets/ICW-076-background-tile-mip-levels.md)
- [ICW-314](../tasks/tickets/ICW-314-canvas-selection-and-tooltip-ownership.md)
- [ICW-337](../tasks/tickets/ICW-337-external-material-inspection-readiness.md)
- [ICW-339](../tasks/tickets/ICW-339-semantic-material-viewport-identity.md)
- [ICW-340](../tasks/tickets/ICW-340-atomic-material-layer-plan-publication.md)
- [ICW-341](../tasks/tickets/ICW-341-external-host-parity-and-runtime-stress.md)
- [ICW-343](../tasks/tickets/ICW-343-external-material-and-annotation-adapters.md)
- [ADR-0008](../ADR/0008-external-material-and-annotation-adapters.md)