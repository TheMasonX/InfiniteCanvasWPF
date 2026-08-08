---
id: ICW-343
author: Copilot
key: ICW-343
title: Extract sample data and define external material and annotation adapters
status: Proposed
type: Story
priority: P0
tags:
  - external-viewport
  - source-agnostic
  - annotations
  - sample-data
  - adapters
dependsOn:
  - ICW-076
  - ICW-339
  - ICW-340
related:
  - ICW-337
  - ICW-341
links:
  - docs/ADR/0005-source-agnostic-background-tile-mips.md
  - docs/ADR/0007-canvas-reusable-component-boundary.md
  - docs/ADR/0008-external-material-and-annotation-adapters.md
  - docs/requirements/functional-requirements-and-invariants.md
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/SampleImageTileSource.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
created: 2026-08-08
updated: 2026-08-08
---

## Summary

Move random sample data behind an application or test adapter.
Define neutral material and annotation inputs for external hosts.

The current source-neutral tile cache is a partial boundary.
The active raster path still accepts sample tiles and sample annotations.

## Scope

- Remove sample tile and sample annotation ownership from reusable rendering contracts.
- Keep deterministic sample generation in the demo application or a test fixture.
- Let an external tile adapter provide arbitrary bounds and camera-column metadata.
- Add explicit left or right preference for horizontal tile overlap.
- Reject vertical overlap between tiles in one camera column.
- Let an annotation adapter map defects, markers, regions, and other host objects to neutral render information.
- Carry annotation kind, identity, bounds, order, style, label policy, tooltip data, and optional image data through the frame boundary.
- Keep external domain types outside `InfiniteCanvas.Core`, `InfiniteCanvas.Controls`, and source-neutral rendering contracts.

## Acceptance Criteria

- The reusable canvas and raster contracts contain no `SampleImageTile`, `SampleAnnotation`, or random-data generator dependency.
- The demo application and test fixtures still create deterministic sample tiles and annotations with explicit seeds.
- An external tile source can supply two side-by-side camera columns with horizontal overlap.
- The overlap policy explicitly selects the left or right tile, independent of input list order.
- Validation rejects vertical overlap between tiles assigned to the same camera column.
- One neutral annotation adapter can expose at least defect, marker, and region inputs without generic application types in Core.
- Annotation settings support type visibility, style, label policy, and draw order.
- The accepted frame carries tile composition metadata and annotation settings with the same identity and revision rules.
- A consumer-host test uses external adapter data and does not call `SampleImageGenerator` from the reusable control path.
- Deterministic sample tests retain pixel, geometry, and annotation parity coverage after extraction.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`; `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`; `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Result: Pending design and implementation. Current source review confirms that the materializer contracts are partial and that sample tile and annotation types remain in the active raster path.

## Notes

ICW-076 owns tile materialization and full cache identity.
ICW-340 owns atomic layer publication.
This task owns the adapter boundary and removal of sample-data ownership from reusable paths.
ADR-0008 records the proposed boundary and the open API shape.

## Related Tasks

- ICW-076
- ICW-337
- ICW-339
- ICW-340
- ICW-341