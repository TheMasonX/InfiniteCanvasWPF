
# InfiniteCanvasWPF Readiness Review for production viewport Replacement

**Description:** Source-backed readiness review, bug/improvement synthesis, architecture audit, and implementation plan for using `repository` as a production viewport replacement.  
**Timestamp:** 2026-08-06 09:54 CDT  
**Author:** Copilot  
**Repository Under Review:** `repository` plus external source snapshots of `external source snapshot\Repos\InfiniteCanvasWPF`  
**host application Target:** production viewport host / production viewport ecosystem  
**Status:** **Changes Requested**  
**Overall Readiness:** **Prototype foundation only, not replacement-ready**  
**Confidence:** 82% overall. Higher for opened source-file findings; lower for findings carried forward from prior secondary audit reports where the exact current source line was not re-opened in this pass.  
**Classification:** Engineering architecture audit, bug report, implementation plan, migration readiness assessment.

---

## Executive Summary

InfiniteCanvasWPF has several traits worth preserving: a separate project structure, a Core/Rendering/Spatial/ViewModels split, Windows-specific tests, immutable camera snapshots, a spatial-index abstraction, background tile-generation coordination, mip-aware loading, and an explicit attempt to govern render coalescing and cache pressure. The solution snapshot shows a multi-project layout with benchmarks, application, Core, Rendering, Spatial, ViewModels, and test projects. external-source external-source-reference lines 1-16: solution contains benchmarks, src App/Core/Rendering/Spatial/ViewModels, and tests/Windows.Tests projects.

That said, it is **not ready** to become the production viewport replacement. The current reviewed implementation is closer to a strong WPF rendering prototype than a production viewport-compatible viewport subsystem. The blocker is not just missing features. The blocker is that the core rendering lifecycle, mapped-surface ownership, spatial identity model, production viewport host source contracts, settings/view selection boundaries, and production viewport layer parity are not yet formalized enough for a live industrial inspection viewport.

The strongest source-backed issue is the frame-surface model. `ZeroCopyBitmapFactory` creates `InteropBitmap` objects backed by the factory's native file-mapping section. The code then clears and rewrites the same mapped view in `GenerateFrozenBitmap`, freezes the WPF wrapper, and returns it. The comments explicitly say returned bitmaps are backed by the factory's section and valid only while the factory and mapping remain alive. external-source external-source-reference lines 13-24, 64-104, 106-148, 251-267: InteropBitmap instances are backed by factory memory sections; Render methods clear/rewrite the mapped view and Freeze the wrapper; Dispose unmaps the view and closes the section. For a high-frame-rate viewport, this is not a safe enough frame-publishing contract unless there is a separate, proven surface lease/retirement model.

The next strongest issue is lifecycle. `MainWindow` currently owns scene state, spatial index, camera, tile coordinator, cache budget, render action, UI fields, timers, settings, and diagnostics. During scene regeneration the code reinitializes mutable state, cancels tile work, disposes prior defect-template pools, and asynchronously builds the new tile list. external-source external-source-reference lines 15-91 and 148-188: MainWindow owns mutable scene/render fields, creates timers/coalescing action, and scene regeneration reinitializes state, cancels tile work, disposes old pools, then asynchronously generates a new tile set. A production viewport needs immutable render snapshots and generation-scoped resource ownership. Without that boundary, stale renders can mix old and new scene resources, and shutdown/regeneration can invalidate resources still reachable by active render paths.

The production viewport host side raises the bar. production viewport is not looking for a faster image viewer alone. Product docs describe goals around faster defect access, fast refresh, extended defect context, customizable queries, full-resolution streaming video, 60 FPS smooth scrolling, and known gaps such as multi-inspection view, active inspection view, live-mode changes, and lane viewer. external-source external-source-reference lines 19-29, 32-40, 45-56, 122-130: production viewport goals include faster access, fast refresh, extended defect context, customizable queries, full-resolution streaming video, 60 fps smooth scrolling, and acknowledged gaps in multi-defect/active defect/lane/live-mode features. The viewport-specific KB/source summary describes a fixed 13-layer LayerManager stack: alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels. external-source external-source-reference lines 4-12: LLM Wiki entry describes production viewport LayerManager as a fixed 13-layer stack: alignment layer, edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels. InfiniteCanvasWPF must therefore become an inspection-aware, revision-aware, layer-aware viewport engine, not just a pan/zoom canvas.

**Decision:** changes requested. Keep the repo as a prototype foundation, but do not substitute it for production viewport until the P0/P1 gates in this report are complete.

---

## Evidence Corpus

### Opened primary source snapshots

