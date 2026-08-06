# InfiniteCanvasWPF Deep Bug Sweep Delta 3

**Description:** Third additional pass focused on matching production viewport/production viewport host requirements while avoiding any secret exposure. This report cites only file names, code symbols, task IDs, and requirement summaries already visible in retrieved source/tool outputs.

**Timestamp:** 2026-08-06 09:54 CDT  
**Author:** Copilot  
**Status:** Additional findings for triage; source-backed where directly cited, otherwise strongly inferred from current chunks and tracker/audit evidence.

## Executive Summary

This pass adds more findings around requirements-fit rather than repeating generic WPF issues. The central issue is that InfiniteCanvasWPF has become a stronger reusable WPF canvas, but production viewport requires a production inspection viewport: source/revision identity, layer parity, support diagnostics, scene snapshots, stable hit-test/selection semantics, and runtime evidence with representative inspection data.

## Findings Table

| ID | Priority | Area | Finding | Recommendation | Evidence |
|---|---:|---|---|---|---|
| ICW-DEEP3-115 | P0 | Requirement fit | No explicit host application source/revision model is present in the supplied current chunk evidence; <File>external requirement source</File> implements generic ICanvasSceneSource against sample tiles/annotations. | Add ViewportSnapshot with inspection ID, source IDs, view IDs, layer revisions, connection state, and frame revision before any host application integration. | external-source-reference |
| ICW-DEEP3-116 | P0 | Requirement fit | The supplied current evidence shows a host overlay model, not the production viewport LayerManager parity model. | Implement a first-class ViewportLayerStack and map each viewport layer; do not rely on generic item overlays to approximate it. | external-source-reference, external-source-reference |
| ICW-DEEP3-117 | P0 | Requirement fit | Runtime validation is still not equivalent to field readiness: the audit synthesis states no runtime reproduction was run for concurrency candidates and benchmark/profiler artifacts were not inspected in depth. | Gate replacement readiness on real WPF runtime stress, not only task/test counts and source reasoning. | external-source-reference |
| ICW-DEEP3-118 | P1 | Data-source contract | ICanvasSceneSource exposes QueryVisible, QueryPoint, TryReadResidentPixel, SceneBounds, TotalItemCount, and SceneChanged, but not layer identity, source availability, unit calibration, or source revision. | Split generic canvas source from host application inspection source; add an adapter-specific contract for production viewport host facts. | external-source-reference |
| ICW-DEEP3-119 | P1 | Data-source contract | QueryVisible returns IReadOnlyList<ICanvasItem>, which is too shallow for host application overlay payloads that need typed layer semantics and hit-test metadata. | Introduce ICanvasLayerItem or ICanvasHitTarget with stable IDs, layer ID, z-order, revision, tooltip payload, and selection behavior. | external-source-reference |
| ICW-DEEP3-120 | P1 | Data-source contract | QueryPoint still hides hit-test tolerance inside the source implementation with a fixed probe size, despite being a reusable contract boundary. | Move tolerance policy to CanvasControl/interaction settings or source-provided hit-test configuration. | external-source-reference |
| ICW-DEEP3-121 | P1 | Data-source contract | TryReadResidentPixel returns CanvasPixelSample with one tile ID and background/defect byte values, but not the contributing layer IDs or source revision. | Extend pixel sample to include source revision, mip actually sampled, layer contributions, and policy used for composite value. | external-source-reference |
| ICW-DEEP3-122 | P1 | Scene state | MainWindow holds _tiles, _tileBoundsById, _annotations, _sceneBounds, _spatialIndex, _selectedAnnotationId, and cache budget as separate mutable fields. | Aggregate scene-related state into one immutable SceneSnapshot and swap by reference. | external-source-reference |
| ICW-DEEP3-123 | P1 | Scene state | Visible tile tracking is stored as _lastPublishedVisibleTiles and _lastPublishedCamera, which are host-global mutable values separate from CanvasFrame. | Make visible tile set and camera part of an accepted frame snapshot or render result. | external-source-reference |
| ICW-DEEP3-124 | P1 | Scene state | _lastGridCamera and _lastGridTileIds cache grid rendering separately from frame revision/source revision. | Tie grid-layer cache to frame revision and layer revision, not ad hoc camera and tile ID arrays. | external-source-reference |
| ICW-DEEP3-125 | P1 | Frame identity | RenderRequestTracker exists, but the source excerpt only demonstrates frame request sequencing; host application needs semantic invalidation when source/layer/view revisions change. | Use a composite FrameRevision: request sequence + source revision vector + layer revision vector + view selection revision. | external-source-reference |
| ICW-DEEP3-126 | P1 | Frame identity | CanvasFrame revision discard can prevent out-of-order presentation, but it cannot prove the frame represents the latest host application inspection data without source revisions. | Add source revisions and stale-source rejection tests. | external-source-reference, external-source-reference |
| ICW-DEEP3-127 | P1 | Shutdown lifecycle | Constructor subscribes CanvasSurface events, Loaded, Closed, and CompositionTarget.Rendering; OnClosed shown unsubscribes CompositionTarget.Rendering but not all CanvasSurface events. | Unsubscribe all event handlers or make control lifetime own them; add leak test. | external-source-reference |
| ICW-DEEP3-128 | P1 | Shutdown lifecycle | The shown OnClosed path calls CanvasSurface.DetachFrameShell before disposing frame buffer, but does not explicitly clear SceneSource or FramePublished handler. | Detach all source/event references to avoid retaining MainWindow after close. | external-source-reference |
| ICW-DEEP3-129 | P1 | Shutdown lifecycle | OnClosed awaits _renderAction.DisposeAsync inside an async void event handler and then continues disposing objects; there is no visible top-level try/catch around shutdown. | Wrap shutdown in guarded async Task pattern and log/suppress expected cancellation exceptions. | external-source-reference |
| ICW-DEEP3-130 | P1 | Busy state | BeginBusyOperation is called in RequestRenderAsync and RegenerateSceneAsync; if render/regeneration reenter, busy count semantics must be exact across async exceptions. | Add tests for nested render/regenerate/close paths and ensure busy state never goes negative or stuck visible. | external-source-reference |
| ICW-DEEP3-131 | P1 | Render scheduling | OnTilePixelsGenerated enqueues RequestRenderAsync on Dispatcher for every generated tile event. | Coalesce tile-generated-triggered renders through a single dirty flag or scheduler to avoid dispatcher flooding. | external-source-reference |
| ICW-DEEP3-132 | P1 | Render scheduling | OnTilePixelsGenerationFailed also enqueues render to retry, making failures and successes use the same scheduler path without backoff. | Differentiate success dirtying from failed-tile retry strategy with throttling/backoff. | external-source-reference |
| ICW-DEEP3-133 | P1 | Render scheduling | RequestRenderAsync likely begins a busy operation and delegates to CoalescingAsyncAction, but tile events, viewport changes, style changes, and regeneration all share one render path. | Classify render reasons and make scheduler priority-aware: user interaction > visible tile completion > prefetch/cache > diagnostics. | external-source-reference |
| ICW-DEEP3-134 | P1 | Render scheduling | Resize uses a DispatcherTimer with 150 ms interval, which may add latency or stale frames during continuous resize. | Make resize policy explicit and test continuous resize/high-DPI behavior. | external-source-reference |
| ICW-DEEP3-135 | P1 | Render scheduling | Frame tile CTS replacement uses _frameTileCts and _previousFrameTileCts, making cancellation span two frames by convention. | Encapsulate frame claimant lifetime in a FrameClaimantLease object and test exact cancellation/disposal order. | external-source-reference |
| ICW-DEEP3-136 | P1 | Cache/source identity | TileCacheBudget is owned by MainWindow and reset on regeneration; there is no current evidence of shared host application-level cache policy. | Introduce IViewportCacheBudgetService scoped per host application process/workspace. | external-source-reference |
| ICW-DEEP3-137 | P1 | Cache/source identity | Tile bounds lookup is keyed by string tile ID, not source-qualified key. | Use BackgroundTileCacheKey or a source-qualified tile identity for all bounds/cache maps. | external-source-reference |
| ICW-DEEP3-138 | P1 | Cache/source identity | Selected/visible/pinned tile behavior is based on tile IDs present in the demo scene, not inspection view/source IDs. | Make pinning and cache retention source-qualified before multi-source/multi-view host application trials. | external-source-reference |
| ICW-DEEP3-139 | P1 | Cache/source identity | Cache reset in OnDebugDumpCacheClicked resets all tile caches and replaces TileCacheBudget, which is demo/admin behavior not clearly safe for production operators. | Move cache debug operations behind diagnostic service and separate production-safe cache invalidation controls. | external-source-reference |
| ICW-DEEP3-140 | P1 | Tile generation | Each tile gets Coordinator, ClaimantIdProvider, ClaimantTokenProvider, and ReleaseReservedCacheEntry assigned after generation, making lifecycle wiring mutable and order-dependent. | Pass immutable services into tile generation or externalize scheduling to avoid half-wired tiles. | external-source-reference |
| ICW-DEEP3-141 | P1 | Tile generation | ClaimantTokenProvider closes over _frameTileCts, so tile behavior depends on mutable current-frame global state rather than the request that initiated generation. | Pass frame token through request-specific generation call, not a mutable provider. | external-source-reference |
| ICW-DEEP3-142 | P1 | Tile generation | ClaimantIdProvider is explicitly set to null to use per-tile claimant identity, so current frame ownership may be weaker than intended for shared frame cancellation semantics. | Re-evaluate claimant identity semantics for host application multi-viewport use. | external-source-reference |
| ICW-DEEP3-143 | P2 | Pixelometer | UpdatePixelometer does not visibly check _lifetime cancellation before reading camera/source state. | Guard pixelometer updates during shutdown/regeneration. | external-source-reference |
| ICW-DEEP3-144 | P2 | Pixelometer | Pixelometer status uses formatted strings, not a structured readout model. | Use typed readout state with units, source, revision, tile ID, mip, and unavailable reason. | external-source-reference |
| ICW-DEEP3-145 | P2 | Pixelometer | The readout reports background + defect but not whether display value is max-wins, last-wins, overlay alpha, or layer priority. | Add explicit composite policy to readout. | external-source-reference |
| ICW-DEEP3-146 | P2 | Pixelometer | Pixelometer reads the current SceneSource property from CanvasSurface, so a host swap could change source mid-update. | Capture SceneSource into the frame snapshot/read request and hold stable for one read. | external-source-reference |
| ICW-DEEP3-147 | P2 | Selection | _selectedAnnotationId is stored as string and resolved by scanning _annotations.FirstOrDefault. | Use a dictionary keyed by stable item ID or selection service with source revision. | external-source-reference |
| ICW-DEEP3-148 | P2 | Selection | Selection is reset to null during regeneration before new content succeeds. | Carry selection through scene swap where same logical item survives, or reset only after successful scene commit. | external-source-reference |
| ICW-DEEP3-149 | P2 | Selection | FeatureDataGrid.ItemsSource is updated directly from MainWindow, so selection details remain app-shell-owned. | Move selected-item detail payload into the generic/host application adapter layer. | external-source-reference |
| ICW-DEEP3-150 | P2 | Selection | Selection visual updates likely depend on RequestRenderAsync after click; if render is coalesced/canceled during close, UI state and detail grid can diverge. | Make selection state update and visual invalidation transactional. | external-source-reference |
| ICW-DEEP3-151 | P2 | Zoom | ZoomPresetComboBox.Text is assigned formatted percent manually while SelectedIndex is also used for presets. | Separate transient display text from selectable preset state to avoid combo reentrancy/confusion. | external-source-reference |
| ICW-DEEP3-152 | P2 | Zoom | ComputeDisplayPercent collapses ScaleX/ScaleY into one percent. | For production viewport host horizontal/vertical scale-style semantics, expose independent X/Y percent or named fit mode. | external-source-reference |
| ICW-DEEP3-153 | P2 | Zoom | ApplyCustomZoomAsync parses percent with double.TryParse using ambient culture by default. | Use invariant/current-culture explicitly and add tests for decimal separators. | external-source-reference |
| ICW-DEEP3-154 | P2 | Zoom | OnCanvasPointerWheel only updates pixelometer, relying on CanvasControl for zoom; this split means pixelometer and render updates may be scheduled by different components. | Define a single input pipeline event that updates camera, render, and pixelometer from one transaction. | external-source-reference, external-source-reference |
| ICW-DEEP3-155 | P2 | Geometry | TryReadResidentPixel derives tileRows from _tiles.Count / _tileColumns, which can be wrong when last row is partial or grid shape changes. | Use explicit tile topology in scene metadata. | external-source-reference |
| ICW-DEEP3-156 | P2 | Geometry | GetSceneBounds uses Min/Max over tiles and assumes all tile bounds are finite and non-empty. | Validate tile bounds during scene construction and fail before publication. | external-source-reference |
| ICW-DEEP3-157 | P2 | Geometry | Screen-to-world conversion is duplicated by manual camera offset/scale math in pixelometer and likely other paths. | Centralize ScreenToWorld/WorldToScreen and add round-trip tests. | external-source-reference |
| ICW-DEEP3-158 | P2 | Geometry | Tile-center scheduling uses _tileBoundsById cache rebuilt once per scene; if tile geometry changes with source revision, stale map risk exists unless scene swap is atomic. | Keep tile bounds map inside SceneSnapshot. | external-source-reference |
| ICW-DEEP3-159 | P2 | Rendering | The host keeps overlay composition after ICW-315, so CanvasFrame does not appear to be a complete visual frame. | Clarify whether CanvasFrame is raster-only or full-frame; rename or extend accordingly. | external-source-reference, external-source-reference |
| ICW-DEEP3-160 | P2 | Rendering | FramePublished event is used so host can populate overlays per accepted frame, but acceptance/stale discard semantics need to be guaranteed. | Add tests proving overlays update only for accepted frames. | external-source-reference, external-source-reference |
| ICW-DEEP3-161 | P2 | Rendering | RasterVisible combines background and image-tile toggles, while overlay toggles are separate; this is not a general layer visibility model. | Introduce LayerVisibilitySnapshot per frame. | external-source-reference |
| ICW-DEEP3-162 | P2 | Rendering | Current overlay drawing is WPF-element based, which does not match the same frame-buffer lease discipline as raster. | Decide retained-WPF vs rasterized-overlay architecture per layer; preserve ordering and hit testing. | external-source-reference |
| ICW-DEEP3-163 | P2 | Rendering | Persistent frame shell fixed Viewbox teardown, but annotation overlay elements can still churn and fragment UI tree during dense scenes. | Pool/virtualize overlay elements or draw dense layers into DrawingVisual/bitmap. | external-source-reference, external-source-reference |
| ICW-DEEP3-164 | P2 | Rendering | Background image visibility and sparse image tile visibility are independent booleans but DrawTile/DrawDefectPatch paths combine them in GenerateFrozenBitmap. | Name and test exact layer behavior: background raster, sparse image tiles, defect patches, labels. | external-source-reference |
| ICW-DEEP3-165 | P2 | Diagnostics | Frame diagnostics are kept in several primitive fields: _diagnosticsFrameCount, _lastFrameTicks, _totalFrameTicks, _frameCount. | Move to one RenderDiagnosticsSnapshot with rolling windows and reset/serialize support. | external-source-reference |
| ICW-DEEP3-166 | P2 | Diagnostics | Cache status is written to UI and debug logs, but not exposed as structured telemetry. | Expose structured cache counters for support bundle collection. | external-source-reference |
| ICW-DEEP3-167 | P2 | Diagnostics | Tile/coordinator counters are not associated with render request ID or source revision in the visible status model. | Include render request ID/source revision in all diagnostics. | external-source-reference |
| ICW-DEEP3-168 | P2 | Diagnostics | Settings persistence failure is logged only; current UI may not show that user settings were not saved. | Surface non-blocking settings-save warning. | external-source-reference |
| ICW-DEEP3-169 | P2 | Testing | Task tracker reports many passing test counts, but current replacement readiness requires integration tests with representative host application inspection data, which is not evidenced here. | Add production viewport host fixture tests before replacement claims. | external-source-reference, external-source-reference |
| ICW-DEEP3-170 | P2 | Testing | The audit synthesis states benchmark result files, BenchmarkDotNet artifacts, profiler captures, and FastNoise2 internals were not inspected in depth. | Do a dedicated performance-evidence pass before making performance claims. | external-source-reference |
| ICW-DEEP3-171 | P2 | Testing | User-reproduced black flashes were verified fixed per task tracker, but that does not prove all source/layer/overlay flicker modes are covered. | Add automated visual regression around overlay/raster synchronization. | external-source-reference |
| ICW-DEEP3-172 | P2 | Testing | Consumer-host test proves another app can publish a frame, but not that host application source adapters/layers work. | Complement controls consumer test with host application-like adapter host test. | external-source-reference |
| ICW-DEEP3-173 | P3 | Process | Active tasks mention user-deferred ICW-313 and ICW-314, but those are still important to host application ergonomics. | Mark them explicitly as deferred but replacement-blocking or replacement-nonblocking based on first target surface. | external-source-reference |
| ICW-DEEP3-174 | P3 | Process | Audit synthesis reports duplicate/status divergence history; current tracker is useful but should remain secondary until source-verified. | Keep source verification as gate for master findings. | external-source-reference |
| ICW-DEEP3-175 | P3 | Process | No implementation was performed in the reconciliation, but some findings are now marked done in later active tasks; the master backlog needs status reconciliation by commit. | Create a commit-keyed status reconciliation matrix. | external-source-reference, external-source-reference |
| ICW-DEEP3-176 | P3 | Docs | ADR/task terms like CanvasFrame, source, frame shell, source contract, and layer are overloaded. | Add glossary distinguishing raster frame, visual frame, source frame, render request, scene snapshot, layer plan. | external-source-reference, external-source-reference |
| ICW-DEEP3-177 | P3 | Docs | The About dialog states MIT license and third-party credits in source excerpt; production host application integration may need a different attribution path. | Keep demo About dialog separate from production control library attribution/notice. | external-source-reference |
| ICW-DEEP3-178 | P3 | Maintainability | MainWindow contains app orchestration, source implementation, render scheduling, diagnostics, settings, and selection. | Continue decomposition under ICW-022-style service extraction. | external-source-reference, external-source-reference |

