# Wave V Handoff, Typed Annotation Metrics

## Status

Wave V is complete. ICW-031 now provides typed Confidence and Severity metrics for generated annotations.

## Delivered

- Added `AnnotationMetrics` with typed Confidence and Severity values.
- Captured generated metric values once during annotation generation.
- Kept legacy feature rows for non-metric inspection fields.
- Marked the public `Features` dictionary obsolete.
- Migrated tooltip metric formatting to `AnnotationMetrics`.
- Added generated-metric and typed-presenter regression coverage.
- Added a zero-scale guard to viewport-point selection.

## Evidence

- Focused annotation presenter tests pass, `3/3`.
- Core tests pass, `196/196`.
- Windows tests pass, `26/26`.
- App Release build passes with the known unused `_frameClaimantId` warning.
- Touched-file diagnostics report no errors.
- Task tracker validation passes, `224` task files validated and `5` legacy files skipped.
- `git diff --check` passes.

## Review Findings

Wave U selection ownership is correct. The control converts viewport points through the captured camera, selects through the host-neutral scene contract, clears empty-space selection, and protects drag pan from selection changes.

Tooltip payload and tooltip lifecycle remain host-specific. The next slice is ICW-314, which moves those concerns into `CanvasControl` after the typed metrics boundary.

## Next Step

Extend the reusable item contract with tooltip payload data and move tooltip lifecycle ownership into `CanvasControl` under ICW-314.