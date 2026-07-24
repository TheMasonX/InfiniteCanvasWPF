# ADR-0002: Inspection Raster and Annotation Layers

- Status: Accepted
- Date: 2026-07-23

## Context

The canvas models a web inspection system where large monochrome source images are annotated with
classified defects. The previous demo continuously ingested point records and rendered each visible
record as one pixel. Frame rendering also read mutable camera state multiple times and queried the
spatial index again to update statistics, which allowed interaction and presentation to drift apart.

Eight `8192x2048` source images require 128 MiB as Gray8 data and 512 MiB as BGRA32 data before WPF
surface overhead. Annotation labels, tooltips, and animated selection also require hit-testable elements.

## Decision

- Generate deterministic sample tiles as Gray8 arrays in `InfiniteCanvas.Rendering`, with configurable
  target value, noise, object count, layout, and seed.
- Store annotation metadata independently and index annotation bounds in the existing STR tree.
- Compose visible source pixels and filled defect samples into the unmanaged BGRA32 viewport surface.
- Present bounding boxes and centered IDs as a retained WPF overlay so hover, selection, and animation
  do not require raster hit testing.
- Capture one immutable camera snapshot per frame and use one spatial query for raster composition,
  overlay layout, and visible-item statistics.
- Generate the static inspection scene once. Do not run the former periodic point-ingestion timer.

## Consequences

The source image memory remains bounded near 128 MiB, independent of viewport dimensions. Background
and annotation geometry update atomically from the same transform and query result. Native WPF tooltips
and animations remain straightforward and accessible. The overlay contains only visible annotations,
but it is rebuilt after camera changes; a future high-density scene may need element pooling or a custom
retained visual layer.