## Detailed Findings

### ICW-DEEP3-115: Requirement fit

- **Priority:** P0
- **Finding:** No explicit host application source/revision model is present in the supplied current chunk evidence; <File>external requirement source</File> implements generic ICanvasSceneSource against sample tiles/annotations.
- **Evidence:** external-source-reference
- **Recommendation:** Add ViewportSnapshot with inspection ID, source IDs, view IDs, layer revisions, connection state, and frame revision before any host application integration.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-116: Requirement fit

- **Priority:** P0
- **Finding:** The supplied current evidence shows a host overlay model, not the production viewport LayerManager parity model.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Implement a first-class ViewportLayerStack and map each viewport layer; do not rely on generic item overlays to approximate it.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-117: Requirement fit

- **Priority:** P0
- **Finding:** Runtime validation is still not equivalent to field readiness: the audit synthesis states no runtime reproduction was run for concurrency candidates and benchmark/profiler artifacts were not inspected in depth.
- **Evidence:** external-source-reference
- **Recommendation:** Gate replacement readiness on real WPF runtime stress, not only task/test counts and source reasoning.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-118: Data-source contract

- **Priority:** P1
- **Finding:** ICanvasSceneSource exposes QueryVisible, QueryPoint, TryReadResidentPixel, SceneBounds, TotalItemCount, and SceneChanged, but not layer identity, source availability, unit calibration, or source revision.
- **Evidence:** external-source-reference
- **Recommendation:** Split generic canvas source from host application inspection source; add an adapter-specific contract for production viewport host facts.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-119: Data-source contract