- `InfiniteCanvasWPF.slnx.txt` - repository/project structure. external-source external-source-reference lines 1-16: solution contains benchmarks, src App/Core/Rendering/Spatial/ViewModels, and tests/Windows.Tests projects.
- `CameraTransform.cs` - camera state, CAS update pattern, snapshots, clamp math. external-source external-source-reference lines 25-31, 48-80, 88-121, 164-192: CameraTransform exposes immutable snapshots, CAS-based pan/zoom, viewport-bounds calculation, and bound clamping.
- `ZeroCopyBitmapFactory.Windows.cs` - mapped section, InteropBitmap creation, frame drawing, disposal. external-source external-source-reference lines 13-24, 64-104, 106-148, 251-267: InteropBitmap instances are backed by factory memory sections; Render methods clear/rewrite the mapped view and Freeze the wrapper; Dispose unmaps the view and closes the section.
- `SampleImageTile.cs` - tile generation/cache state, mip path, defect-template pool reference. external-source external-source-reference lines 8-66, 111-172, 180-207: SampleImageTile owns generated pixels, mip pixels, claimant provider, defect pool reference, sync cache gate, and non-blocking pixel retrieval path.
- `LiveSpatialIndexService.cs` - live spatial cache, publish model, query behavior. external-source external-source-reference lines 13-43, 44-97, 98-128: LiveSpatialIndexService Count sums snapshot/hot/publishing arrays; Query concatenates snapshot/publishing/hot matches; PublishSnapshotAsync uses a single publish-in-progress gate and CAS state swaps.
- `CoalescingAsyncAction.cs` - render/coalescing action and disposal semantics. external-source external-source-reference lines 19-31, 32-57, 58-82: CoalescingAsyncAction returns the processing task, cancels lifetime during disposal, awaits the processing task, and loops until no request remains.
- `TileWorkCoordinator.cs` - background tile work queue/coalescing/cancel path. external-source external-source-reference lines 96-143, 202-219, 248-305, 323-348: TileWorkCoordinator coalesces requests by cache key, cancels all work, starts background items, dispatches completed pixels even after cancellation flag, and releases reservations on cancel.
- `BackgroundTileContracts.cs` - source/revision/mip tile contracts and IBackgroundTileSource. external-source external-source-reference lines 3-68, 78-106, 107-141: Background tile descriptors include source ID, tile ID, content revision, bounds, native dimensions, payload validation, and IBackgroundTileSource; mip policy selects using the minimum camera scale.
- `ISpatialEntity.cs`, `ISpatialIndexService.cs`, `ISpatialIndexBuilder.cs` - spatial contracts. external-source external-source-reference lines 1-5 and external-source-reference lines 3-7, external-source-reference lines 3-6: ISpatialEntity exposes only Bounds; ISpatialIndexService exposes Count/Query; ISpatialIndexBuilder.Build has no cancellation token.
- `ViewportZoomPolicy.cs` - per-axis wheel delta and single display percent. external-source external-source-reference lines 4-61 and 62-87: ViewportZoomPolicy computes X/Y deltas from current scales, minimum scales, and requested delta; one display percent is derived from the dominant minimum scale.
- `MainWindow.xaml.cs` - demo application orchestration and regeneration lifecycle. external-source external-source-reference lines 15-91 and 148-188: MainWindow owns mutable scene/render fields, creates timers/coalescing action, and scene regeneration reinitializes state, cancels tile work, disposes old pools, then asynchronously generates a new tile set.

### Internal source-backed / directional production viewport host context

- `3.1 - production viewport v1.pptx` - production viewport goals, streaming video needs, and product gaps. external-source external-source-reference lines 19-29, 32-40, 45-56, 122-130: production viewport goals include faster access, fast refresh, extended defect context, customizable queries, full-resolution streaming video, 60 fps smooth scrolling, and acknowledged gaps in multi-defect/active defect/lane/live-mode features.
- `Viewport_LayerManager_viewport_overlay_layer_stack_concept.json` - source-code-oriented KB summary of the 13-layer production viewport host viewport stack. external-source external-source-reference lines 4-12: LLM Wiki entry describes production viewport LayerManager as a fixed 13-layer stack: alignment layer, edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels.
- `Viewport_viewport_wpf_ecosystem_viewport control_layermanager_architecture_concept.json` - source-code-oriented KB summary of viewport control, LayerManagerFactory, MainViewModel, and snapshot controller integration gap. external-source external-source-reference lines 20-32: LLM Wiki entry describes the WPF viewport ecosystem, viewport control, LayerManagerFactory injection, MainViewModel draw gates, snapshot controller gap, and extension via virtual method promotion.

### Secondary synthesis inputs

The previous reports opened in this pass include the combined audit and delta audits. These reports are useful for synthesis and issue discovery, but I do **not** treat them as primary proof unless the underlying source was also opened in this pass. They are carried as secondary analysis and marked accordingly where used.

---

## Readiness Verdict

