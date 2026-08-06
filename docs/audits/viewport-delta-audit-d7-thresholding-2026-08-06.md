# InfiniteCanvasWPF Delta Audit D7: Thresholding Support

**Description:** Additional delta audit focused on whether the viewport architecture can support the expected thresholding option families without overfitting to demo generation or annotation overlays.  
**Timestamp:** 2026-08-06 12:28 CDT  
**Author:** Copilot  
**Repository / Subject:** InfiniteCanvasWPF / production viewport replacement candidate  
**Status:** Changes Requested  
**Overall Confidence:** 81%  
**Scope:** Delta-only. This report intentionally stays concise on threshold mechanics and focuses on architecture/support readiness.  
**Secret Posture:** Neutral terms only; no credentials, private customer data, internal URLs, or proprietary adapter names.

## Executive Summary

This D7 pass adds a thresholding-readiness slice to the previous ICW audits. The current viewport foundation can probably display threshold-related outputs eventually, but the architecture should first add a neutral threshold options snapshot, threshold layer contract, effective-value diagnostics, and tests that prove option plumbing works end to end. The most important practical fix remains simple: centralize threshold/display options into immutable frame/source snapshots so fixed, adaptive, projection, histogram-like, region/binary, NR/streak-style, and sparse-display threshold options can all travel through the same pipeline.

## Evidence Corpus

| ID | Source | Directly used evidence |
|---|---|---|
| S1 | <File>external requirement source</File> | Current ICW MainWindow / renderer snippets include render options, SampleAnnotation overlay behavior, and generation settings. |
| S2 | <File>external requirement source</File> / <File>external requirement source</File> | Source-backed notes that MinimumSparseTilePixelSize exists/passes through partially but is not fully applied in rendering. |
| S3 | <File>external requirement source</File> / <File>external requirement source</File> | Adaptive thresholding references Fixed/Adaptive setup, sensitivity, effective value variation, and threshold adjustment concepts. |
| S4 | <File>external requirement source</File> / <File>external requirement source</File> | Region thresholding references min/max gray-level interval and binary region output. |
| S5 | <File>external requirement source</File> / <File>external requirement source</File> | Projection/NR/streak references indicate threshold options can be orientation-specific and mode-specific. |
| S6 | <File>external requirement source</File> | Histogram thresholding review references lower/upper histogram levels and separate validation expectations. |

## Findings Index

| ID | Priority | Area | Finding | Confidence |
|---|---:|---|---|---:|
| D7-001 | P1 | Thresholding architecture | No neutral threshold-options model is visible in the ICW viewport boundary | 82% |
| D7-002 | P1 | Settings plumbing | MinimumSparseTilePixelSize remains the clearest current threshold-like option plumbing gap | 88% |
| D7-003 | P1 | Frame consistency | Thresholding decisions need frame/source/revision identity before they are added | 84% |
| D7-004 | P1 | Layer model | Region/binary threshold results need a real layer contract, not SampleAnnotation-only overlays | 80% |
| D7-005 | P2 | Threshold modes | Fixed/adaptive threshold support should be represented as mode-specific data, but rendered through one common contract | 82% |
| D7-006 | P2 | Orientation / units | Projection/streak threshold options require explicit axis units orientation metadata | 78% |
| D7-007 | P2 | Diagnostics / support | Threshold diagnostics need to record both selected mode and effective values | 80% |
| D7-008 | P2 | Pixelometer / readout | Pixelometer should report threshold contribution through the same source-neutral readout path | 77% |
| D7-009 | P2 | Mip / sampling policy | Threshold display and threshold calculation need an explicit native-vs-mip policy | 76% |
| D7-010 | P3 | Test coverage | Add threshold contract tests before implementing visual polish | 86% |

## Detailed Delta Findings

### D7-001: No neutral threshold-options model is visible in the ICW viewport boundary

**Priority:** P1  
**Area:** Thresholding architecture  
**Confidence:** 82%  

**Evidence:** Current ICW snippets show synthetic generation fields such as TargetValue, Noise, NoiseAmplitude, AnnotationDisplayOptions, and MinimumSparseTilePixelSize, but no neutral threshold option contract in the canvas/source/render frame boundary. Internal thresholding references cover fixed/adaptive, region min/max, projection, NR/streak, and histogram-style thresholding as distinct concepts.

**Risk:** production viewport-style integration will need threshold settings to be carried as source/layer/frame state, not as demo generation knobs or overlay-only display settings.

