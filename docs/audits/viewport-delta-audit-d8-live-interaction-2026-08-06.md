# InfiniteCanvasWPF Delta Audit D8: Live, Interaction, and Viewport-Host Readiness

**Description:** Additional delta audit after D7, focused on live inspection lifecycle, interaction contracts, source health, coordinate systems, dirty layers, hit testing, accessibility, and packaging readiness.  
**Timestamp:** 2026-08-06 12:28 CDT  
**Author:** Copilot  
**Repository / Subject:** InfiniteCanvasWPF / production viewport replacement candidate  
**Status:** Changes Requested  
**Overall Confidence:** 79%  
**Scope:** Delta-only. This report extends the prior D4-D7 reports and avoids restating their findings except where required for sequencing.  
**Secret Posture:** Neutral implementation names only; no credentials, private customer data, internal URLs, or proprietary adapter names.

## Executive Summary

This D8 pass shifts the audit lens from rendering/caching toward viewport-host readiness. The recurring issue is that ICW can be a strong drawing foundation, but production viewport-style replacement needs explicit live state, source health, input commands, coordinate-system transforms, dirty-layer semantics, hit testing, rulers/scrollbars/readout synchronization, and packaging gates. The most important next design move is to make the WPF layer a thin host adapter over a viewport engine that owns immutable snapshots and versioned publication.

## Evidence Corpus

| ID | Source | Directly used evidence |
|---|---|---|
| S1 | <File>external requirement source</File> | Acceptance criteria requiring surface ownership, bounded scheduling, exact reservation cleanup, stale-generation rejection, shared immutable snapshots for images/overlays/rulers/scrollbars/hit testing, explicit axis units, lifecycle state machines, source errors, and classic .sln. |
| S2 | <File>external requirement source</File> | Prior recommendation to establish immutable scene generations, frame snapshots, generation-aware cancellation, exact cache identity, leased surfaces, transactional replacement, deterministic shutdown, layer ordering, ROI transforms, alignment layer source acquisition, frame/region interaction, selection semantics, and live latch behavior. |
| S3 | <File>external requirement source</File> | Required remediation sequence: versioned publication, spatial identity, immutable viewport snapshots, settings ownership, ROI/DPI/units/selection/display/live state. |
| S4 | <File>external requirement source</File> | Findings that mutable production viewport host view/settings models are not safe render snapshots and surface release needs explicit ownership rather than WPF render callbacks. |
| S5 | <File>external requirement source</File> / <File>external requirement source</File> | WPF layer should be host adapter, engine owns snapshots/render scheduling, dense overlays need retained/batched rendering and hit-test service. |
| S6 | <File>external requirement source</File> / <File>external requirement source</File> | production viewport source failure/reconnect and live path concerns, including source connection loss/service-loss style issues in prior internal summaries. |
| S7 | <File>external requirement source</File> | production viewport context references streaming video, scrolling/no delay, global/local query, alignment layer limitations, and missing tools/live-mode gaps. |

## Findings Index

| ID | Priority | Area | Finding | Confidence |
|---|---:|---|---|---:|
| D8-001 | P1 | Live/update lifecycle | No explicit live/paused/catch-up viewport lifecycle contract is visible in the ICW boundary | 83% |
| D8-002 | P1 | Versioned publication | Live and render publication need source/view/camera/frame generation identity, not only render request revision | 83% |
| D8-003 | P1 | Source fault/reconnect | No source-health/reconnect contract is visible for adapter failure or source connection loss-like loss | 83% |
| D8-004 | P1 | Input architecture | Interaction handling lacks a neutral command/input contract suitable for host application replacement | 83% |
| D8-005 | P1 | Coordinate spaces | ROI/subview/source-raster/screen transforms need explicit named stages | 83% |
| D8-006 | P2 | Rulers/scrollbars/readout | Rulers, scrollbars, and readout do not appear as first-class frame-synchronized layers | 78% |
| D8-007 | P2 | Dirty-layer model | No dirty-layer plan is visible for partial updates and host application-like layer ordering | 78% |
| D8-008 | P2 | Hit testing | Per-entity WPF handlers remain incompatible with dense retained overlays | 78% |
| D8-009 | P2 | Accessibility/focus | Keyboard/focus/accessibility behavior is not captured as part of viewport readiness | 78% |
| D8-010 | P2 | Classic solution/project packaging | Replacement readiness reports still flag the classic .sln preference versus current .slnx state | 78% |

