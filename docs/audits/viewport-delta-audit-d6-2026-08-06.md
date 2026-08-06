# InfiniteCanvasWPF Delta Audit D6

**Description:** Additional delta audit after the prior combined D4/D5 report, focused on newly surfaced source-backed risks around render/frame snapshots, overlay synchronization, cache pinning, and diagnostics.  
**Timestamp:** 2026-08-06 12:28 CDT  
**Author:** Copilot  
**Repository / Subject:** InfiniteCanvasWPF / production viewport replacement candidate  
**Status:** Changes Requested  
**Overall Confidence:** 82%  
**Scope:** Delta-only. This report intentionally does not restate the prior D4/D5 report except where needed for sequencing.  
**Secret Posture:** Neutral identifiers only; no credentials, customer-private data, internal URLs, or proprietary adapter names.

## Executive Summary

This D6 pass found ten additional deltas. The highest-value new finding is that the render path still assembles a frame from a captured camera plus live mutable scene/cache/display state, which means the new CanvasFrame boundary is not yet equivalent to an immutable scene/render generation contract. The next strongest new issue is cache pinning by plain tile ID while cache entries are keyed by source/revision/mip identity. Together, these reinforce the same strategic recommendation: harden immutable frame snapshots, cache identity, and overlay event context before treating the canvas as production-ready for production viewport replacement work.

## Evidence Corpus

| ID | Source | Use |
|---|---|---|
| S1 | <File>external requirement source</File> | MainWindow, PublishFrame, OnCanvasFramePublished, UpdateAnnotationLayer, RenderFrameAsync, TileCacheBudget, SampleImageGenerator, ZeroCopyBitmapFactory snippets. |
| S2 | <File>external requirement source</File> | TileWorkCoordinator disposal/start/claimant/queue behavior. |
| S3 | <File>external requirement source</File> | Active task/handoff state, ICW-144 benchmark scenario count, Wave I status, ICW-318/FrameBufferPool context. |
| S4 | <File>external requirement source</File> | Audit reconciliation, earlier accepted/refuted finding context. |
| S5 | <File>external requirement source</File> and related prior audits | Earlier directional report context for frame generation model, ownership, anisotropic mip, and grid findings. Treated as secondary unless source snippet text is present. |

## Findings Index

| ID | Priority | Area | Finding | Confidence |
|---|---:|---|---|---:|
| D6-001 | P1 | Frame snapshot / scene generation | RenderFrameAsync still assembles a frame from a captured camera plus live mutable scene/cache state | 86% |
| D6-002 | P1 | Frame publication metadata | PublishFrame mixes frame-local item list with live _spatialIndex.Count | 82% |
| D6-003 | P1 | Overlay synchronization | OnCanvasFramePublished uses host-level _lastPublishedCamera/_lastPublishedVisibleTiles instead of frame-owned overlay context | 78% |
| D6-004 | P1 | Cache pinning / identity | TileCacheBudget pins by tile.Id, not by source/revision/mip cache key | 87% |
| D6-005 | P2 | Cache ownership / render side effects | Render task mutates cache pinning state while rendering | 74% |
| D6-006 | P2 | Overlay rendering / performance | Annotation overlay rebuild allocates WPF elements, brushes, tooltips, and handlers every publish | 90% |
| D6-007 | P2 | Annotation overlay / abstraction | UpdateAnnotationLayer still silently drops non-SampleAnnotation ICanvasItem values | 90% |
| D6-008 | P2 | Render cancellation | RenderFrameAsync cancellation only gates Task.Run scheduling; inner rasterization does not observe cancellation | 81% |
| D6-009 | P2 | User-facing status / diagnostics | Cache/debug status reports generated tile IDs without source/revision/mip context | 84% |
| D6-010 | P3 | Tracker / evidence discipline | Tracker and handoff text still contain divergent benchmark scenario counts | 88% |

## Detailed Findings

### D6-001: RenderFrameAsync still assembles a frame from a captured camera plus live mutable scene/cache state

**Priority:** P1  
**Area:** Frame snapshot / scene generation  
**Classification:** Source-backed architecture defect / regression risk  
**Confidence:** 86%  