- **Priority:** P1
- **Finding:** QueryVisible returns IReadOnlyList<ICanvasItem>, which is too shallow for host application overlay payloads that need typed layer semantics and hit-test metadata.
- **Evidence:** external-source-reference
- **Recommendation:** Introduce ICanvasLayerItem or ICanvasHitTarget with stable IDs, layer ID, z-order, revision, tooltip payload, and selection behavior.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-120: Data-source contract

- **Priority:** P1
- **Finding:** QueryPoint still hides hit-test tolerance inside the source implementation with a fixed probe size, despite being a reusable contract boundary.
- **Evidence:** external-source-reference
- **Recommendation:** Move tolerance policy to CanvasControl/interaction settings or source-provided hit-test configuration.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-121: Data-source contract

- **Priority:** P1
- **Finding:** TryReadResidentPixel returns CanvasPixelSample with one tile ID and background/defect byte values, but not the contributing layer IDs or source revision.
- **Evidence:** external-source-reference
- **Recommendation:** Extend pixel sample to include source revision, mip actually sampled, layer contributions, and policy used for composite value.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-122: Scene state

- **Priority:** P1
- **Finding:** MainWindow holds _tiles, _tileBoundsById, _annotations, _sceneBounds, _spatialIndex, _selectedAnnotationId, and cache budget as separate mutable fields.
- **Evidence:** external-source-reference
- **Recommendation:** Aggregate scene-related state into one immutable SceneSnapshot and swap by reference.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-123: Scene state

