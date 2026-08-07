# Wave W Handoff, Tooltip Lifecycle Ownership

## Status

Wave W implements the ICW-314 tooltip lifecycle slice. The control owns tooltip attachment and cleanup for host-created item visuals.

## Delivered

- Added `CanvasControl.RegisterItemVisual` with optional tooltip text.
- Added a deferred control-local tooltip wrapper.
- Cleared registered tooltips before each accepted frame and during `ClearFrame`.
- Removed direct `DeferredAnnotationToolTip` construction from `MainWindow`.
- Kept annotation formatting in the app presenter.
- Added a consumer-host regression for registration and frame cleanup.
- Preserved the stable `ICanvasItem` Id and Bounds contract.

## Review Findings

The prior Wave V summary correctly identified ICW-314 as the next slice. The current `ICanvasItem` file remains the minimal Id and Bounds contract, so this wave does not claim an interface extension.

The control now owns tooltip object creation and lifecycle. The host still supplies formatted text and creates application-specific annotation visuals. A later slice can extend the item contract if a reusable host-neutral formatter is required.

## Evidence

- Focused consumer-host tests pass, `6/6`.
- Core tests pass, `197/197`.
- Windows tests pass, `27/27`.
- App Release build passes with the existing `_frameClaimantId` warning.
- Task tracker validation passes, `225` task files validated and `5` legacy files skipped.
- `git diff --check` passes.

## Next Step

Commit and push Wave W. Revisit an item-level tooltip provider only if another host needs to supply payloads without host-side formatting.