| Area | Grade | Status | Reason |
|---|---:|---|---|
| Repository decomposition | B | Promising | Separate Core/Rendering/Spatial/ViewModels/App/tests/benchmarks projects exist. |
| WPF prototype completeness | B- | Useful prototype | Pan/zoom/render/coalescing/cache concepts exist. |
| production viewport product parity | D | Not ready | production viewport host source adapters, layer parity, live inspection, alignment layer, and host application tool compatibility are not proven. |
| Frame-surface ownership | F | Blocker | Mapped section / InteropBitmap lifetime is not a production frame-publishing contract. |
| production viewport host data model integration | D | Not ready | Needs immutable snapshot boundary and adapters for inspection, view selection, display settings, revisions, and sources. |
| Spatial identity/revision semantics | D | Blocker | Spatial entities expose Bounds only; live index can concatenate duplicates. |
| Tile cache/cancellation semantics | C- | Risky | Coordinator exists but completion/cancel/reservation semantics need lease-level correctness. |
| Threading/shutdown lifecycle | C- | Risky | Coalescing action and regeneration are promising but lack bounded shutdown and generation isolation. |
| Test posture | C | Improving | Tests exist, but production acceptance tests are missing for production viewport host, surface leases, live growth, service loss, and high-DPI/multi-monitor behavior. |
| Replacement readiness | D | Changes requested | Use as foundation, not a direct replacement.

---

## Source-Backed Findings


### ICW-RDY-001: Not production-ready as a drop-in production viewport replacement

- **Priority:** P0
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** The repo structure is promising, but the reviewed code is still a synthetic visualization prototype with no proven production viewport host source adapter, inspection identity, revision model, axis units calibration contract, 13-layer parity, or production lifecycle boundary.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Do not wire it into production viewport as the default viewport. Use it as a prototype foundation behind an adapter/feature flag only after P0/P1 gates below are closed.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-LIFE-001: Mapped-surface lifetime is not safe enough for a live viewport

- **Priority:** P0
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** InteropBitmap instances are explicitly backed by the factory memory section, yet GenerateFrozenBitmap clears and rewrites the same mapped view before returning a frozen wrapper. Freezing does not copy pixels by source evidence.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Replace factory-level reusable memory with per-frame surface leases or a validated ring of immutable frame buffers with explicit publication and retirement semantics.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-LIFE-002: Surface disposal can invalidate displayed frames

- **Priority:** P0
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** ZeroCopyBitmapFactory.Dispose unmaps the view and closes the section. Any published InteropBitmap whose backing section is owned by that factory is only valid while the factory remains alive.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Frame publisher must own presented surfaces until presentation is retired. Avoid passing raw factory lifetime across UI and renderer layers.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-LIFE-003: Scene regeneration mutates core state before new generation success is guaranteed

- **Priority:** P0
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** MainWindow regenerates by replacing spatial state, selected annotation, camera, cache budget, canceling coordinator work, and disposing prior template pools before the new Task.Run generation has completed.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Introduce immutable SceneSnapshot and RenderGeneration. Build the next generation off to the side, then atomically swap only after validation succeeds.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-LIFE-004: Coalescing disposal can block indefinitely if action ignores cancellation

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** DisposeAsync cancels the lifetime CTS and then awaits the processing task; the contract does not enforce bounded cancellation if the action does not observe the token.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Add bounded shutdown behavior, diagnostics, and a test where the action ignores cancellation.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-TILE-001: Canceled tile work may still dispatch completed pixels

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** TileWorkCoordinator StartWorkItem calls item.DispatchCompleted(pixels) after the lock section even when wasCanceled is true according to its own local variable.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Do not dispatch completion to stale claimants unless the tile-level epoch check is proven and tested. Prefer typed Completed/Canceled terminal notifications.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-TILE-002: Coordinator reservations are accounting-only in visible source

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** Medium-high
- **Evidence:** ReleaseReservation only increments a counter; no reserve token or owner callback is visible in the opened source.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Make reservation an explicit lease object returned by cache budget admission; dispose it exactly once in all terminal paths.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-TILE-003: Mip selection uses minimum scale and can pick too-low resolution for non-uniform X/Y zoom

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** BackgroundTileMipPolicy.SelectMipLevel chooses Math.Min(camera.ScaleX, camera.ScaleY), then Log2(1/minimumScale). For non-uniform scaling, this biases toward the more zoomed-out axis.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Select mip per axis or choose the scale that preserves the most magnified axis. Add asymmetric zoom tests.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-SPAT-001: Spatial contracts lack identity, revision, replacement, and deletion semantics

