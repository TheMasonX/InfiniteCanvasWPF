# InfiniteCanvasWPF delta audit — latest commit follow-up

Commit reviewed: `d74dde2655b9cee1f6502e0200fca022ce1435dd` (`Restore viewport scrollbars and optimize direct mip generation`). The repo’s live task corpus is ICW/REQ-based; a repository search for `TSK-` returned only the task schema/skill docs, not task files, so de-duplication was performed against the ICW/REQ tracker only. citeturn76file0turn83file0turn81file0turn81file1

## Delta summary

The new commit closes the visible scrollbar regression and adds the mip/background refactor surface, but a few high-signal contract gaps remain:

- mip fallback is still conditionally bypassed, so the “keep the best resident image visible during mip transitions” contract is not fully enforced yet; and
- the new pixelometer readout now reports mip metadata, but the sampled value path still uses the native full-resolution pixel read, not the resident mip that is actually being rendered. citeturn112file0turn116file0turn108file0turn98file0

## New findings and corrections

### 1) Mip-transition fallback is still gated by sparse-generation heuristics
Severity: High  
Confidence: 92%

`DrawTile` only attempts resident-payload lookup when `shouldGeneratePixels` is true. That flag is currently `tile.IsMipGenerated(mipLevel) || tile.ShouldGenerateForPixelSize(camera, minimumSparseTilePixelSize)`. When both are false, the code paints placeholders even if an older resident mip is available. That is in tension with the ICW-096 acceptance criteria, which require a resident older mip or full-resolution payload to remain visible until the requested replacement completes. citeturn112file0turn116file0

Recommendation: decouple “should we generate now?” from “should we display a resident fallback?” The render path should always choose the best resident payload first, and only then decide whether to queue new generation.

Task correction: ICW-096 should explicitly call out resident-fallback selection as unconditional for rendering, not only for generation-eligible tiles. citeturn116file0

### 2) Pixelometer value sampling is still out of sync with the rendered mip path
Severity: Medium  
Confidence: 88%

The updated pixelometer readout now computes a mip level and shows `BackgroundTileReadoutInfo`, but the actual sample path still calls `tile.TryGetPixelValue(...)`, which reads the native tile payload only. That means the pixelometer can report a value from the full-resolution source even while the renderer is showing a lower-res resident mip or a placeholder fallback. citeturn108file0turn97file0turn98file0

Recommendation: either sample from the same resident mip selected for rendering or make the pixelometer explicitly label itself as “native source sample” and stop implying it matches the on-screen mip.

Task correction: this is a stronger version of the existing renderer/pixelometer contract work and should be merged into ICW-035 or a successor task rather than tracked as a separate one-off tweak. citeturn109file0turn108file0

### 3) The Windows defect-overlay bitmap read path does dead work
Severity: Medium  
Confidence: 95%

`DrawDefectPatch` locks `annotation.DefectBitmap`, computes `value = sourceRow[sourceX * 3]`, and then ignores that value completely. The final pixels come from `DefectOverlaySampler.ResolveDisplayValue(...)`, not from the bitmap read. That is a pure hot-path cost with no visible effect. citeturn112file0turn109file0

Recommendation: either remove the `LockBits`/source-row read entirely or change the render path so the bitmap data is actually used. Right now the code pays for a bitmap read and then discards it.

Task correction: this fits naturally into ICW-097 as a CPU-budget cleanup, with a smaller follow-up note in ICW-035 if the rendered-vs-sampled defect contract needs tightening. citeturn116file0turn112file0

### 4) Defect overlay blending is still order-dependent with no explicit overlap policy
Severity: Medium  
Confidence: 89%

`DefectOverlaySampler.ResolveDisplayValue(IEnumerable<SampleAnnotation>)` is a simple last-wins fold. If multiple annotations overlap, the result depends entirely on enumeration order, but no policy is documented in code or surfaced in the task entry. That is an implicit contract and a brittle one. citeturn109file0turn116file0

Recommendation: define the overlap rule explicitly. Good candidates are topmost-by-Z, first-hit, max-severity, or a deliberate “last annotation wins” policy with tests. Right now the choice is accidental rather than architectural.

Task correction: ICW-035 should include overlap semantics in its acceptance criteria if overlaps are possible in the scene model. citeturn109file0turn116file0

### 5) The direct mip generator still has avoidable per-pixel overhead
Severity: Low  
Confidence: 84%

`GenerateMonochromeMipPixels` now generates lower mips directly, but it still creates fresh deterministic RNG instances inside the inner sampling loop and repeatedly recomputes the previous mip dimensions for every child lookup. That is better than full-chain regeneration, but it is not yet a tight hot loop. citeturn99file0turn100file0

Recommendation: hoist the previous-level dimensions once per mip, and replace per-sample RNG construction with a stable deterministic helper or precomputed stream if benchmarks prove it is worth the memory tradeoff.

Task correction: ICW-097 should treat this as a first-order optimization target rather than assuming the current direct-mip implementation is already done. citeturn116file0turn99file0turn100file0

## Notes on task overlap

The latest commit already introduced or updated `ICW-096` and `ICW-097`, so I did not create duplicate task IDs. The new deltas above are corrections/extensions to those tasks, plus an ICW-035 refinement for pixelometer/blend semantics. citeturn116file0turn64file0turn65file0

## Open questions

The remaining implementation question is whether the resident-fallback policy should always prefer “best available visual data” even when sparse generation is disabled for a tile, or whether placeholder rendering is intentionally allowed outside the mip-transition path. The current code and the ICW-096 acceptance criteria point in different directions, so that policy needs to be made explicit. citeturn112file0turn116file0