- **Priority:** P1
- **Finding:** Visible tile tracking is stored as _lastPublishedVisibleTiles and _lastPublishedCamera, which are host-global mutable values separate from CanvasFrame.
- **Evidence:** external-source-reference
- **Recommendation:** Make visible tile set and camera part of an accepted frame snapshot or render result.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-124: Scene state

- **Priority:** P1
- **Finding:** _lastGridCamera and _lastGridTileIds cache grid rendering separately from frame revision/source revision.
- **Evidence:** external-source-reference
- **Recommendation:** Tie grid-layer cache to frame revision and layer revision, not ad hoc camera and tile ID arrays.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-125: Frame identity

- **Priority:** P1
- **Finding:** RenderRequestTracker exists, but the source excerpt only demonstrates frame request sequencing; host application needs semantic invalidation when source/layer/view revisions change.
- **Evidence:** external-source-reference
- **Recommendation:** Use a composite FrameRevision: request sequence + source revision vector + layer revision vector + view selection revision.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-126: Frame identity

- **Priority:** P1
- **Finding:** CanvasFrame revision discard can prevent out-of-order presentation, but it cannot prove the frame represents the latest host application inspection data without source revisions.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Add source revisions and stale-source rejection tests.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-127: Shutdown lifecycle

- **Priority:** P1
- **Finding:** Constructor subscribes CanvasSurface events, Loaded, Closed, and CompositionTarget.Rendering; OnClosed shown unsubscribes CompositionTarget.Rendering but not all CanvasSurface events.
- **Evidence:** external-source-reference
- **Recommendation:** Unsubscribe all event handlers or make control lifetime own them; add leak test.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-128: Shutdown lifecycle