#### Evidence
- RenderFrameAsync captures camera and viewport before Task.Run, but the Task.Run body queries _spatialIndex, _tiles, and _tileCacheBudget and passes live _showBackgroundImages/_showImageTiles into GenerateFrozenBitmap.
- RegenerateSceneAsync replaces tile and annotation state, rebuilds _tileBoundsById, assigns the coordinator to tiles, publishes the spatial snapshot, and invokes SceneChanged later in the method.

#### Risk Mechanism
The frame boundary has improved, but the render request is still not a single immutable scene/render generation. A frame can be derived from one camera/viewport and later-read scene/cache/display fields. Stale-frame revision checking protects some out-of-order render publication, but it does not prove the frame was built from one coherent scene generation.

#### Recommendation
Introduce a FrameRequest or ViewportRenderSnapshot carrying scene generation ID, tile snapshot, annotation/query snapshot, display settings, cache budget handle, source revision, and camera. The background render delegate should consume only that snapshot, not MainWindow mutable fields.

#### Targeted Tests
- `Render_UsesSingleSceneGenerationSnapshot`
- `RegenerateDuringRender_OldFrameCannotMixNewSceneCount`
- `ToggleBackgroundDuringRender_StaleOrSnapshotConsistent`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-002: PublishFrame mixes frame-local item list with live _spatialIndex.Count

**Priority:** P1  
**Area:** Frame publication metadata  
**Classification:** Source-backed frame metadata consistency defect  
**Confidence:** 82%  

#### Evidence
- PublishFrame receives annotations from the completed render work but constructs CanvasFrame with totalItemCount: _spatialIndex.Count.
- The same source shows RegenerateSceneAsync can replace/publish spatial state and _annotations separately from render completion.

#### Risk Mechanism
The frame payload can carry visible items from one render result while total count is read from whatever spatial index is current at publication time. Even if the displayed raster is stale-rejected, frame metadata should be internally coherent and sourced from the same snapshot as the raster/items.

#### Recommendation
Carry TotalItemCount from the same frame snapshot/query result that produced annotations. Do not read _spatialIndex.Count inside PublishFrame; pass total count in the frame result.

#### Targeted Tests
- `PublishFrame_TotalItemCount_ComesFromFrameSnapshot`
- `RegenerateBetweenRenderAndPublish_DoesNotMixItemCounts`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-003: OnCanvasFramePublished uses host-level _lastPublishedCamera/_lastPublishedVisibleTiles instead of frame-owned overlay context

**Priority:** P1  
**Area:** Overlay synchronization  
**Classification:** Strongly inferred overlay atomicity risk  
**Confidence:** 78%  

#### Evidence
- PublishFrame assigns _lastPublishedCamera and _lastPublishedVisibleTiles, then calls CanvasSurface.PublishFrame(frame).
- OnCanvasFramePublished receives CanvasFrame frame, but uses _lastPublishedCamera and _lastPublishedVisibleTiles for grid/annotation overlay updates, while frame.Items supplies only annotations.

#### Risk Mechanism
Overlay synchronization depends on mutable side-channel fields rather than the published frame carrying all overlay context. If frame publication becomes queued, asynchronous, replayed, or multi-host/multi-viewport, this side-channel can bind a frame event to the wrong camera/tile context. Even if current PublishFrame is synchronous, the API shape is brittle.

#### Recommendation
Extend CanvasFrame or a host-only PublishedFrameContext to carry the camera snapshot and visible tile identity needed for overlays. OnCanvasFramePublished should derive all overlay state from the event payload, not mutable last-published fields.

#### Targeted Tests
- `FramePublished_OverlayUsesEventFrameContext`
- `TwoFramesPublishedQuickly_OverlayMatchesFrameRevision`
- `CanvasFrame_CarriesOverlayContextOrHostContext`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-004: TileCacheBudget pins by tile.Id, not by source/revision/mip cache key

**Priority:** P1  
**Area:** Cache pinning / identity  
**Classification:** Source-backed cache identity defect  
**Confidence:** 87%  

#### Evidence
- TileCacheBudget has _pinnedTileIds as HashSet<string> and SetPinnedTiles adds tile.Id for each visible tile.
- TryReserve eviction excludes candidates whose candidate.Tile.Id is in _pinnedTileIds, while tracked entries are keyed by BackgroundTileCacheKey containing source, tile ID, content revision, and mip level.