- **Priority:** P0
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** ISpatialEntity has only Bounds; LiveSpatialIndexService concatenates snapshot, publishing, and hot matches without logical identity.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Introduce ISpatialRecord or equivalent immutable record with EntityId, Revision, Kind, Bounds, and tombstone/replace behavior.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-SPAT-002: LiveSpatialIndexService.Query can duplicate logical entities

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** Count sums all buffers and Query appends snapshot index results plus publishing items plus hot items. Without identity, same logical entity can appear multiple times.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Deduplicate by stable entity ID at query boundary or make publish pipeline replace by identity.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-SPAT-003: Concurrent publish request completion does not mean caller data is published

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** PublishSnapshotAsync returns immediately when _publishInProgress is already 1.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Expose a generation-aware publish API returning the generation included in the snapshot, or serialize with pending-generation tracking.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-SPAT-004: Spatial index builder cannot observe cancellation

- **Priority:** P2
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** ISpatialIndexBuilder.Build accepts only IReadOnlyList<T>; PublishSnapshotAsync wraps it in Task.Run with a cancellation token but cannot pass cancellation into the builder body.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Add CancellationToken to Build or separate cancellable async build interface.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-ZOOM-001: One zoom display percent is insufficient for production viewport host horizontal/vertical scale behavior

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** ViewportZoomPolicy computes one display percent based on the dominant minimum scale, while production viewport host viewport requirements include independently meaningful CD and MD zoom state.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Expose ZoomState with ScaleX, ScaleY, FitScaleX, FitScaleY, DisplayPercentX, DisplayPercentY, and current fit mode.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-ZOOM-002: Zoom target multiplication can overflow before validation

- **Priority:** P2
- **Classification:** Source-backed
- **Confidence:** Medium-high
- **Evidence:** ComputeWheelDeltas validates inputs before multiplying current scale by requestedScaleDelta; it does not validate targetScaleX/Y before returning ratios.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Validate targetScaleX/Y for finite positive values before computing deltas.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-UI-001: MainWindow is still the app, host, orchestrator, renderer trigger, settings owner, and scene manager

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** High
- **Evidence:** MainWindow owns spatial index, ViewModel, camera, coalescing action, timers, scene state, tile coordinator, settings, scrollbars, diagnostics, and UI state.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Extract engine, WPF host control, input adapter, settings adapter, frame publisher, and demo app shell.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-UI-002: Per-frame WPF element creation risk remains a scalability blocker

- **Priority:** P1
- **Classification:** Source-backed
- **Confidence:** Medium
- **Evidence:** Prior opened audit source notes a visible-annotation path that creates WPF elements/animations per visible annotation per frame. This was derived from MainWindow source reviewed in the audit; full source lines beyond opening were not re-opened here.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Move dense overlays to retained/batched drawing and reserve WPF elements for sparse interaction adorners only.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-VIEW-001: production viewport replacement must preserve the existing 13-layer production viewport host overlay semantics

- **Priority:** P0
- **Classification:** Source-backed from KB/source summaries
- **Confidence:** High for requirement, source verification still requested
- **Evidence:** LLM Wiki source-oriented entry describes a fixed LayerManager stack including alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Define layer parity tests before substituting the viewport.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-VIEW-002: host application target requires full-resolution video, smooth scrolling, defect context, and analysis-tool combinations

- **Priority:** P0
- **Classification:** Source-backed from product docs and KB/source summaries
- **Confidence:** High
- **Evidence:** production viewport presentation explicitly lists full-resolution streaming video, lossless compression, 60 FPS smooth scrolling, fast refresh, and extended defect context goals.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Benchmark ICW against these product criteria using production viewport host inspection sources, not synthetic tiles.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-VIEW-003: Viewport cannot depend on mutable production viewport host ViewModels as render-state snapshots

- **Priority:** P0
- **Classification:** Source-backed from KB/source summaries
- **Confidence:** Medium-high
- **Evidence:** Delta audit 5 and opened KB context identify mutable selection/settings hazards; the exact current source should be re-read before final merge, but the architectural requirement is strong.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Translate UI state into immutable ViewportFrameSnapshot before render scheduling.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


### ICW-VIEW-004: production viewport host viewport control/MainViewModel ecosystem must be respected or deliberately replaced

- **Priority:** P1
- **Classification:** Source-backed from KB/source summaries
- **Confidence:** High for requirement, source verification still requested
- **Evidence:** LLM Wiki entry describes viewport control, LayerManagerFactory injection, MainViewModel draw gates, and snapshot controller not being in the ecosystem.
- **Impact:** If left unresolved, this can produce incorrect images, stale overlays, resource leaks, unpredictable shutdown behavior, production viewport feature regressions, or inability to diagnose production viewport failures.
- **Recommendation:** Choose: integrate ICW as a LayerManager/FramePublisher behind viewport control, or explicitly replace the viewport control stack with compatibility shims.
- **Counterargument / nuance:** Some issues may be acceptable inside a demo app or single-user prototype. They are not acceptable as hidden assumptions inside a production viewport host viewport replacement.
- **Validation criteria:** Add a focused regression test or integration scenario that fails before the fix and passes after the fix. Confirm behavior under rapid pan/zoom, regeneration, shutdown, and live update conditions.