## Detailed Delta Findings

### D8-001: No explicit live/paused/catch-up viewport lifecycle contract is visible in the ICW boundary

**Priority:** P1  
**Area:** Live/update lifecycle  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior audits state host application-style parity includes completed and live inspections with defined lifecycle state machines, and production viewport materials reference streaming video/no delay and live mode gaps. Current ICW snippets mostly show synthetic/regenerate/render flow rather than a live source lifecycle.

**Risk:** A production viewport needs to distinguish static, completed, live, paused, catch-up, reconnecting, faulted, and closed states. Without that published state model, render scheduling and UI controls can disagree about what the viewport is showing.

**Recommendation:** Add a neutral ViewportLifecycleState and SourceConnectionState to the frame/source snapshot and diagnostics.

**Targeted Tests:**
- `ViewportLifecycleState_RoundTripsThroughFrame`
- `LivePauseState_BlocksOrAllowsUpdatesAsExpected`
- `FaultedSource_SurfacesRecoverableStatus`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-002: Live and render publication need source/view/camera/frame generation identity, not only render request revision

**Priority:** P1  
**Area:** Versioned publication  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior remediation guidance calls for scene, source, camera, and frame generations and says stale work cannot publish across viewport, inspection, view, source, or calibration generations. Current ICW work has CanvasFrame revision wiring but prior deltas found frame snapshot gaps.

**Risk:** A render request revision alone is not enough for live source replacement, selected-view changes, or calibration changes.

**Recommendation:** Introduce ViewportRevisionVector with source, view, camera, display, selection, and calibration revisions. Reject frames that mismatch any active dimension.

**Targeted Tests:**
- `FrameRejectsStaleSourceRevision`
- `FrameRejectsStaleViewRevision`
- `FrameRejectsStaleCalibrationRevision`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-003: No source-health/reconnect contract is visible for adapter failure or source connection loss-like loss

**Priority:** P1  
**Area:** Source fault/reconnect  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** production viewport backlog summaries reference source connection loss server loss, reconnect behavior, and degraded/crash paths. Existing ICW diagnostics focus on frame/cache/coordinator counters.

**Risk:** The candidate viewport must not assume the source is always available. Source loss should be reported as status, not as an unhandled render failure.

**Recommendation:** Add IViewportSourceHealth, SourceFaultInfo, and a recoverable-source-state path that surfaces unavailable/reconnecting/faulted states without crashing or stale publication.

**Targeted Tests:**
- `SourceLoss_DoesNotCrashViewport`
- `SourceReconnect_AdvancesSourceRevision`
- `FaultedSource_DiagnosticsAreSecretSafe`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-004: Interaction handling lacks a neutral command/input contract suitable for host application replacement

**Priority:** P1  
**Area:** Input architecture  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** WPF control requirements from prior reports describe WPF as a host adapter: input/layout/focus/DPI/presentation in the control, render scheduling and snapshots in the engine. Current ICW source evidence still centers on MainWindow event handlers and host-composed overlays.

**Risk:** host application replacement needs coherent pan, wheel zoom, rectangle zoom, fit, selection, hover, context command, keyboard, and possibly multi-tool modes without baking behavior into MainWindow.

**Recommendation:** Introduce IViewportInputAdapter, ViewportCommand, ViewportInputContext, and command routing from WPF control to engine/selection services.

**Targeted Tests:**
- `PanZoomCommands_UseInputAdapter`
- `Keyboarsource connection lossmands_RouteThroughViewportCommand`
- `ContextMenuCommand_UsesHitTestResult`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-005: ROI/subview/source-raster/screen transforms need explicit named stages