#### Risk Mechanism
Pinning by tile ID overpins all variants of a visible tile regardless of source, revision, or mip. This can retain stale variants, reduce the evictable set, and make no-evictable-entry failures more likely under mip or revision churn. It also conflicts with the source-qualified cache key direction already in the code.

#### Recommendation
Pin by BackgroundTileCacheKey or a frame-scoped set of visible cache keys, not plain tile ID. If the policy intentionally protects all variants of a visible tile, encode that as a named VisibleTilePinPolicy and test it explicitly.

#### Targeted Tests
- `CachePinning_DoesNotProtectStaleRevisionVariant`
- `CachePinning_DoesNotProtectOtherSourceSameTileId`
- `CachePinning_VisibleMipOnlyPolicy`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-005: Render task mutates cache pinning state while rendering

**Priority:** P2  
**Area:** Cache ownership / render side effects  
**Classification:** Strongly inferred architecture concern  
**Confidence:** 74%  

#### Evidence
- Inside RenderFrameAsync Task.Run, the code computes visibleTiles and calls _tileCacheBudget.SetPinnedTiles(visibleTiles) before GenerateFrozenBitmap.
- TileCacheBudget.SetPinnedTiles mutates the shared _pinnedTileIds set under the cache lock.

#### Risk Mechanism
A render operation changes global cache policy as a side effect. Concurrent or fast-superseded renders can leave pin state representing the last render task to execute that line, not necessarily the frame that is currently displayed or the current viewport interest set.

#### Recommendation
Move pinning into a frame-scoped cache lease/interest snapshot. The active frame should own visible cache pins and release/replace them atomically on publication or supersession.

#### Targeted Tests
- `SupersededRender_DoesNotLeaveOldPinnedTiles`
- `CachePins_BoundToPublishedFrameRevision`
- `ConcurrentRender_PinStateMatchesLatestAcceptedFrame`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-006: Annotation overlay rebuild allocates WPF elements, brushes, tooltips, and handlers every publish

**Priority:** P2  
**Area:** Overlay rendering / performance  
**Classification:** Source-backed performance and lifecycle concern  
**Confidence:** 90%  

#### Evidence
- UpdateAnnotationLayer clears annotationLayer.Children and, for each SampleAnnotation item, creates SolidColorBrush, Rectangle, Grid, Border, DeferredAnnotationToolTip, and MouseLeftButtonDown handler before adding it to the canvas.
- BuildAnnotationLabel creates a Border and TextBlock for each label when labels are enabled.

#### Risk Mechanism
The overlay layer does per-frame WPF element allocation proportional to visible annotations. This is acceptable for a demo slice but likely expensive for production viewport-like defect density, fast pan/zoom, or live updates. Repeated event handler allocation also complicates lifecycle auditing.

#### Recommendation
Introduce an overlay virtualization/recycling layer or move annotation overlays into a retained visual/adornment model keyed by item ID and frame revision. Cache frozen brushes by class color and display mode.

#### Targeted Tests
- `AnnotationLayer_ReusesVisualsForUnchangedItems`
- `AnnotationLayer_DoesNotAllocateHandlersPerFrame`
- `OverlayPerf_VisibleAnnotationStress`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-007: UpdateAnnotationLayer still silently drops non-SampleAnnotation ICanvasItem values

**Priority:** P2  
**Area:** Annotation overlay / abstraction  
**Classification:** Source-backed abstraction defect  
**Confidence:** 90%  

#### Evidence
- UpdateAnnotationLayer accepts IReadOnlyList<ICanvasItem> items, but continues only when item is SampleAnnotation; all other ICanvasItem implementations are skipped.

#### Risk Mechanism
The control/source contract can carry generic ICanvasItem values, but host overlay composition only renders SampleAnnotation. This is expected for the demo host, but it means current overlay behavior is not a reusable external-host annotation/selection/tooltip model.

#### Recommendation
Add an item visual adapter contract such as ICanvasOverlayPresenter or IViewportItemVisualSource. Keep SampleAnnotation-specific rendering in the app adapter, not in a generic publish path.

#### Targeted Tests
- `ExternalICanvasItem_CanRenderOverlayViaAdapter`
- `SampleAnnotationRenderer_IsAppLocal`
- `NonSampleItem_NotSilentlyDroppedWhenAdapterProvided`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-008: RenderFrameAsync cancellation only gates Task.Run scheduling; inner rasterization does not observe cancellation