---

## Consolidated Bug and Improvement Backlog

### P0 blockers

1. Replace `ZeroCopyBitmapFactory` frame lifetime model with explicit leases or copy-owned immutable frames.
2. Add generation-scoped immutable `SceneSnapshot` and `ViewportFrameSnapshot` before rendering.
3. Define production viewport host production source adapters for inspection, view selection, tile retrieval, overlays, alignment layer, calibration, and revisions.
4. Implement spatial identity/revision/replacement/tombstone semantics.
5. Define production viewport layer parity contract and regression tests.
6. Keep ICW behind a feature flag; do not default it into production viewport.

### P1 required fixes

1. Fix tile cancellation/completion semantics so canceled/stale work cannot publish to a current generation.
2. Replace cache reservation counter behavior with disposable reservation leases.
3. Make coalescing/render shutdown bounded and observable.
4. Split demo `MainWindow` responsibilities into production host, engine, frame publisher, render scheduler, and demo shell.
5. Add per-axis zoom display model for horizontal/vertical scale equivalents.
6. Add cross-generation render tests.
7. Add viewer disposal tests with active background tile work.
8. Add high-DPI and multi-monitor coordinate tests.
9. Add production viewport host source-loss/reconnect adapter behavior.
10. Add golden-image tests for core layer combinations.

### P2 improvements

1. Replace dense WPF element creation with retained/batched overlay rendering.
2. Add predictive tile loading after visible-tile correctness is stable.
3. Add pixelometer consistency tests against final composite values.
4. Add visible diagnostics overlay for test builds.
5. Add memory budget dashboard and event counters.
6. Add A/B comparison harness against current production viewport.
7. Add snapshot/ruler synchronization contract.
8. Add fake production viewport host source package for deterministic tests.

---

## production viewport host / production viewport Replacement Requirements

### Functional parity requirements

A viable replacement viewport must support at minimum:

1. **Inspection image display** with correct axis units coordinate mapping.
2. **Full-resolution streaming video path** and downsampled/preview path with explicit source revision.
3. **alignment layer layer** with time/position alignment.
4. **Web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels** in the same logical order as the existing layer stack.
5. **Independent horizontal/vertical scale zoom and scroll state** with fit-to-width, fit-to-height, manual pan, and live catch-up behavior.
6. **Ruler synchronization** from the same frame snapshot as the rendered image.
7. **Pixelometer** that reads final composite state or clearly labels source-layer values.
8. **Live inspection growth** without invalidating old frames or duplicating logical entities.
9. **Source-loss and reconnect behavior** that degrades visibly and recovers deterministically.
10. **Multi-viewport memory governance** across inspection view, inspection view, Snapshot, active defect, and future tools.

### Non-functional requirements

1. No frame can reference native memory that may be overwritten before the compositor is done.
2. No render can observe partially swapped scene state.
3. No tile generation can publish after its revision/generation is invalidated.
4. No UI-bound observable state can be raised from an arbitrary worker thread.
5. No settings or view selection object used by rendering can hold UI commands, dispatcher state, weak-messenger registrations, or mutable event subscriptions.
6. All render inputs must be immutable, versioned, and serializable enough for diagnostics.
7. All feature flags must have fallback behavior.
8. All production failures must include enough source IDs, revisions, frame IDs, layer IDs, and surface IDs to diagnose without reproducing locally.

---

## Proposed Production Architecture

### Component model

```text
production viewport Host
  -> InfiniteCanvasViewport WPF Control
       -> InputAdapter
       -> ViewportEngine
           -> ViewportStateReducer
           -> RenderScheduler
           -> SceneSnapshotProvider
           -> LayerRenderPlanBuilder
           -> TileScheduler
           -> FramePublisher
       -> production viewport hostViewportSourceAdapter
           -> InspectionSource
           -> BackgroundTileSource
           -> OverlaySourceSet
           -> ViewSelectionSnapshotProvider
           -> DisplaySettingsSnapshotProvider
```

### Render pipeline

```text
UI gesture / live source update
  -> ViewportStateReducer creates ViewportRequest
  -> production viewport host adapter creates immutable ViewportFrameSnapshot
  -> LayerRenderPlanBuilder creates immutable RenderPlan
  -> TileScheduler materializes required tiles under cache leases
  -> Renderer draws into FrameSurfaceLease
  -> FramePublisher publishes RenderedFrame
  -> Previous surfaces retire only through explicit lease retirement
```

### Snapshot boundary

`ViewportFrameSnapshot` should include:

- Inspection ID
- Inspection revision / source revision vector
- View selection keys, not mutable ViewModels
- axis units visible range
- axis units scale
- ROI offset
- DPI/device scale
- Client pixel size
- Display settings value object
- Layer toggles
- Selected defect IDs
- Render generation ID
- Source connection state
- Timestamp and monotonic sequence number