**Priority:** P1  
**Area:** Coordinate spaces  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior production viewport host migration reports state ROI offset is a first-class transform component and list inspection, primary-view, subview/ROI, source raster, and screen coordinate spaces.

**Risk:** Generic world X/Y coordinates will not be enough for production viewport parity where ROI, view, source raster, and axis units interact.

**Recommendation:** Add ViewportTransformSet with named transforms and include it in frame snapshots.

**Targeted Tests:**
- `TransformSet_IncludesRoiOffset`
- `ScreenToSourceRoundTrip_WithRoiOffset`
- `NonSquareScale_CWDWUnitsRemainConsistent`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-006: Rulers, scrollbars, and readout do not appear as first-class frame-synchronized layers

**Priority:** P2  
**Area:** Rulers/scrollbars/readout  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior acceptance criteria state images, overlays, rulers, scrollbars, and hit testing should share one immutable frame snapshot. Current ICW deltas have focused on raster and annotation overlays.

**Risk:** If rulers and scrollbars are updated independently from the frame snapshot, zoom/readout/UI geometry can drift under resize/live updates.

**Recommendation:** Represent ruler/scrollbar/readout state as derived frame artifacts using the same camera/units/snapshot.

**Targeted Tests:**
- `RulerUsesSameFrameCamera`
- `ScrollbarRangeMatchesFrameVisibleArea`
- `ReadoutUsesSameTransformSet`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-007: No dirty-layer plan is visible for partial updates and host application-like layer ordering

**Priority:** P2  
**Area:** Dirty-layer model  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior reports state production viewport host layer parity needs exact ordering and dirty-layer behavior; ICW current path rebuilds overlays and raster frame in broad operations.

**Risk:** Production viewport should avoid full redraws when only selection, labels, or tooltips change, and should maintain deterministic layer ordering.

**Recommendation:** Add LayerRenderPlan with dirty reasons and per-layer revision.

**Targeted Tests:**
- `SelectionChange_DirtiesSelectionLayerOnly`
- `LabelToggle_DirtiesLabelLayerOnly`
- `LayerOrder_GoldenTest`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-008: Per-entity WPF handlers remain incompatible with dense retained overlays

**Priority:** P2  
**Area:** Hit testing  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior WPF requirements say dense overlays need retained/batched rendering, spatial hit-test service, on-demand tooltip generation, and no per-entity WPF event handlers in the hot path.

**Risk:** A production overlay model should not rely on thousands of WPF Borders or per-entity mouse handlers.

**Recommendation:** Move hit testing to an IViewportHitTestService using spatial identity, layer, z-order, and frame revision.

**Targeted Tests:**
- `HitTest_ReturnsLayerAndItemRevision`
- `DenseOverlay_NoPerEntityHandlers`
- `Tooltip_GeneratedOnDemand`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-009: Keyboard/focus/accessibility behavior is not captured as part of viewport readiness

**Priority:** P2  
**Area:** Accessibility/focus  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** The reviewed source/search corpus did not surface explicit accessibility/focus contracts for the ICW replacement; prior WPF-requirement text assigns focus and input to the WPF control adapter.

**Risk:** Even if first integration is engineering/internal only, losing keyboard navigation and focus semantics can block parity later.

**Recommendation:** Add a small accessibility/focus acceptance gate: focusable control, keyboard command map, selection/readout announcements where applicable.

**Targeted Tests:**
- `ViewportControl_IsFocusable`
- `KeyboardPanZoomCommands_Work`
- `SelectedItem_AccessibleNameAvailable`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

### D8-010: Replacement readiness reports still flag the classic .sln preference versus current .slnx state

**Priority:** P2  
**Area:** Classic solution/project packaging  
**Classification:** Delta requirement / architecture blocker  

**Evidence:** Prior acceptance criteria explicitly say the final solution should use a classic .sln per standing Visual Studio preference and that the reviewed solution was .slnx.