**Priority:** P2  
**Area:** Render cancellation  
**Classification:** Source-backed responsiveness concern  
**Confidence:** 81%  

#### Evidence
- RenderFrameAsync awaits Task.Run(() => { ... factory.GenerateFrozenBitmap(...) }, cancellationToken).
- ZeroCopyBitmapFactory.GenerateFrozenBitmap clears memory, draws tiles, draws defect patches, creates and freezes the bitmap; the reviewed method signature does not include a CancellationToken.

#### Risk Mechanism
Once the render delegate starts, cancellation does not stop the expensive rasterization loop. Superseded renders still burn CPU until completion and only then get rejected by RenderRequestTracker. This is safe for correctness but weak for fast-scroll responsiveness.

#### Recommendation
Pass a render cancellation token to GenerateFrozenBitmap, DrawTile loops, and DrawDefectPatch. Check cancellation at row/tile/annotation boundaries and return a canceled frame result without publishing.

#### Targeted Tests
- `RenderCancellation_StopsLongTileLoop`
- `RenderCancellation_StopsAnnotationPatchLoop`
- `SupersededRender_DoesNotCompleteFullRasterWork`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-009: Cache/debug status reports generated tile IDs without source/revision/mip context

**Priority:** P2  
**Area:** User-facing status / diagnostics  
**Classification:** Source-backed diagnostics gap  
**Confidence:** 84%  

#### Evidence
- OnDebugDumpCacheClicked builds fetchedTiles from _tiles.Where(tile => tile.IsBackgroundFetched).Select(tile => tile.Id).
- TileCacheBudget.DescribeStatus returns aggregate bytes, tile count, variant count, and eviction count but no source/revision/mip breakdown.

#### Risk Mechanism
Diagnostics can identify that tiles are fetched or variants exist, but not which source/revision/mip keys are resident or stale. This limits usefulness when debugging multi-mip, source-qualified, or stale-generation cache behavior.

#### Recommendation
Add a structured cache diagnostics snapshot with source ID, tile ID, content revision, mip level, resident bytes, pin state, generated state, and eviction reason.

#### Targeted Tests
- `CacheDiagnostics_IncludesSourceRevisionMip`
- `DebugDumpCache_ReportsVariantKeys`
- `CacheDiagnostics_NoPrivatePaths`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

### D6-010: Tracker and handoff text still contain divergent benchmark scenario counts

**Priority:** P3  
**Area:** Tracker / evidence discipline  
**Classification:** Source-backed process/documentation issue  
**Confidence:** 88%  

#### Evidence
- Active tasks text states ICW-144 added eight benchmark scenarios.
- The ICW-144 ticket/handoff states the benchmark file has seven benchmark methods and says parameterized cases are not additional methods.

#### Risk Mechanism
This is not a runtime bug, but it undermines tracker reliability. Agents can accidentally cite the inflated scenario count or use it as evidence of broader stress coverage than exists.

#### Recommendation
Normalize tracker wording to the seven-method count and explicitly distinguish BenchmarkDotNet parameterized cases from scenario methods.

#### Targeted Tests
- `TaskTracker_BenchmarkScenarioCountMatchesSource`
- `ValidateTaskTracker_DetectsKnownScenarioCountMismatch`

#### Acceptance Criteria
- The finding is either fixed with direct source/test evidence or downgraded with a documented reason.
- Any public/reusable API change is covered by a consumer-host or boundary test.
- Any remaining uncertainty is captured in the tracker as a request or open question.

## Recommended Implementation Sequence

| Order | Findings | Theme | Objective |
|---:|---|---|---|
| 1 | D6-001, D6-002 | Frame snapshot integrity | Create a frame/render snapshot so raster, items, counts, display options, source revision, and cache context are coherent. |
| 2 | D6-003 | Overlay context atomicity | Move overlay camera/visible-tile context into frame payload or a frame-owned host context. |
| 3 | D6-004, D6-005 | Cache pinning identity | Make pins key/revision/source-aware and frame-scoped. |
| 4 | D6-008 | Render cancellation | Stop superseded render work earlier than post-facto stale rejection. |
| 5 | D6-006, D6-007 | Overlay abstraction/performance | Separate demo SampleAnnotation rendering from generic overlay contracts and reduce per-frame WPF allocations. |
| 6 | D6-009, D6-010 | Diagnostics/process cleanup | Improve cache diagnostics and correct tracker scenario counts. |