**Recommendation:** Add a compact ThresholdOptionsSnapshot and ThresholdLayerDescriptor to the frame/source model. Keep it mode-neutral and serializable.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-002: MinimumSparseTilePixelSize remains the clearest current threshold-like option plumbing gap

**Priority:** P1  
**Area:** Settings plumbing  
**Confidence:** 88%  

**Evidence:** Prior source-backed audit snippets state GenerateFrozenBitmap accepts minimumSparseTilePixelSize and passes it to DrawTile, but DrawTile did not use it to gate tile generation; related snippets also say the user setting exists/validates but MainWindow did not pass a non-default value into rendering.

**Risk:** This is a small option, but it is an important canary: if one threshold-like feature can be validated/stored without affecting rendering, richer threshold options are likely to drift too.

**Recommendation:** Route option values through one RenderOptionsSnapshot used by MainWindow, renderer, pixelometer, diagnostics, and tests.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-003: Thresholding decisions need frame/source/revision identity before they are added

**Priority:** P1  
**Area:** Frame consistency  
**Confidence:** 84%  

**Evidence:** Previous ICW reports and concat snippets show stale work/frames are a recurring concern and that the system is moving toward CanvasFrame revision wiring. Internal training material also notes adaptive threshold values can vary during inspection.

**Risk:** Threshold overlays/readouts become hard to trust if the frame does not say which threshold settings and source revision produced them.

**Recommendation:** Bind threshold options to the same immutable frame/source revision vector used for raster, overlays, pixelometer, and diagnostics.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-004: Region/binary threshold results need a real layer contract, not SampleAnnotation-only overlays

**Priority:** P1  
**Area:** Layer model  
**Confidence:** 80%  

**Evidence:** Region thresholding references describe binary membership output for a layer; current ICW overlay snippets show UpdateAnnotationLayer accepts ICanvasItem but only renders SampleAnnotation items.

**Risk:** Threshold result display could be forced into the annotation overlay path and then inherit SampleAnnotation-specific assumptions.

**Recommendation:** Add a ThresholdMaskLayer or BinaryRegionLayer contract separate from annotation boxes/labels.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-005: Fixed/adaptive threshold support should be represented as mode-specific data, but rendered through one common contract

**Priority:** P2  
**Area:** Threshold modes  
**Confidence:** 82%  

**Evidence:** Adaptive-thresholding materials state thresholds can be Fixed or Adaptive and discuss initial values, limits, sensitivity, and adaptation behavior. Current ICW source evidence does not show a threshold mode model.

**Risk:** Mode-specific settings will sprawl unless the model separates common threshold identity from fixed/adaptive parameters.

**Recommendation:** Use a small discriminated model: Fixed, Adaptive, Projection, Histogram, and Derived/NR-style modes as data records behind one threshold layer interface.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-006: Projection/streak threshold options require explicit axis units orientation metadata

**Priority:** P2  
**Area:** Orientation / units  
**Confidence:** 78%  

**Evidence:** Projection thresholding references mention narrow horizontal crossweb defects; prior acceptance criteria already require axis units and orientation to be explicit and tested.

**Risk:** A generic canvas can display pixels, but threshold options tied to web direction need orientation-aware metadata or the UI will be ambiguous.

**Recommendation:** Include orientation/unit metadata in threshold layer descriptors and test non-square scale/orientation cases.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-007: Threshold diagnostics need to record both selected mode and effective values

**Priority:** P2  
**Area:** Diagnostics / support  
**Confidence:** 80%  

**Evidence:** Adaptive thresholding references say the effective threshold value may vary during inspection and can affect defect features. Existing ICW diagnostics snippets emphasize cache/frame counters but not threshold state.

**Risk:** Support/debug workflows will not be able to explain why thresholded pixels/objects changed if the frame does not include effective threshold mode/value metadata.

**Recommendation:** Add secret-safe threshold diagnostics to the frame/support snapshot: mode, source/layer ID, configured values, effective values where available, and revision.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-008: Pixelometer should report threshold contribution through the same source-neutral readout path

**Priority:** P2  
**Area:** Pixelometer / readout  
**Confidence:** 77%  

**Evidence:** Earlier source review found pixelometer composition still depends on SampleAnnotation, while thresholding options will need to report pixel-level membership/contribution from threshold layers.

**Risk:** Adding threshold readout on top of the current SampleAnnotation path will create another special case and likely disagree with rendered output.

**Recommendation:** Extend the source-neutral composite pixel sample with optional threshold contributions.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-009: Threshold display and threshold calculation need an explicit native-vs-mip policy

