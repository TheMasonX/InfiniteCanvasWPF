# Wave Y Handoff, Anisotropic Mip Selection

## Status

Wave Y completes ICW-325.

## Prior-Wave Review

Wave X correctly clears each registered tooltip and the registration set before `DetachFrameShell` drops shell references. The consumer-host test covers the direct-detach symptom. The test does not prove garbage collection, but the source path clears both the attached tooltip and the registry, which satisfies the lifecycle contract.

## Delivered

- `BackgroundTileMipPolicy.SelectMipLevel` now uses the larger camera scale as the binding axis.
- ADR-0005 now states the anisotropic binding-axis rule.
- Added a discriminating non-uniform-camera regression test. The old `Math.Min` policy returns mip 2 for this input, while the corrected policy returns mip 0.
- Left the concurrent settings and property-editor changes untouched.

## Evidence

- Focused mip-policy tests pass, 2/2.
- Core tests pass, 198/198.
- Windows tests pass, 28/28.
- App Release build passes with the existing `_frameClaimantId` warning.
- Task tracker validation passes, 225 task files validated and 5 legacy files skipped.
- `git diff --check` passes.

## Findings and Residual Risk

The policy change affects which resident mip a non-uniform camera requests. A visual anisotropic zoom check remains useful when Windows runtime review is available. No source or test file from the concurrent settings/property-editor work was included.

## Next Step

Keep ICW-325 aligned with ADR-0005. Revisit visual mip selection during the next Windows rendering review.
