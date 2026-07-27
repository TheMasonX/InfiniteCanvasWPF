---
id: ICW-130
author: Copilot
key: ICW-130
title: Draw world-scale background tile index text in raster output
status: Done
type: Story
priority: P2
tags:
  - rendering
  - background-tiles
  - labels
  - graphics
dependsOn: []
related:
  - ICW-040
  - ICW-076
  - ICW-097
links:
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/requirements/functional-requirements-and-invariants.md
created: 2026-07-26
updated: 2026-07-26
---

# Bake background tile index text into Gray8 tile mips

## Summary

Draw each background tile's stable tile index as dark-gray Gray8 content at the tile image's top-left while generating the tile and each requested mip. The label is diagnostic content for the synthetic inspection scene; it is intentionally rendered at the payload resolution so it becomes less sharp as lower-resolution mips are selected.

## Scope

- Thread the stable tile ID into synthetic tile and mip generation, alongside the existing circle rasterization.
- Use Windows GDI+ during Gray8 payload generation to rasterize the label with grayscale value `16`; the platform-specific implementation is intentional.
- Render the label into every requested mip at that mip's pixel dimensions so mip selection visibly affects label sharpness.
- Remove the later frame-level label pass from `ZeroCopyBitmapFactory`; labels must not be duplicated into the shared BGRA frame.
- Keep the existing background-image visibility, resident-mip fallback, and sparse defect layering behavior unchanged.

## Acceptance Criteria

- Every generated background tile payload can contain a stable index such as `TILE-01` at its local top-left corner.
- Labels are generated at each mip's native dimensions and become visibly less sharp when a lower-resolution mip is used.
- Labels remain tied to the same logical tile when resident-mip fallback selects another payload.
- Text does not write outside the Gray8 tile payload or the final mapped BGRA surface.
- Existing grid, sparse-image, annotation, cache, and mip rendering behavior remains intact.
- Add focused Windows rendering coverage for index text placement or, where pixel assertions are impractical, a testable projection/label-bounds helper plus a source-level wiring regression test.
- Avoid generation-time unmanaged resource leaks: dispose any `Graphics`, `Font`, `Brush`, `StringFormat`, or temporary bitmap resources created for text drawing.

## Validation

- Run: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release` — focused Windows tests passed 7/7
- Run: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release` — focused generator tests passed 24/24
- Run: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore` — succeeded with 12 pre-existing FastNoise2 binding warnings
- Result: Tile IDs are generated as value-16 Gray8 content alongside circles for native and requested mip resolutions; the frame-level label compositor was removed. Tests cover payload presence, mip dimensions/content, hidden background behavior, and partial tile composition.

## Notes

- Synthetic background pixels remain Gray8; the text is a diagnostic part of the generated image, not a WPF or frame overlay.
- System.Drawing is Windows-only and remains behind the existing `#if WINDOWS` boundary. Non-Windows builds retain the existing circle rasterizer and omit the platform-specific text operation.
- The label value is fixed at `16` and uses the tile ID supplied by `GenerateSet`; it must not be derived from visible-tile ordering.

## Related Tasks

- ICW-040: Existing camera-synchronized tile-grid overlay.
- ICW-076: Source-agnostic background tile mip levels and resident fallback.
- ICW-097: Synthetic Gray8 materialization and CPU-budget baseline.