**Priority:** P2  
**Area:** Mip / sampling policy  
**Confidence:** 76%  

**Evidence:** Existing reports identify mip selection and sparse tile generation as active concerns; thresholding generally depends on source pixel values, while the viewport can render resident mips.

**Risk:** Threshold pixel visuals can diverge from detection semantics if the display implicitly uses mips without recording whether thresholding is based on native, resident, or derived data.

**Recommendation:** Declare threshold evaluation data level explicitly in ThresholdOptionsSnapshot and readout diagnostics.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

### D7-010: Add threshold contract tests before implementing visual polish

**Priority:** P3  
**Area:** Test coverage  
**Confidence:** 86%  

**Evidence:** Existing audit reports repeatedly gate readiness on explicit tests for frame consistency, orientation, cache, and settings plumbing; threshold-related source snippets show settings drift is already possible.

**Risk:** Threshold options are configuration-heavy, so regressions will be easy to introduce if tests are added after UI work.

**Recommendation:** Add mode-plumbing tests first: fixed, adaptive, projection, histogram-like, region/binary mask, min/max limits, orientation, and diagnostics snapshot.

**Acceptance Criteria:**
- Option state is carried by a snapshot, not live mutable UI fields.
- The rendered output, readout, and diagnostics identify the threshold option set used.
- The behavior is covered by at least one automated test or explicit manual-gate note.

## Minimal Thresholding Contract Recommendation

```csharp
public enum ThresholdMode
{
    Fixed,
    Adaptive,
    Projection,
    Histogram,
    RegionBinary,
    Derived
}

public sealed record ThresholdOptionsSnapshot(
    string SourceId,
    string LayerId,
    long Revision,
    ThresholdMode Mode,
    IReadOnlyDictionary<string, double> Parameters,
    string EvaluationLevel,
    string Orientation);

public sealed record ThresholdLayerDescriptor(
    string LayerId,
    ThresholdOptionsSnapshot Options,
    bool IsVisible);
```

Keep this small. The key architectural point is not the exact enum names; it is that all threshold options become frame/source/layer snapshot data instead of ad hoc UI settings or demo-renderer branches.

## Proposed Tickets

| Ticket | Priority | Summary | Findings |
|---|---:|---|---|
| ICW-D7-THRESHOLD-SNAPSHOT | P1 | Add ThresholdOptionsSnapshot and ThresholdLayerDescriptor to the source/frame model. | D7-001, D7-003, D7-005 |
| ICW-D7-OPTIONS-PLUMBING | P1 | Route threshold-like options through one RenderOptionsSnapshot and test MinimumSparseTilePixelSize end to end. | D7-002, D7-010 |
| ICW-D7-THRESHOLD-LAYERS | P1 | Add neutral threshold/binary mask layer support separate from SampleAnnotation overlays. | D7-004, D7-008 |
| ICW-D7-THRESHOLD-DIAGNOSTICS | P2 | Add secret-safe threshold mode/effective-value diagnostics. | D7-007, D7-009 |
| ICW-D7-ORIENTATION-METADATA | P2 | Add orientation/unit metadata for projection/streak-style threshold layers. | D7-006 |

## Test Plan

- `ThresholdOptionsSnapshot_Fixed_Mode_RoundTripsThroughFrame`
- `ThresholdOptionsSnapshot_Adaptive_Mode_RecordsEffectiveValuesWhenProvided`
- `MinimumSparseTilePixelSize_IsAppliedByRenderer`
- `ThresholdLayer_BinaryMask_RendersWithoutSampleAnnotation`
- `ThresholdPixelReadout_UsesSourceNeutralContribution`
- `ProjectionThresholdLayer_RequiresOrientationMetadata`
- `ThresholdDiagnostics_ContainsModeLayerRevisionAndEvaluationLevel`
- `ThresholdOptions_AreFrameRevisionStable`

## Requests / Missing Evidence

- Current full source for `CanvasUserSettings`, `MainViewModel`, `ZeroCopyBitmapFactory.Windows.cs`, and any current threshold-related branch or ticket files.
- Exact target threshold option list for first production viewport parity slice.
- Decision on whether the first implementation only displays threshold results, or also models threshold configuration/editing.
- Representative fixed/adaptive/projection/histogram-like examples for tests.

## Final Recommendation

**Decision: Changes Requested.** Add threshold support as data contracts and tests first, not as UI-specific or SampleAnnotation-specific renderer branches. The system should carry threshold options as immutable frame/source/layer snapshots, expose effective values in diagnostics, and keep pixelometer/readout contributions source-neutral.


