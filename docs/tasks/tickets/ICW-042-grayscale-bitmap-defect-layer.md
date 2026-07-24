# ICW-042: Grayscale Bitmap Defect Layer

- Status: Done
- Priority: High
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Correct the sparse defect image regression. Defect imagery must be grayscale `System.Drawing.Bitmap` input rendered unaltered on an image layer separate from annotation shape overlays.

## Scope

- Generate each defect template as a `System.Drawing.Bitmap`.
- Fill the bitmap with grayscale value 150 by default.
- Draw 5-10 grayscale circles with GDI+ `Graphics` drawing methods.
- Preserve bitmap pixels as grayscale; do not apply class colors or other tinting.
- Do not clip or rescale the defect image to the annotation bounding box.
- Keep sparse image composition separate from bounding boxes, labels, and selection shapes.

## Validation

- Assert generated defect pixels have equal R/G/B channels and expected dimensions/content.
- Assert renderer output preserves source grayscale channels.
- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Findings

- The inherited ICW-039 implementation centers shared templates but the runtime result remains class-colored and visually clipped to annotation bounds.
- This contradicts the required separation between sparse raster imagery and shape overlays.
- Windows annotations now retain pooled `System.Drawing.Bitmap` inputs rather than discarding them before composition.
- Templates use grayscale fill 150 and 5-10 darker GDI+ circles.
- The sparse pass copies source intensity equally to B/G/R over the bitmap extent and no longer clips iteration to logical annotation bounds.
- A Windows test verifies grayscale 150 outside the logical box; full suite passed 32/32.

## Next Step

- Add deterministic bitmap-pool disposal ownership alongside ICW-029 regeneration/shutdown lifecycle hardening.