- **Priority:** P1
- **Finding:** The shown OnClosed path calls CanvasSurface.DetachFrameShell before disposing frame buffer, but does not explicitly clear SceneSource or FramePublished handler.
- **Evidence:** external-source-reference
- **Recommendation:** Detach all source/event references to avoid retaining MainWindow after close.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-129: Shutdown lifecycle

- **Priority:** P1
- **Finding:** OnClosed awaits _renderAction.DisposeAsync inside an async void event handler and then continues disposing objects; there is no visible top-level try/catch around shutdown.
- **Evidence:** external-source-reference
- **Recommendation:** Wrap shutdown in guarded async Task pattern and log/suppress expected cancellation exceptions.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-130: Busy state

- **Priority:** P1
- **Finding:** BeginBusyOperation is called in RequestRenderAsync and RegenerateSceneAsync; if render/regeneration reenter, busy count semantics must be exact across async exceptions.
- **Evidence:** external-source-reference
- **Recommendation:** Add tests for nested render/regenerate/close paths and ensure busy state never goes negative or stuck visible.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-131: Render scheduling

- **Priority:** P1
- **Finding:** OnTilePixelsGenerated enqueues RequestRenderAsync on Dispatcher for every generated tile event.
- **Evidence:** external-source-reference
- **Recommendation:** Coalesce tile-generated-triggered renders through a single dirty flag or scheduler to avoid dispatcher flooding.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-132: Render scheduling

- **Priority:** P1
- **Finding:** OnTilePixelsGenerationFailed also enqueues render to retry, making failures and successes use the same scheduler path without backoff.
- **Evidence:** external-source-reference
- **Recommendation:** Differentiate success dirtying from failed-tile retry strategy with throttling/backoff.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-133: Render scheduling

- **Priority:** P1
- **Finding:** RequestRenderAsync likely begins a busy operation and delegates to CoalescingAsyncAction, but tile events, viewport changes, style changes, and regeneration all share one render path.
- **Evidence:** external-source-reference
- **Recommendation:** Classify render reasons and make scheduler priority-aware: user interaction > visible tile completion > prefetch/cache > diagnostics.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-134: Render scheduling

- **Priority:** P1
- **Finding:** Resize uses a DispatcherTimer with 150 ms interval, which may add latency or stale frames during continuous resize.
- **Evidence:** external-source-reference
- **Recommendation:** Make resize policy explicit and test continuous resize/high-DPI behavior.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-135: Render scheduling

- **Priority:** P1
- **Finding:** Frame tile CTS replacement uses _frameTileCts and _previousFrameTileCts, making cancellation span two frames by convention.
- **Evidence:** external-source-reference
- **Recommendation:** Encapsulate frame claimant lifetime in a FrameClaimantLease object and test exact cancellation/disposal order.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-136: Cache/source identity

- **Priority:** P1
- **Finding:** TileCacheBudget is owned by MainWindow and reset on regeneration; there is no current evidence of shared host application-level cache policy.
- **Evidence:** external-source-reference
- **Recommendation:** Introduce IViewportCacheBudgetService scoped per host application process/workspace.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-137: Cache/source identity