### Frame surface model

Use one of two safe models:

1. **Safe copy model:** renderer fills a pooled `byte[]` / native buffer, copies into a new/frozen `WriteableBitmap` or independent immutable presentation image, then releases render buffer.
2. **Lease model:** renderer obtains `FrameSurfaceLease`, WPF presentation holds/retains the lease, and reuse is forbidden until explicit retire. This requires conservative assumptions because WPF `OnRender` is not equal to compositor completion.

For production viewport, start with safe copy or a conservative ring. Optimize only after benchmarks prove copying is the bottleneck.

---

## Super Detailed Implementation Plan


### Phase 0 - Evidence lock and branch control

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Capture exact GitHub commit SHA or vendor drop hash.
- Generate file inventory, project graph, and public API list.
- Map every opened source path to GitHub path and production viewport host integration path.
- Create a reusable ViewportViewportReadiness test matrix.
- Freeze current report findings into Jira/ADO issues with source anchors.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 1 - Surface ownership and frame publication

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Replace reusable mapped section with explicit FrameSurfaceLease.
- Define IFramePublisher with PublishAsync(RenderedFrame frame, CancellationToken).
- Use per-frame WriteableBitmap copy or ring-buffered sections with retirement acknowledgments.
- Add stress tests with rapid frame replacement, disposal during presentation, and forced GC.
- Add ETW/Serilog diagnostics for frame generation, publish, retire, surface allocation, and dropped frame causes.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 2 - Immutable scene/render lifecycle

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Create SceneSnapshot containing immutable tiles, overlays, calibration, source revisions, and generation ID.
- Build next scene completely before swapping.
- Make render requests hold one SceneSnapshot reference.
- Prevent scene regeneration from disposing resources visible to active renders.
- Add generation mismatch tests for regeneration during render, shutdown during render, and failed generation rollback.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 3 - production viewport host source adapter contract

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Define IInspectionViewportSource with inspection ID, revision, selected views, source validity, and connection state.
- Define IBackgroundTileSource adapter for Gray8/alignment layer/image streams.
- Define IOverlaySource adapters for defects, frames, edges, lanes, cameras, fiducials, film edges, slits, selected defects, labels, and region.
- Define unit/calibration contract for axis units, DPI, and ROI offset.
- Add adapter tests using captured production viewport host fixtures.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 4 - Spatial identity, revisions, and live growth

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Replace ISpatialEntity-only records with SpatialRecord<T> including stable ID and revision.
- Add replacement and tombstone support.
- Make LiveSpatialIndexService query results unique by logical ID.
- Return publish generation tokens from snapshot publication.
- Add duplicate, deletion, live-growth, and concurrent-publish tests.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 5 - Rendering architecture split

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Extract InfiniteCanvasViewport WPF control.
- Extract ViewportEngine independent of WPF visuals.
- Extract InputAdapter for mouse, keyboard, scrollbars, zoom, and selection.
- Extract RenderScheduler using CoalescingAsyncAction or evolution with bounded shutdown.
- Keep demo-only synthetic generation outside production assemblies.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 6 - Coordinate and zoom parity

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Model axis units scale independently.
- Represent fit-to-width, fit-to-height, fit-to-area, manual zoom, and scroll origin explicitly.
- Add WebToScreen and ScreenToWeb round-trip tests.
- Add ruler snapshot synchronization tests.
- Add asymmetric scale tests for mips and pixelometer.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 7 - Layer parity

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Implement layer stack contract matching LayerManager order.
- Implement render plan with per-layer revision and diagnostic status.
- Add layer-specific toggles and settings snapshots.
- Add golden-image tests for representative inspections.
- Support z-order, selection, hover, and tooltip hit testing without per-entity WPF object churn.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 8 - Cache, tile, and backpressure

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Replace accounting-only reservation with explicit cache leases.
- Define tile cache key as SourceId + TileId + ContentRevision + MipLevel + PixelFormat if relevant.
- Add memory-budget tests across multi-viewport scenarios.
- Add cancellation semantics for stale viewports and live source revisions.
- Add priority scheduling for visible tiles over predictive/offscreen tiles.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 9 - Diagnostics and observability

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Expose dropped-frame reasons.
- Expose tile queue depth, hit/miss, cache pressure, and cancellation counters.
- Log per-frame scene generation, render generation, source revision, layer status, and surface lease ID.
- Add debug overlay for render generation, frame time, and visible tile count.
- Add self-test page for compositor and adapter health.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 10 - production viewport integration path

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Choose embedded viewport control adapter vs replacement control strategy.
- Feature-flag the ICW viewport per tool/window/customer build configuration.
- Implement side-by-side viewport comparison harness.
- Add fallback to current viewport on adapter fault, source loss, or validation mismatch.
- Add operator-visible diagnostics only where actionable.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


### Phase 11 - Release hardening