## Proposed New / Updated Tickets

| Ticket | Priority | Summary | Findings |
|---|---:|---|---|
| ICW-D6-FRAME-SNAPSHOT | P1 | Introduce immutable FrameRequest/FrameResult carrying scene generation, camera, viewport, display options, source revisions, visible tiles, visible items, total counts, and cache context. | D6-001, D6-002 |
| ICW-D6-FRAME-OVERLAY-CONTEXT | P1 | Make overlay camera and visible-tile context frame-owned rather than side-channel state. | D6-003 |
| ICW-D6-CACHE-PIN-KEYS | P1 | Replace tile-ID pinning with BackgroundTileCacheKey or ViewportTileKey pinning. | D6-004, D6-005 |
| ICW-D6-RENDER-COOPERATIVE-CANCEL | P2 | Thread cancellation through GenerateFrozenBitmap, DrawTile loops, and DrawDefectPatch. | D6-008 |
| ICW-D6-OVERLAY-RECYCLING | P2 | Add retained/recycled overlay visuals and app-local SampleAnnotation renderer adapter. | D6-006, D6-007 |
| ICW-D6-CACHE-DIAGNOSTICS | P2 | Add structured cache diagnostic snapshot including source/revision/mip/pin state. | D6-009 |
| ICW-D6-TRACKER-BENCHMARK-COUNT | P3 | Normalize ICW-144 scenario count language to seven benchmark methods. | D6-010 |

## Consolidated Test Plan

### P1 Tests
- `Render_UsesSingleSceneGenerationSnapshot`
- `RegenerateDuringRender_OldFrameCannotMixNewSceneCount`
- `PublishFrame_TotalItemCount_ComesFromFrameSnapshot`
- `FramePublished_OverlayUsesEventFrameContext`
- `CachePinning_DoesNotProtectStaleRevisionVariant`
- `CachePinning_DoesNotProtectOtherSourceSameTileId`
- `SupersededRender_DoesNotLeaveOldPinnedTiles`

### P2/P3 Tests
- `RenderCancellation_StopsLongTileLoop`
- `AnnotationLayer_ReusesVisualsForUnchangedItems`
- `ExternalICanvasItem_CanRenderOverlayViaAdapter`
- `CacheDiagnostics_IncludesSourceRevisionMip`
- `TaskTracker_BenchmarkScenarioCountMatchesSource`

## Assumptions

| ID | Assumption | Confidence | Handling |
|---|---|---:|---|
| A-D6-001 | CanvasSurface.PublishFrame currently likely raises FramePublished in process, but the API should not rely on side-channel state even if current behavior is synchronous. | Medium | Treat D6-003 as an API hardening issue unless source proves event payload already carries all overlay context. |
| A-D6-002 | Source-qualified cache identity remains a target requirement for production viewport adapter work. | High | Keep D6-004 as P1 until source/revision/mip pinning is implemented or explicitly rejected. |
| A-D6-003 | Per-frame WPF overlay allocation is acceptable for small demo scenes but risky for production defect density. | Medium-high | Validate with visible annotation stress and downgrade if production overlays are out of scope. |

## Requests / Missing Evidence

- Exact current commit SHA for the concat bundle used in this D6 pass.
- Full current source for CanvasSurface.PublishFrame and CanvasFrame so overlay event sync can be classified as source-backed rather than strongly inferred.
- Full current source for FrameBufferPool.Windows.cs to confirm current reuse/fence behavior after ICW-318.
- Current tests for ICW-328 revision wiring and CanvasFrame validation.
- Runtime stress traces for rapid regenerate-during-render and fast-scroll overlay allocation pressure.

## Final Recommendation

**Decision: Changes Requested.** This D6 pass does not invalidate the prior D4/D5 report; it sharpens the next slice. The most important next work is to make render/frame publication atomic by construction: one frame request, one frame result, one frame-owned overlay context, and cache pins that match the same source/revision/mip identity. After that, add render cancellation and overlay recycling so fast navigation does not waste CPU or allocate avoidable WPF object graphs.