- **Priority:** P1
- **Finding:** Tile bounds lookup is keyed by string tile ID, not source-qualified key.
- **Evidence:** external-source-reference
- **Recommendation:** Use BackgroundTileCacheKey or a source-qualified tile identity for all bounds/cache maps.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-138: Cache/source identity

- **Priority:** P1
- **Finding:** Selected/visible/pinned tile behavior is based on tile IDs present in the demo scene, not inspection view/source IDs.
- **Evidence:** external-source-reference
- **Recommendation:** Make pinning and cache retention source-qualified before multi-source/multi-view host application trials.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-139: Cache/source identity

- **Priority:** P1
- **Finding:** Cache reset in OnDebugDumpCacheClicked resets all tile caches and replaces TileCacheBudget, which is demo/admin behavior not clearly safe for production operators.
- **Evidence:** external-source-reference
- **Recommendation:** Move cache debug operations behind diagnostic service and separate production-safe cache invalidation controls.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-140: Tile generation

- **Priority:** P1
- **Finding:** Each tile gets Coordinator, ClaimantIdProvider, ClaimantTokenProvider, and ReleaseReservedCacheEntry assigned after generation, making lifecycle wiring mutable and order-dependent.
- **Evidence:** external-source-reference
- **Recommendation:** Pass immutable services into tile generation or externalize scheduling to avoid half-wired tiles.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-141: Tile generation

- **Priority:** P1
- **Finding:** ClaimantTokenProvider closes over _frameTileCts, so tile behavior depends on mutable current-frame global state rather than the request that initiated generation.
- **Evidence:** external-source-reference
- **Recommendation:** Pass frame token through request-specific generation call, not a mutable provider.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-142: Tile generation

- **Priority:** P1
- **Finding:** ClaimantIdProvider is explicitly set to null to use per-tile claimant identity, so current frame ownership may be weaker than intended for shared frame cancellation semantics.
- **Evidence:** external-source-reference
- **Recommendation:** Re-evaluate claimant identity semantics for host application multi-viewport use.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-143: Pixelometer

- **Priority:** P2
- **Finding:** UpdatePixelometer does not visibly check _lifetime cancellation before reading camera/source state.
- **Evidence:** external-source-reference
- **Recommendation:** Guard pixelometer updates during shutdown/regeneration.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-144: Pixelometer

- **Priority:** P2
- **Finding:** Pixelometer status uses formatted strings, not a structured readout model.
- **Evidence:** external-source-reference
- **Recommendation:** Use typed readout state with units, source, revision, tile ID, mip, and unavailable reason.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-145: Pixelometer

- **Priority:** P2
- **Finding:** The readout reports background + defect but not whether display value is max-wins, last-wins, overlay alpha, or layer priority.
- **Evidence:** external-source-reference
- **Recommendation:** Add explicit composite policy to readout.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-146: Pixelometer

- **Priority:** P2
- **Finding:** Pixelometer reads the current SceneSource property from CanvasSurface, so a host swap could change source mid-update.
- **Evidence:** external-source-reference
- **Recommendation:** Capture SceneSource into the frame snapshot/read request and hold stable for one read.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-147: Selection

- **Priority:** P2
- **Finding:** _selectedAnnotationId is stored as string and resolved by scanning _annotations.FirstOrDefault.
- **Evidence:** external-source-reference
- **Recommendation:** Use a dictionary keyed by stable item ID or selection service with source revision.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-148: Selection

- **Priority:** P2
- **Finding:** Selection is reset to null during regeneration before new content succeeds.
- **Evidence:** external-source-reference
- **Recommendation:** Carry selection through scene swap where same logical item survives, or reset only after successful scene commit.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-149: Selection

- **Priority:** P2
- **Finding:** FeatureDataGrid.ItemsSource is updated directly from MainWindow, so selection details remain app-shell-owned.
- **Evidence:** external-source-reference
- **Recommendation:** Move selected-item detail payload into the generic/host application adapter layer.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-150: Selection

- **Priority:** P2
- **Finding:** Selection visual updates likely depend on RequestRenderAsync after click; if render is coalesced/canceled during close, UI state and detail grid can diverge.
- **Evidence:** external-source-reference
- **Recommendation:** Make selection state update and visual invalidation transactional.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-151: Zoom

- **Priority:** P2
- **Finding:** ZoomPresetComboBox.Text is assigned formatted percent manually while SelectedIndex is also used for presets.
- **Evidence:** external-source-reference
- **Recommendation:** Separate transient display text from selectable preset state to avoid combo reentrancy/confusion.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-152: Zoom

- **Priority:** P2
- **Finding:** ComputeDisplayPercent collapses ScaleX/ScaleY into one percent.
- **Evidence:** external-source-reference
- **Recommendation:** For production viewport host horizontal/vertical scale-style semantics, expose independent X/Y percent or named fit mode.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-153: Zoom

- **Priority:** P2
- **Finding:** ApplyCustomZoomAsync parses percent with double.TryParse using ambient culture by default.
- **Evidence:** external-source-reference
- **Recommendation:** Use invariant/current-culture explicitly and add tests for decimal separators.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-154: Zoom

- **Priority:** P2
- **Finding:** OnCanvasPointerWheel only updates pixelometer, relying on CanvasControl for zoom; this split means pixelometer and render updates may be scheduled by different components.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Define a single input pipeline event that updates camera, render, and pixelometer from one transaction.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-155: Geometry