**Goal:** Close one replacement-readiness gate without relying on hidden UI/materialization assumptions.

**Tasks:**
- Run large inspection loading tests.
- Run live-mode catch-up and pause/resume tests.
- Run multi-monitor/high-DPI tests.
- Run service restart/disconnect/reconnect tests.
- Run memory longevity and overnight live inspection tests.
- Run customer workflow parity tests for SSAB/Elval-style scenarios.

**Unit tests:**
- Happy path test for the new behavior.
- Cancellation/stale-generation test.
- Disposal/shutdown test.
- Invalid input or source-loss test.
- Diagnostic assertion test where observable IDs/counters must be present.

**Acceptance criteria:**
- No source-backed P0/P1 finding in this phase remains open.
- All new tests fail against the old implementation and pass after changes.
- No production adapter depends on demo-only synthetic classes.
- All diagnostics include source ID, revision, render generation, and surface/frame IDs where applicable.
- Behavior is documented in a small ADR or engineering note.

**Risks:**
- Overfitting the adapter to current production viewport host mutable ViewModels instead of immutable snapshots.
- Hiding missing parity behind feature flags without measurable acceptance tests.
- Optimizing zero-copy rendering before proving correctness.
- Carrying demo UI assumptions into production assemblies.


---

## Test Plan

### Unit tests

- `FrameSurfaceLeaseTests`
  - publishing does not allow reuse while the frame is retained
  - disposing old surface does not invalidate current frame
  - rapid publish/retire does not reuse active memory
- `SceneSnapshotTests`
  - regeneration failure leaves prior scene intact
  - active render sees one generation only
  - shutdown cancels next-generation build without disposing current presentation resources
- `SpatialIdentityTests`
  - duplicate logical ID is replaced or deduplicated
  - tombstone removes prior record
  - publish returns included generation
  - concurrent publish does not report completion for missing caller generation
- `TileCoordinatorTests`
  - canceled claimant cannot receive success publish
  - reservation lease disposed exactly once
  - failed work releases budget
  - pre-canceled claimant does not leave item admitted without owner
- `ViewportZoomPolicyTests`
  - asymmetric horizontal/vertical scale zoom preserves display state
  - overflow targets are rejected
  - fit-to-width and fit-to-height have independent percentages
- `production viewport hostAdapterTests`
  - selected views snapshot by stable keys
  - display settings snapshot is immutable and messenger-free
  - source revision changes invalidate old render plan

### Integration tests

- Load a representative inspection and compare current production viewport vs ICW viewport screenshots.
- Pan/zoom rapidly while inspection source updates.
- Switch selected views while tile generation is active.
- Toggle every layer in the 13-layer stack.
- Exercise alignment layer with live/pause/catch-up modes.
- Simulate StationServer/StorageServer loss and reconnect.
- Run high-DPI, multi-monitor, minimized/restored, and window-resize stress.
- Run overnight live-mode memory and handle leak test.

### Performance tests

- Frame time p50/p95/p99 during live scrolling.
- Tile cache hit rate, miss rate, eviction rate, and memory pressure.
- Surface allocation/reuse counts.
- Dropped frame reasons.
- UI-thread frame budget and input latency.
- Scrollbar/zoom gesture latency.
- Pixelometer latency under live updates.

### Acceptance criteria for replacement readiness

1. All P0 blockers are closed with source-backed fixes and tests.
2. All P1 issues either closed or explicitly accepted with owner and fallback.
3. ICW can run side-by-side with current viewport on the same inspection and preserve layer/order/coordinate parity.
4. ICW can be disabled by feature flag without breaking production viewport.
5. No demo-only synthetic tile/source code is required by production adapter paths.
6. No published frame can reference overwritten/disposed memory.
7. No render can mix scene generations.
8. No source revision can publish stale tiles or overlays into a new frame.
9. Operators can recover from source loss/reconnect without restarting host application unless the current product requirement explicitly allows restart.
10. Diagnostics are sufficient for remote support cases.

---

## Assumptions

| ID | Assumption | Confidence | Handling |
|---|---|---:|---|
| A1 | The external source snapshots correspond closely to the GitHub repo requested. | Medium | GitHub clone was blocked in the execution environment; exact commit verification remains a request. |
| A2 | production viewport replacement target includes inspection view and inspection view viewport behavior, not only a standalone demo image viewer. | High | Product presentation and KB context support this. |
| A3 | production viewport host layer parity is required for operator confidence. | High | Existing layer stack and production viewport feature goals support this. |
| A4 | Safe correctness should beat zero-copy optimization initially. | High | Surface ownership risk is severe and hard to debug. |
| A5 | The current repo can be evolved rather than discarded. | Medium-high | Architecture decomposition is promising, but lifecycle and adapters need major work. |

---

## Open Questions

