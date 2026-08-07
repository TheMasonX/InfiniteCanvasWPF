# Wave AD Handoff, Rendering Abstraction Cleanup

Date: 2026-08-07
Status: Complete

## Review Result

Wave AC passes its reported core and Windows validation on the current tree.
The commit also contains unrelated settings edits, so future reviews must inspect commit scope separately from test results.

The Wave AC sampler contract has no confirmed behavioral defect in the reviewed path.
The overlap test covers last-applicable-wins output, but the UI pixelometer text remains outside that regression.

## Delivered

- Deleted unreferenced `IRenderer`, `ViewportRenderRequest`, and `MipOptions` types.
- Retained `IBackgroundTileSource` because ADR-0005 and ADR-0007 define it as an intentional future boundary.
- Added source-neutral contract comments for the ICW-076 records and interface.
- Confirmed that the duplicate private `SampleImageGenerator.GenerateAnnotations` method is already absent.

## Evidence

- Core tests passed 198/198 before this wave.
- Repository search found no source, test, or benchmark consumer for the three deleted types.
- ADR search found active references to `IBackgroundTileSource`.
- The unrelated ICW-336 settings changes remain uncommitted and untouched.

## Review Correction

The original handoff contained a malformed `interface.+-` line. Wave AE records the correction without changing the Wave AD source scope.

## Next Step

Continue ICW-076 by connecting `IBackgroundTileSource` to the source-neutral materializer and cache.
Keep the one-host ICW-144 benchmark limitation explicit.