- **Priority:** P2
- **Finding:** TryReadResidentPixel derives tileRows from _tiles.Count / _tileColumns, which can be wrong when last row is partial or grid shape changes.
- **Evidence:** external-source-reference
- **Recommendation:** Use explicit tile topology in scene metadata.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-156: Geometry

- **Priority:** P2
- **Finding:** GetSceneBounds uses Min/Max over tiles and assumes all tile bounds are finite and non-empty.
- **Evidence:** external-source-reference
- **Recommendation:** Validate tile bounds during scene construction and fail before publication.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-157: Geometry

- **Priority:** P2
- **Finding:** Screen-to-world conversion is duplicated by manual camera offset/scale math in pixelometer and likely other paths.
- **Evidence:** external-source-reference
- **Recommendation:** Centralize ScreenToWorld/WorldToScreen and add round-trip tests.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-158: Geometry

- **Priority:** P2
- **Finding:** Tile-center scheduling uses _tileBoundsById cache rebuilt once per scene; if tile geometry changes with source revision, stale map risk exists unless scene swap is atomic.
- **Evidence:** external-source-reference
- **Recommendation:** Keep tile bounds map inside SceneSnapshot.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-159: Rendering

- **Priority:** P2
- **Finding:** The host keeps overlay composition after ICW-315, so CanvasFrame does not appear to be a complete visual frame.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Clarify whether CanvasFrame is raster-only or full-frame; rename or extend accordingly.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-160: Rendering

- **Priority:** P2
- **Finding:** FramePublished event is used so host can populate overlays per accepted frame, but acceptance/stale discard semantics need to be guaranteed.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Add tests proving overlays update only for accepted frames.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-161: Rendering

- **Priority:** P2
- **Finding:** RasterVisible combines background and image-tile toggles, while overlay toggles are separate; this is not a general layer visibility model.
- **Evidence:** external-source-reference
- **Recommendation:** Introduce LayerVisibilitySnapshot per frame.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-162: Rendering

- **Priority:** P2
- **Finding:** Current overlay drawing is WPF-element based, which does not match the same frame-buffer lease discipline as raster.
- **Evidence:** external-source-reference
- **Recommendation:** Decide retained-WPF vs rasterized-overlay architecture per layer; preserve ordering and hit testing.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-163: Rendering

- **Priority:** P2
- **Finding:** Persistent frame shell fixed Viewbox teardown, but annotation overlay elements can still churn and fragment UI tree during dense scenes.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Pool/virtualize overlay elements or draw dense layers into DrawingVisual/bitmap.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-164: Rendering

- **Priority:** P2
- **Finding:** Background image visibility and sparse image tile visibility are independent booleans but DrawTile/DrawDefectPatch paths combine them in GenerateFrozenBitmap.
- **Evidence:** external-source-reference
- **Recommendation:** Name and test exact layer behavior: background raster, sparse image tiles, defect patches, labels.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-165: Diagnostics

- **Priority:** P2
- **Finding:** Frame diagnostics are kept in several primitive fields: _diagnosticsFrameCount, _lastFrameTicks, _totalFrameTicks, _frameCount.
- **Evidence:** external-source-reference
- **Recommendation:** Move to one RenderDiagnosticsSnapshot with rolling windows and reset/serialize support.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-166: Diagnostics

- **Priority:** P2
- **Finding:** Cache status is written to UI and debug logs, but not exposed as structured telemetry.
- **Evidence:** external-source-reference
- **Recommendation:** Expose structured cache counters for support bundle collection.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-167: Diagnostics

- **Priority:** P2
- **Finding:** Tile/coordinator counters are not associated with render request ID or source revision in the visible status model.
- **Evidence:** external-source-reference
- **Recommendation:** Include render request ID/source revision in all diagnostics.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-168: Diagnostics

- **Priority:** P2
- **Finding:** Settings persistence failure is logged only; current UI may not show that user settings were not saved.
- **Evidence:** external-source-reference
- **Recommendation:** Surface non-blocking settings-save warning.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-169: Testing

- **Priority:** P2
- **Finding:** Task tracker reports many passing test counts, but current replacement readiness requires integration tests with representative host application inspection data, which is not evidenced here.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Add production viewport host fixture tests before replacement claims.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-170: Testing

- **Priority:** P2
- **Finding:** The audit synthesis states benchmark result files, BenchmarkDotNet artifacts, profiler captures, and FastNoise2 internals were not inspected in depth.
- **Evidence:** external-source-reference
- **Recommendation:** Do a dedicated performance-evidence pass before making performance claims.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-171: Testing

- **Priority:** P2
- **Finding:** User-reproduced black flashes were verified fixed per task tracker, but that does not prove all source/layer/overlay flicker modes are covered.
- **Evidence:** external-source-reference
- **Recommendation:** Add automated visual regression around overlay/raster synchronization.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-172: Testing

- **Priority:** P2
- **Finding:** Consumer-host test proves another app can publish a frame, but not that host application source adapters/layers work.
- **Evidence:** external-source-reference
- **Recommendation:** Complement controls consumer test with host application-like adapter host test.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-173: Process

- **Priority:** P3
- **Finding:** Active tasks mention user-deferred ICW-313 and ICW-314, but those are still important to host application ergonomics.
- **Evidence:** external-source-reference
- **Recommendation:** Mark them explicitly as deferred but replacement-blocking or replacement-nonblocking based on first target surface.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-174: Process