1. What exact GitHub commit SHA should be treated as the review baseline?
2. Is the replacement intended for inspection view only, inspection view only, Snapshot, or all viewport surfaces?
3. Which production viewport host branch/version is the integration target: SV9 clone, 9.0.0, 9.1, or another release branch?
4. Is alignment layer parity mandatory in V1, or can the first feature-flagged prototype be static inspection/overlay only?
5. What customer workflow is the first acceptance target: SSAB, Elval, Nucor, generic large inspection, or internal demo?
6. Are product-specific overlays intentionally out of scope for production viewport replacement viewport, consistent with the current layer stack summary?
7. Can the current `viewport control` ecosystem be extended, or is the team willing to replace it with a new control and compatibility adapter?
8. What are the memory ceilings per viewport and per host application process on target systems?
9. Should tile source adapters support offline SSF only, live inspection only, or both in V1?
10. What operator-visible fallback should occur when ICW detects adapter/source mismatch?

---

## Requests / Missing Evidence

1. Exact GitHub commit SHA and branch for `repository`.
2. Full source tree export or accessible clone for the current commit.
3. Current production viewport host target branch and commit.
4. Current `LayerManager.cs`, `ViewportModel.cs`, `ViewportViewModel.cs`, `MainViewModel.cs`, `DisplaySettings.cs`, `OverlaySettingsViewModel.cs`, `GrayDisplayViewModel.cs`, and `ViewSelection.cs` source from the target branch.
5. Representative inspection fixtures for inspection view, inspection view, alignment layer, fiducials, film edges, lanes, slits, labels, frames, and selected defects.
6. Current production viewport acceptance criteria for live mode, alignment layer, frame rate, and memory.
7. Any existing customer-specific must-not-regress workflows.
8. Decision on whether ICW should integrate behind `viewport control` or replace the existing WPF viewport control entirely.

---

## Final Recommendation

Do **not** use InfiniteCanvasWPF as a direct production viewport replacement yet. Use it as a **prototype foundation** for a new viewport engine after the P0 architecture gates are closed.

The most important implementation move is to stop thinking of this as "drop a better canvas into production viewport." Treat it as a new production viewport subsystem with four hard boundaries:

1. **production viewport host state -> immutable viewport snapshot**
2. **Viewport snapshot -> immutable render plan**
3. **Render plan -> leased/copy-owned frame surface**
4. **Frame surface -> explicit publish/retire lifecycle**

If those boundaries are implemented first, the existing ICW work can become valuable. If not, the replacement will reproduce the hardest production viewport problems in a new rendering stack: stale state, hidden native lifetime bugs, UI-thread hazards, coordinate drift, and customer-visible parity gaps.

---

## Appendix A - Suggested First Engineering Ticket Breakdown

| Ticket | Title | Priority | Acceptance Criteria |
|---|---|---:|---|
| ICW-host application-P0-001 | Create frame surface lease model | P0 | No displayed frame can reference overwritten/disposed native memory. |
| ICW-host application-P0-002 | Add immutable SceneSnapshot and ViewportFrameSnapshot | P0 | Active render uses one generation only. Failed regeneration preserves prior frame/scene. |
| ICW-host application-P0-003 | Add production viewport host source adapter interfaces | P0 | Production path no longer depends on synthetic tile classes. |
| ICW-host application-P0-004 | Add spatial identity/revision contract | P0 | Duplicate/replacement/tombstone behavior tested. |
| ICW-host application-P0-005 | Define 13-layer parity contract | P0 | Golden tests cover base layer order and toggles. |
| ICW-host application-P1-001 | Fix tile coordinator cancellation publishing | P1 | Canceled/stale work cannot publish success into current frame. |
| ICW-host application-P1-002 | Replace cache reservation counter with leases | P1 | Reservation released exactly once under success, failure, cancel, reject, dispose. |
| ICW-host application-P1-003 | Bound CoalescingAsyncAction shutdown | P1 | Ignored-cancellation action cannot hang teardown indefinitely. |
| ICW-host application-P1-004 | Extract WPF host control from demo MainWindow | P1 | Engine can run under test host without demo app UI. |
| ICW-host application-P1-005 | Add horizontal/vertical scale zoom state model | P1 | Independent scales and display percentages tested. |
| ICW-host application-P1-006 | Add side-by-side viewport comparison harness | P1 | Same fixture can render current host application viewport and ICW viewport for comparison. |

---

## Appendix B - Review Notes on Prior Reports

Prior audits were valuable and largely directionally consistent with the source read performed here. However, this final report intentionally distinguishes:

- **Opened source-backed findings**: promoted to source-backed status above.
- **Secondary prior-audit findings**: useful for backlog synthesis but requiring revalidation against the exact current commit.
- **KB/wiki findings**: useful for orientation and requirements, but still requiring source verification before code-claim promotion.

This preserves evidence discipline while still using the previous report formats as requested.