**Risk:** This is not a runtime bug, but it matters for adoption, build tooling, and developer workflow.

**Recommendation:** Keep .slnx only if explicitly accepted; otherwise provide a classic .sln and validate build/test commands against it.

**Targeted Tests:**
- `ClassicSln_BuildsRelease`
- `ClassicSln_RunsCoreAndWindowsTests`
- `Slnx_NotRequiredForContributorWorkflow`

**Acceptance Criteria:**
- Contract is represented as immutable snapshot or source/frame state, not mutable host fields.
- Test coverage proves stale, disconnected, or out-of-order state cannot be displayed as current.
- Diagnostics identify the relevant source/view/frame revision and failure state where applicable.

## Proposed Thin-Host Architecture Addendum

```csharp
public interface IViewportEngine
{
    ValueTask<ViewportFrame> RenderAsync(ViewportFrameRequest request, CancellationToken cancellationToken);
}

public interface IViewportInputAdapter
{
    bool TryMapInput(ViewportInputContext context, out ViewportCommand command);
}

public sealed record ViewportFrameRequest(
    ViewportRevisionVector Revisions,
    ViewportLifecycleState LifecycleState,
    SourceConnectionState SourceState,
    ViewportTransformSet TransformSet,
    LayerRenderPlan LayerPlan);

public enum ViewportLifecycleState
{
    Static,
    Live,
    Paused,
    CatchingUp,
    Reconnecting,
    Faulted,
    Closed
}
```

The exact names can change; the required property is that live state, source health, transforms, layer plan, and input commands are first-class contracts rather than MainWindow-local behavior.

## Recommended Ticket Set

| Ticket | Priority | Summary | Findings |
|---|---:|---|---|
| ICW-D8-LIFECYCLE-SOURCE-STATE | P1 | Add live/static/paused/catch-up/reconnect/fault lifecycle and source-health states to frame/source snapshots. | D8-001, D8-003 |
| ICW-D8-REVISION-VECTOR | P1 | Extend frame identity to source/view/camera/display/selection/calibration revisions. | D8-002 |
| ICW-D8-INPUT-CONTRACTS | P1 | Add input adapter, command model, and context routing for pan/zoom/selection/context/key commands. | D8-004, D8-009 |
| ICW-D8-TRANSFORM-SET | P1 | Add named coordinate transforms including ROI/subview/source-raster/screen stages. | D8-005, D8-006 |
| ICW-D8-LAYER-DIRTY-HITTEST | P2 | Add dirty-layer plan, retained hit testing, tooltip/hover/selection layers. | D8-007, D8-008 |
| ICW-D8-CLASSIC-SLN | P2 | Provide and validate classic .sln workflow if .slnx is not accepted. | D8-010 |

## Test Plan

- `ViewportLifecycleState_RoundTripsThroughFrame`
- `FrameRejectsStaleSourceRevision`
- `SourceLoss_DoesNotCrashViewport`
- `PanZoomCommands_UseInputAdapter`
- `TransformSet_IncludesRoiOffset`
- `RulerUsesSameFrameCamera`
- `SelectionChange_DirtiesSelectionLayerOnly`
- `DenseOverlay_NoPerEntityHandlers`
- `ViewportControl_IsFocusable`
- `ClassicSln_BuildsRelease`

## Requests / Missing Evidence

- Current full ICW source for CanvasControl input handling, focus behavior, and command routing.
- Current full source or accepted requirements for live/pause/catch-up behavior in the target production viewport.
- Authoritative layer parity list and required dirty-layer behavior.
- Current build/package decision for .sln versus .slnx.
- Representative source-fault/reconnect scenario that the replacement must surface safely.

## Final Recommendation

**Decision: Changes Requested.** Before adding more feature-specific viewport behavior, define the host/engine boundary: lifecycle/source state, revision vector, transform set, input command adapter, dirty-layer plan, and source-health diagnostics. This creates the frame-safe foundation needed for live inspection, alignment layer-style sources, rulers, overlays, hit testing, and production viewport interaction parity.