- **Priority:** P3
- **Finding:** Audit synthesis reports duplicate/status divergence history; current tracker is useful but should remain secondary until source-verified.
- **Evidence:** external-source-reference
- **Recommendation:** Keep source verification as gate for master findings.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-175: Process

- **Priority:** P3
- **Finding:** No implementation was performed in the reconciliation, but some findings are now marked done in later active tasks; the master backlog needs status reconciliation by commit.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Create a commit-keyed status reconciliation matrix.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-176: Docs

- **Priority:** P3
- **Finding:** ADR/task terms like CanvasFrame, source, frame shell, source contract, and layer are overloaded.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Add glossary distinguishing raster frame, visual frame, source frame, render request, scene snapshot, layer plan.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-177: Docs

- **Priority:** P3
- **Finding:** The About dialog states MIT license and third-party credits in source excerpt; production host application integration may need a different attribution path.
- **Evidence:** external-source-reference
- **Recommendation:** Keep demo About dialog separate from production control library attribution/notice.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

### ICW-DEEP3-178: Maintainability

- **Priority:** P3
- **Finding:** MainWindow contains app orchestration, source implementation, render scheduling, diagnostics, settings, and selection.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Continue decomposition under ICW-022-style service extraction.
- **Validation:** add a failing unit/integration/stress/parity test where practical, then reclassify as source-backed or downgrade if the exact current source contradicts the finding.

## Highest-Value Triage Set

- **ICW-DEEP3-115 (P0)**: No explicit host application source/revision model is present in the supplied current chunk evidence; <File>external requirement source</File> implements generic ICanvasSceneSource against sample tiles/annotations.
- **ICW-DEEP3-116 (P0)**: The supplied current evidence shows a host overlay model, not the production viewport LayerManager parity model.
- **ICW-DEEP3-117 (P0)**: Runtime validation is still not equivalent to field readiness: the audit synthesis states no runtime reproduction was run for concurrency candidates and benchmark/profiler artifacts were not inspected in depth.
- **ICW-DEEP3-118 (P1)**: ICanvasSceneSource exposes QueryVisible, QueryPoint, TryReadResidentPixel, SceneBounds, TotalItemCount, and SceneChanged, but not layer identity, source availability, unit calibration, or source revision.
- **ICW-DEEP3-119 (P1)**: QueryVisible returns IReadOnlyList<ICanvasItem>, which is too shallow for host application overlay payloads that need typed layer semantics and hit-test metadata.
- **ICW-DEEP3-120 (P1)**: QueryPoint still hides hit-test tolerance inside the source implementation with a fixed probe size, despite being a reusable contract boundary.
- **ICW-DEEP3-121 (P1)**: TryReadResidentPixel returns CanvasPixelSample with one tile ID and background/defect byte values, but not the contributing layer IDs or source revision.
- **ICW-DEEP3-122 (P1)**: MainWindow holds _tiles, _tileBoundsById, _annotations, _sceneBounds, _spatialIndex, _selectedAnnotationId, and cache budget as separate mutable fields.
- **ICW-DEEP3-123 (P1)**: Visible tile tracking is stored as _lastPublishedVisibleTiles and _lastPublishedCamera, which are host-global mutable values separate from CanvasFrame.
- **ICW-DEEP3-124 (P1)**: _lastGridCamera and _lastGridTileIds cache grid rendering separately from frame revision/source revision.
- **ICW-DEEP3-125 (P1)**: RenderRequestTracker exists, but the source excerpt only demonstrates frame request sequencing; host application needs semantic invalidation when source/layer/view revisions change.
- **ICW-DEEP3-126 (P1)**: CanvasFrame revision discard can prevent out-of-order presentation, but it cannot prove the frame represents the latest host application inspection data without source revisions.
- **ICW-DEEP3-127 (P1)**: Constructor subscribes CanvasSurface events, Loaded, Closed, and CompositionTarget.Rendering; OnClosed shown unsubscribes CompositionTarget.Rendering but not all CanvasSurface events.
- **ICW-DEEP3-128 (P1)**: The shown OnClosed path calls CanvasSurface.DetachFrameShell before disposing frame buffer, but does not explicitly clear SceneSource or FramePublished handler.
- **ICW-DEEP3-129 (P1)**: OnClosed awaits _renderAction.DisposeAsync inside an async void event handler and then continues disposing objects; there is no visible top-level try/catch around shutdown.
- **ICW-DEEP3-130 (P1)**: BeginBusyOperation is called in RequestRenderAsync and RegenerateSceneAsync; if render/regeneration reenter, busy count semantics must be exact across async exceptions.
- **ICW-DEEP3-131 (P1)**: OnTilePixelsGenerated enqueues RequestRenderAsync on Dispatcher for every generated tile event.
- **ICW-DEEP3-132 (P1)**: OnTilePixelsGenerationFailed also enqueues render to retry, making failures and successes use the same scheduler path without backoff.

## Secret Handling Note

This report intentionally avoids exposing credentials, customer-private data, internal URLs beyond file/entity names from retrieved tool outputs, or any opaque binary content. It references only code symbols, task IDs, public-ish repo concepts, and high-level host application requirements already present in retrieved snippets.


