# Wave U Handoff, Selection Ownership

## Status

Wave U is complete. The viewport MVP audit identified selection ownership as the highest-value missing functional slice after the reusable control extraction.

## Delivered

- Added `CanvasItemHitTesting` in Core for host-neutral world-point containment.
- Added `CanvasSelectionChangedEventArgs` with nullable `ICanvasItem` selection state.
- Made `CanvasControl` own selected-item state and viewport-point selection.
- Converted an un-dragged left click into a world-point query through `ICanvasSceneSource`.
- Preserved drag panning without changing selection.
- Added empty-space deselection.
- Routed `MainWindow` inspection updates through the control selection event.
- Added consumer-host regression coverage.

## Evidence

- Core tests pass, `196/196`.
- Windows tests pass, `26/26`.
- App Release build passes with the known unused `_frameClaimantId` warning.
- Task tracker validation passes, `224` task files validated and `5` legacy files skipped.
- `git diff --check` passes.

## Review Findings

The viewport audit remains correct about the remaining tooltip gap. Tooltip payload and tooltip lifecycle still depend on typed annotation metrics under ICW-031. The control now owns point selection, but host-specific annotation visuals remain in `MainWindow` until the next interaction slice.

The first contract edit exposed a known editor-tool persistence mismatch. The implementation uses a new Core helper instead of relying on an interface default member. The real on-disk source and all validation commands confirm the helper path.

## Next Step

Implement typed annotation metrics, then move tooltip payload and tooltip display lifecycle into the control under ICW-314.
