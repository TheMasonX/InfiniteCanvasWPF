# Wave AC Handoff, Defect Overlay Contract

Date: 2026-08-07
Status: Complete

## Review Result

Wave AB evidence is internally consistent. The corrected script passes exporters as separate arguments, archives four report files, and fails when no report exists. The archived run contains 18 parameterized benchmark cases, three warmups, and ten measured iterations.

The ICW-144 ticket still contains a residual acceptance statement about identifying the dominant improvement. The archived run does not provide a before-and-after comparison, so it cannot support that claim. Cross-machine comparison remains future work.

## Delivered

- Recorded last-applicable-wins precedence for overlapping defect annotations.
- Changed the resident pixel read path to use `DefectOverlaySampler`.
- Added a Windows pixel regression that compares overlapping renderer output with the shared sampler.
- Closed ICW-035 in the ticket and both task trackers.

## Evidence

- Focused Windows tests passed two selected `ZeroCopyBitmapFactoryTests` cases.
- The full core suite passed 198/198 tests.
- The full Windows suite passed 29/29 tests.
- The App Release build passed with the existing unused `_frameClaimantId` warning.
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ZeroCopyBitmapFactoryTests.GenerateFrozenBitmap_UsesSameLastWinsDefectValueAsSampler|FullyQualifiedName~ZeroCopyBitmapFactoryTests.GenerateFrozenBitmap_RendersDefectPayloadUnalteredOutsideLogicalBounds"`
- Task tracker validation passed with 225 task files validated and 5 legacy files skipped.
- `git diff --check` passes.

## Residual Risk

The renderer assertion covers overlap precedence and emitted Gray8 output. It does not validate the complete UI pixelometer text surface. The benchmark archive still represents one host and does not establish a performance comparison.

## Next Step

Commit and push Wave AC. Keep the one-host benchmark limitation and the open cross-machine comparison as explicit residual risk.
