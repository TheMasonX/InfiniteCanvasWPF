# InfiniteCanvasWPF Deep Bug Sweep Delta
**Description:** Additional deep pass focused on finding many more current bugs, latent defects, production viewport replacement blockers, and high-value improvements from the supplied ICW chunks.
**Timestamp:** 2026-08-06 09:54 CDT
**Author:** Copilot
**Status:** Additional findings; source-backed where cited; runtime validation still required.
**Confidence:** Mixed. P0/P1 integration gaps are high confidence from provided chunks; some P2/P3 items rely on audit-ledger evidence and should be rechecked against current HEAD before coding.

## Executive Summary
This deeper pass found a much larger backlog. The most serious pattern is that core canvas stabilization has improved, but the code is still not a production viewport product: source identity, source revisions, layer parity, workflow parity, and runtime stress evidence remain missing. The second pattern is that several fixed areas still have follow-on hazards: cancellation completion semantics, queued claimant cleanup, reentrant cache/coordinator locks, non-transactional scene regeneration, sample-type leakage, and unfinished interaction ownership.

## Findings Table
| ID | Priority | Area | Finding | Recommendation | Evidence |
|---|---:|---|---|---|---|
| ICW-DEEP-001 | P0 | host application adapter gap | Current abstractions are generic canvas/source contracts; supplied evidence does not show production viewport host inspection, alignment layer, or overlay adapters. | Build ViewportInspectionSource, ViewportBackgroundTileSource, ViewportOverlaySourceSet; block replacement until wired. | external-source-reference, external-source-reference, external-source-reference, external-source-reference |
| ICW-DEEP-002 | P0 | 13-layer parity not implemented | production viewport layer stack has alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, labels; ICW path is generic raster/items and host overlays. | Create ViewportLayerStack contract and golden tests. | external-source-reference, external-source-reference |
| ICW-DEEP-003 | P0 | Synthetic source IDs still in production-like render path | RenderFrameAsync builds BackgroundTileCacheKey("synthetic", tile.Id, epoch, mipLevel). | Replace with source adapter ID and revision before any host application path. | external-source-reference |
| ICW-DEEP-004 | P0 | Runtime concurrency not reproduced | Audit reconciliation says concurrency candidates were source-traced, but no runtime reproduction was run. | Add cancellation-storm, fast-scroll, multi-viewport stress tests. | external-source-reference |
| ICW-DEEP-005 | P1 | MainWindow still owns production host responsibilities | MainWindow owns spatial index, camera, FrameBufferPool, TileWorkCoordinator, cache budget, render request tracker, frame CTSs, scene state, and overlay composition. | Extract ViewportEngine/ViewportHost. | external-source-reference |
| ICW-DEEP-006 | P1 | Regeneration is not transactional | RegenerateSceneAsync mutates/swallows multiple state fields and then awaits source generation/publish; older audits identify missing partial-failure rollback. | Introduce SceneSnapshot and atomic swap. | external-source-reference, external-source-reference |
| ICW-DEEP-007 | P1 | Shutdown does not await generation gate | OnClosed cancels/disposes render/coordinator/pool/generation gate, but evidence from audit ledger flags no await on generation gate. | Add orderly shutdown state machine; finish/cancel generation before disposing shared resources. | external-source-reference, external-source-reference |
| ICW-DEEP-008 | P1 | Frame buffer contract still relies on convention | FrameBufferPool composition fencing is present, but frame publication still hands InteropBitmap backed by memory section and then publishes/retires buffer. | Promote to IFrameSurfaceLease with explicit lifetime/retire contract. | external-source-reference |
| ICW-DEEP-009 | P1 | Stale frame guard protects render order, not production viewport host source revision | CanvasFrame revision uses render request version; no evidence of production viewport host source revision vector in frame identity. | Include inspection/source/layer revision vector in frame. | external-source-reference, external-source-reference |
| ICW-DEEP-010 | P1 | Canceled work can still dispatch completion | StartWorkItem records wasCanceled but always calls DispatchCompleted(pixels), relying on tile epoch checks. | Prefer explicit canceled terminal callback and do not dispatch success to canceled claimants. | external-source-reference |
| ICW-DEEP-011 | P1 | Queued cancel path does not clear claimant registrations | Prior delta notes CancelWorkItem queued branch dispatches failed/removes item without clearing claimants/registrations; current TileWorkItem has registration storage/removal methods. | Add ClearClaimants/DisposeRegistrations on all terminal paths. | external-source-reference |
| ICW-DEEP-012 | P1 | Reentrant coordinator/cache lock chain remains | TryReserve can call EvictCacheEntry, which calls RemoveClaimant while coordinator lock is held; source comments mark this safe only through same-thread reentrancy. | Break callback-after-lock or enforce non-async lock-chain invariant with tests/review gate. | external-source-reference, external-source-reference |
| ICW-DEEP-013 | P1 | Cache accounting still has multi-mip risk | Ticket text states mip payloads can add ~33 percent more bytes and must be summed; current cache reserves per key. | Make byte accounting exact across all resident mips and release paths. | external-source-reference |
| ICW-DEEP-014 | P1 | Eviction can discard active/generating content | Audit synthesis tracks eviction selecting actively generating tile as ICW-104/305 follow-up. | Add in-progress residency state to eviction policy. | external-source-reference |
| ICW-DEEP-015 | P1 | No formal multi-viewport memory governance | Cache budget is per MainWindow instance; host application target can have multiple viewport surfaces. | Design shared budget service with per-viewport leases. | external-source-reference, external-source-reference |
| ICW-DEEP-016 | P1 | Pixelometer works through scene source but remains SampleAnnotation/SampleImageTile-specific in host | TryReadResidentPixel uses _tiles geometry and QueryPoint, then casts hit items to SampleAnnotation for defect value. | Move pixel readout semantics into adapter/layer contracts. | external-source-reference |
| ICW-DEEP-017 | P1 | Pixelometer tile-row math assumes _tiles.Count / _tileColumns | TryReadResidentPixel computes tileRows = Math.Max(1, _tiles.Count / _tileColumns), which is only safe for exact grid sizes. | Use configured _tileRows or source-provided tile grid, not count division. | external-source-reference |
| ICW-DEEP-018 | P1 | QueryPoint still embeds fixed probe size | QueryPoint contains const double probeSize = 0.01. | Make hit-test tolerance explicit, DPI/scale-aware, and adapter-owned. | external-source-reference |
| ICW-DEEP-019 | P1 | Overlay rendering ignores generic ICanvasItem types | UpdateAnnotationLayer skips every item that is not SampleAnnotation. | Move typed visuals to renderer/adapters; generic control must not depend on sample model. | external-source-reference |
| ICW-DEEP-020 | P1 | Per-frame overlay element churn remains | UpdateAnnotationLayer clears AnnotationLayer and allocates Border/Grid/Rectangle/ToolTip/labels for each frame. | Use retained/batched visuals or virtualization for dense overlays. | external-source-reference |
| ICW-DEEP-021 | P1 | Tooltip ownership remains unfinished | ICW-314 remains proposed; current overlay creates DeferredAnnotationToolTip in host. | Finish ICW-314 before reusable host application control claim. | external-source-reference, external-source-reference |
| ICW-DEEP-022 | P1 | CanvasOverlayHost internals-visible-to app is a boundary smell | Wave H notes overlay host stays internal and app accesses it through InternalsVisibleTo. | Replace with explicit overlay presenter API before external host adoption. | external-source-reference |
| ICW-DEEP-023 | P1 | Canvas control input abstraction remains deferred | ICW-313 notes CanvasControl implements pan, zoom, anchor pan, scrollbar logic directly and remains proposed/deferred. | Defer only if scope-limited; for host application, create interaction-service seam. | external-source-reference |
| ICW-DEEP-024 | P2 | CanvasViewModel.Zoom dead wrapper | Audit text says CanvasViewModel.Zoom has zero callers and wheel path reaches through Camera directly. | Delete or replace with proper separate-X/Y wrapper. | external-source-reference |
| ICW-DEEP-025 | P2 | ComputeMinimumZoom lacks local zero guard | Audit synthesis confirms ComputeMinimumZoom divides by SceneBounds.Width/Height without local guard. | Guard or make precondition explicit and test public method directly. | external-source-reference |
| ICW-DEEP-026 | P2 | Anisotropic mip selection under-resolves zoomed-in axis | BackgroundTileMipPolicy.SelectMipLevel uses Math.Min(scaleX, scaleY); ICW-325 says larger scale should bind per ADR. | Fix selection for non-uniform zoom. | external-source-reference, external-source-reference |
| ICW-DEEP-027 | P2 | Single selected mip in interest set is not enough for anisotropic views | RenderFrameAsync computes one mip level and uses it in all visible keys. | Support per-axis/per-layer mip or binding-axis policy. | external-source-reference, external-source-reference |
| ICW-DEEP-028 | P2 | Prefetch set is empty | RenderFrameAsync publishes visible keys and new empty HashSet for prefetch; comments say configurable margin can be added later. | Add predictive/preload margin for fast scrolling after correctness gates. | external-source-reference |
| ICW-DEEP-029 | P2 | Unknown distance returns 0 and can prioritize stale/unknown keys | squaredDistanceFromCenter returns 0 for keys with no known bounds. | Unknown bounds should sort after known visible work, not at center. | external-source-reference |
| ICW-DEEP-030 | P2 | Tile-grid cache key comparison ignores geometric duplicates/order nuance | Grid skip uses tile ID array sequence and camera equality; distinct boundaries are built using Distinct over doubles. | Use normalized screen line set or stable geometry hash. | external-source-reference |
| ICW-DEEP-031 | P2 | Distinct double boundary values can create near-duplicate grid lines | UpdateTileGridLayer uses Distinct on worldX/worldY doubles from tile bounds. | Canonicalize grid positions or use tile index topology. | external-source-reference |
| ICW-DEEP-032 | P2 | Overlay z/layer model is still ad hoc | Host updates tile grid and annotation layer only; no typed layer contract or order registry. | Introduce ordered layer render plan. | external-source-reference, external-source-reference |
| ICW-DEEP-033 | P2 | Background noise seamlessness is internally contradictory | ICW-324 says noise uses per-tile seed and local min/max normalization, conflicting with seamless worldspace sampling. | Resolve deterministic per-tile vs seamless requirement. | external-source-reference, external-source-reference |
| ICW-DEEP-034 | P2 | Local noise normalization can seam even with shared seed | Audit pass says per-tile local min/max normalization can still seam adjacent tiles. | Use scene-wide/config-derived normalization. | external-source-reference |
| ICW-DEEP-035 | P2 | Objects spanning tiles remains unimplemented/deferred | ICW-075 proposes global object settings and cross-tile margins. | Needed if production viewport host defects/items can span tile boundaries. | external-source-reference |
| ICW-DEEP-036 | P2 | Boundary conventions still inconsistent | Audit synthesis tracks closed SpatialBounds.Intersects vs half-open renderer/tile lookup conventions. | Define one boundary policy and update tests. | external-source-reference |
| ICW-DEEP-037 | P2 | TileGridIndexLookup unchecked index risk remains tracked | Audit ledger says row*columns+column is not checked. | Validate final computed index before indexing tiles. | external-source-reference |
| ICW-DEEP-038 | P2 | Settings validation mismatch around ObjectsPerTile | Audit ledger says CanvasUserSettings.IsValid lacks upper bound while SampleImageGenerator throws for ObjectsPerTile > 256. | Use shared validation constants and tests. | external-source-reference |
| ICW-DEEP-039 | P2 | MinimumSparseTilePixelSize parameter is unused/dead | Audit ledger states persisted/validated MinimumSparseTilePixelSize is not referenced, and DrawTile parameter unused. | Wire into DrawTile or remove setting/ticket. | external-source-reference |
| ICW-DEEP-040 | P2 | Benchmarks can measure non-production paths | Audit says ProjectionAndBitmapBenchmarks uses legacy point overload with no production caller; ICW-133 still needs realistic shipped tile path. | Benchmark production GenerateFrozenBitmap overload with real tiles/annotations. | external-source-reference |
| ICW-DEEP-041 | P2 | Fast-scroll benchmarks are proposed, not completed | ICW-P0 sequencing shows ICW-144 proposed for fast-scroll stress benchmarks. | Complete ICW-144 before performance claims. | external-source-reference |
| ICW-DEEP-042 | P2 | ApplyDetailsWithGdiPlus concurrency risk not reproduced | Audit evidence says System.Drawing Bitmap/Graphics are constructed inside coordinator worker tasks with failure mode not runtime-reproduced. | Serialize GDI+ or remove from hot path; add concurrent factory stress. | external-source-reference |
| ICW-DEEP-043 | P2 | Async void error surface remains broad | Audit shows async void handlers can crash app and dispatcher safety net was a finding; MainWindow still shows async void UI handlers. | Centralize async event wrapper with logging/status handling. | external-source-reference, external-source-reference |
| ICW-DEEP-044 | P2 | OnTilePixelsGenerationFailed blindly re-renders and can retry-loop | Failure handler triggers RequestRenderAsync so pipeline can retry; no backoff/terminal failure state shown. | Add per-key failure budget/backoff and diagnostics. | external-source-reference |
| ICW-DEEP-045 | P2 | Tile generation event subscriptions depend on explicit unsubscribe | MainWindow subscribes tile events after regeneration and unsubscribes old tiles; shutdown/regeneration correctness depends on all paths reaching unsubscribe. | Move subscriptions into disposable SceneSnapshot. | external-source-reference |
| ICW-DEEP-046 | P2 | Feature panel still app-specific and sample-model-bound | SelectedAnnotationFeatures comes from SampleAnnotation.GetFeatureDisplayItems and binds directly to FeatureDataGrid. | Move inspector payload to generic item metadata or production viewport host adapter. | external-source-reference |
| ICW-DEEP-047 | P2 | host application product goals lack acceptance mapping | host application docs list speed, streaming, smooth scrolling, defect context, customizable queries; ICW tasks do not show a parity matrix. | Create acceptance matrix with fixtures. | external-source-reference, external-source-reference |
| ICW-DEEP-048 | P2 | snapshot controller gap still unresolved for replacement scope | Viewport ecosystem KB says snapshot controller is a gap outside the ecosystem. | Decide whether replacement includes snapshot controller and add adapter surface. | external-source-reference |
| ICW-DEEP-049 | P3 | Duplicate/stale tracker hygiene remains a risk | Audit synthesis notes duplicate IDs/status divergence and legacy tracker validator errors. | Run tracker cleanup before using tickets as ground truth. | external-source-reference, external-source-reference |
| ICW-DEEP-050 | P3 | Dead/unreferenced abstractions create false readiness signals | Audit ledger lists IRenderer, IBackgroundTileSource, MipOptions, BackgroundTileDescriptor/Request/Payload only test-used/unreferenced. | Either wire into production path or remove/mark future scaffold. | external-source-reference, external-source-reference |
| ICW-DEEP-051 | P3 | Frame diagnostics average is lifetime average, not windowed | RenderFrameAsync accumulates _totalFrameTicks/_frameCount and logs every 120 frames. | Expose rolling p95/p99 and dropped-frame reasons for field diagnostics. | external-source-reference |
| ICW-DEEP-052 | P3 | Status text reports one zoom axis | StatusText formats Zoom {camera.ScaleX:F3}x only. | Show X/Y scale or host application horizontal/vertical scale units. | external-source-reference |

## Detailed Findings
### ICW-DEEP-001: host application adapter gap
- **Priority:** P0
- **Finding:** Current abstractions are generic canvas/source contracts; supplied evidence does not show production viewport host inspection, alignment layer, or overlay adapters.
- **Evidence:** external-source-reference, external-source-reference, external-source-reference, external-source-reference
- **Recommendation:** Build ViewportInspectionSource, ViewportBackgroundTileSource, ViewportOverlaySourceSet; block replacement until wired.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-002: 13-layer parity not implemented
- **Priority:** P0
- **Finding:** production viewport layer stack has alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, labels; ICW path is generic raster/items and host overlays.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Create ViewportLayerStack contract and golden tests.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-003: Synthetic source IDs still in production-like render path
- **Priority:** P0
- **Finding:** RenderFrameAsync builds BackgroundTileCacheKey("synthetic", tile.Id, epoch, mipLevel).
- **Evidence:** external-source-reference
- **Recommendation:** Replace with source adapter ID and revision before any host application path.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-004: Runtime concurrency not reproduced
- **Priority:** P0
- **Finding:** Audit reconciliation says concurrency candidates were source-traced, but no runtime reproduction was run.
- **Evidence:** external-source-reference
- **Recommendation:** Add cancellation-storm, fast-scroll, multi-viewport stress tests.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-005: MainWindow still owns production host responsibilities
- **Priority:** P1
- **Finding:** MainWindow owns spatial index, camera, FrameBufferPool, TileWorkCoordinator, cache budget, render request tracker, frame CTSs, scene state, and overlay composition.
- **Evidence:** external-source-reference
- **Recommendation:** Extract ViewportEngine/ViewportHost.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-006: Regeneration is not transactional
- **Priority:** P1
- **Finding:** RegenerateSceneAsync mutates/swallows multiple state fields and then awaits source generation/publish; older audits identify missing partial-failure rollback.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Introduce SceneSnapshot and atomic swap.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-007: Shutdown does not await generation gate
- **Priority:** P1
- **Finding:** OnClosed cancels/disposes render/coordinator/pool/generation gate, but evidence from audit ledger flags no await on generation gate.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Add orderly shutdown state machine; finish/cancel generation before disposing shared resources.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-008: Frame buffer contract still relies on convention
- **Priority:** P1
- **Finding:** FrameBufferPool composition fencing is present, but frame publication still hands InteropBitmap backed by memory section and then publishes/retires buffer.
- **Evidence:** external-source-reference
- **Recommendation:** Promote to IFrameSurfaceLease with explicit lifetime/retire contract.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-009: Stale frame guard protects render order, not production viewport host source revision
- **Priority:** P1
- **Finding:** CanvasFrame revision uses render request version; no evidence of production viewport host source revision vector in frame identity.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Include inspection/source/layer revision vector in frame.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-010: Canceled work can still dispatch completion
- **Priority:** P1
- **Finding:** StartWorkItem records wasCanceled but always calls DispatchCompleted(pixels), relying on tile epoch checks.
- **Evidence:** external-source-reference
- **Recommendation:** Prefer explicit canceled terminal callback and do not dispatch success to canceled claimants.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-011: Queued cancel path does not clear claimant registrations
- **Priority:** P1
- **Finding:** Prior delta notes CancelWorkItem queued branch dispatches failed/removes item without clearing claimants/registrations; current TileWorkItem has registration storage/removal methods.
- **Evidence:** external-source-reference
- **Recommendation:** Add ClearClaimants/DisposeRegistrations on all terminal paths.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-012: Reentrant coordinator/cache lock chain remains
- **Priority:** P1
- **Finding:** TryReserve can call EvictCacheEntry, which calls RemoveClaimant while coordinator lock is held; source comments mark this safe only through same-thread reentrancy.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Break callback-after-lock or enforce non-async lock-chain invariant with tests/review gate.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-013: Cache accounting still has multi-mip risk
- **Priority:** P1
- **Finding:** Ticket text states mip payloads can add ~33 percent more bytes and must be summed; current cache reserves per key.
- **Evidence:** external-source-reference
- **Recommendation:** Make byte accounting exact across all resident mips and release paths.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-014: Eviction can discard active/generating content
- **Priority:** P1
- **Finding:** Audit synthesis tracks eviction selecting actively generating tile as ICW-104/305 follow-up.
- **Evidence:** external-source-reference
- **Recommendation:** Add in-progress residency state to eviction policy.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-015: No formal multi-viewport memory governance
- **Priority:** P1
- **Finding:** Cache budget is per MainWindow instance; host application target can have multiple viewport surfaces.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Design shared budget service with per-viewport leases.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-016: Pixelometer works through scene source but remains SampleAnnotation/SampleImageTile-specific in host
- **Priority:** P1
- **Finding:** TryReadResidentPixel uses _tiles geometry and QueryPoint, then casts hit items to SampleAnnotation for defect value.
- **Evidence:** external-source-reference
- **Recommendation:** Move pixel readout semantics into adapter/layer contracts.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-017: Pixelometer tile-row math assumes _tiles.Count / _tileColumns
- **Priority:** P1
- **Finding:** TryReadResidentPixel computes tileRows = Math.Max(1, _tiles.Count / _tileColumns), which is only safe for exact grid sizes.
- **Evidence:** external-source-reference
- **Recommendation:** Use configured _tileRows or source-provided tile grid, not count division.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-018: QueryPoint still embeds fixed probe size
- **Priority:** P1
- **Finding:** QueryPoint contains const double probeSize = 0.01.
- **Evidence:** external-source-reference
- **Recommendation:** Make hit-test tolerance explicit, DPI/scale-aware, and adapter-owned.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-019: Overlay rendering ignores generic ICanvasItem types
- **Priority:** P1
- **Finding:** UpdateAnnotationLayer skips every item that is not SampleAnnotation.
- **Evidence:** external-source-reference
- **Recommendation:** Move typed visuals to renderer/adapters; generic control must not depend on sample model.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-020: Per-frame overlay element churn remains
- **Priority:** P1
- **Finding:** UpdateAnnotationLayer clears AnnotationLayer and allocates Border/Grid/Rectangle/ToolTip/labels for each frame.
- **Evidence:** external-source-reference
- **Recommendation:** Use retained/batched visuals or virtualization for dense overlays.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-021: Tooltip ownership remains unfinished
- **Priority:** P1
- **Finding:** ICW-314 remains proposed; current overlay creates DeferredAnnotationToolTip in host.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Finish ICW-314 before reusable host application control claim.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-022: CanvasOverlayHost internals-visible-to app is a boundary smell
- **Priority:** P1
- **Finding:** Wave H notes overlay host stays internal and app accesses it through InternalsVisibleTo.
- **Evidence:** external-source-reference
- **Recommendation:** Replace with explicit overlay presenter API before external host adoption.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-023: Canvas control input abstraction remains deferred
- **Priority:** P1
- **Finding:** ICW-313 notes CanvasControl implements pan, zoom, anchor pan, scrollbar logic directly and remains proposed/deferred.
- **Evidence:** external-source-reference
- **Recommendation:** Defer only if scope-limited; for host application, create interaction-service seam.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-024: CanvasViewModel.Zoom dead wrapper
- **Priority:** P2
- **Finding:** Audit text says CanvasViewModel.Zoom has zero callers and wheel path reaches through Camera directly.
- **Evidence:** external-source-reference
- **Recommendation:** Delete or replace with proper separate-X/Y wrapper.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-025: ComputeMinimumZoom lacks local zero guard
- **Priority:** P2
- **Finding:** Audit synthesis confirms ComputeMinimumZoom divides by SceneBounds.Width/Height without local guard.
- **Evidence:** external-source-reference
- **Recommendation:** Guard or make precondition explicit and test public method directly.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-026: Anisotropic mip selection under-resolves zoomed-in axis
- **Priority:** P2
- **Finding:** BackgroundTileMipPolicy.SelectMipLevel uses Math.Min(scaleX, scaleY); ICW-325 says larger scale should bind per ADR.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Fix selection for non-uniform zoom.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-027: Single selected mip in interest set is not enough for anisotropic views
- **Priority:** P2
- **Finding:** RenderFrameAsync computes one mip level and uses it in all visible keys.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Support per-axis/per-layer mip or binding-axis policy.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-028: Prefetch set is empty
- **Priority:** P2
- **Finding:** RenderFrameAsync publishes visible keys and new empty HashSet for prefetch; comments say configurable margin can be added later.
- **Evidence:** external-source-reference
- **Recommendation:** Add predictive/preload margin for fast scrolling after correctness gates.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-029: Unknown distance returns 0 and can prioritize stale/unknown keys
- **Priority:** P2
- **Finding:** squaredDistanceFromCenter returns 0 for keys with no known bounds.
- **Evidence:** external-source-reference
- **Recommendation:** Unknown bounds should sort after known visible work, not at center.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-030: Tile-grid cache key comparison ignores geometric duplicates/order nuance
- **Priority:** P2
- **Finding:** Grid skip uses tile ID array sequence and camera equality; distinct boundaries are built using Distinct over doubles.
- **Evidence:** external-source-reference
- **Recommendation:** Use normalized screen line set or stable geometry hash.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-031: Distinct double boundary values can create near-duplicate grid lines
- **Priority:** P2
- **Finding:** UpdateTileGridLayer uses Distinct on worldX/worldY doubles from tile bounds.
- **Evidence:** external-source-reference
- **Recommendation:** Canonicalize grid positions or use tile index topology.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-032: Overlay z/layer model is still ad hoc
- **Priority:** P2
- **Finding:** Host updates tile grid and annotation layer only; no typed layer contract or order registry.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Introduce ordered layer render plan.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-033: Background noise seamlessness is internally contradictory
- **Priority:** P2
- **Finding:** ICW-324 says noise uses per-tile seed and local min/max normalization, conflicting with seamless worldspace sampling.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Resolve deterministic per-tile vs seamless requirement.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-034: Local noise normalization can seam even with shared seed
- **Priority:** P2
- **Finding:** Audit pass says per-tile local min/max normalization can still seam adjacent tiles.
- **Evidence:** external-source-reference
- **Recommendation:** Use scene-wide/config-derived normalization.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-035: Objects spanning tiles remains unimplemented/deferred
- **Priority:** P2
- **Finding:** ICW-075 proposes global object settings and cross-tile margins.
- **Evidence:** external-source-reference
- **Recommendation:** Needed if production viewport host defects/items can span tile boundaries.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-036: Boundary conventions still inconsistent
- **Priority:** P2
- **Finding:** Audit synthesis tracks closed SpatialBounds.Intersects vs half-open renderer/tile lookup conventions.
- **Evidence:** external-source-reference
- **Recommendation:** Define one boundary policy and update tests.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-037: TileGridIndexLookup unchecked index risk remains tracked
- **Priority:** P2
- **Finding:** Audit ledger says row*columns+column is not checked.
- **Evidence:** external-source-reference
- **Recommendation:** Validate final computed index before indexing tiles.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-038: Settings validation mismatch around ObjectsPerTile
- **Priority:** P2
- **Finding:** Audit ledger says CanvasUserSettings.IsValid lacks upper bound while SampleImageGenerator throws for ObjectsPerTile > 256.
- **Evidence:** external-source-reference
- **Recommendation:** Use shared validation constants and tests.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-039: MinimumSparseTilePixelSize parameter is unused/dead
- **Priority:** P2
- **Finding:** Audit ledger states persisted/validated MinimumSparseTilePixelSize is not referenced, and DrawTile parameter unused.
- **Evidence:** external-source-reference
- **Recommendation:** Wire into DrawTile or remove setting/ticket.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-040: Benchmarks can measure non-production paths
- **Priority:** P2
- **Finding:** Audit says ProjectionAndBitmapBenchmarks uses legacy point overload with no production caller; ICW-133 still needs realistic shipped tile path.
- **Evidence:** external-source-reference
- **Recommendation:** Benchmark production GenerateFrozenBitmap overload with real tiles/annotations.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-041: Fast-scroll benchmarks are proposed, not completed
- **Priority:** P2
- **Finding:** ICW-P0 sequencing shows ICW-144 proposed for fast-scroll stress benchmarks.
- **Evidence:** external-source-reference
- **Recommendation:** Complete ICW-144 before performance claims.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-042: ApplyDetailsWithGdiPlus concurrency risk not reproduced
- **Priority:** P2
- **Finding:** Audit evidence says System.Drawing Bitmap/Graphics are constructed inside coordinator worker tasks with failure mode not runtime-reproduced.
- **Evidence:** external-source-reference
- **Recommendation:** Serialize GDI+ or remove from hot path; add concurrent factory stress.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-043: Async void error surface remains broad
- **Priority:** P2
- **Finding:** Audit shows async void handlers can crash app and dispatcher safety net was a finding; MainWindow still shows async void UI handlers.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Centralize async event wrapper with logging/status handling.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-044: OnTilePixelsGenerationFailed blindly re-renders and can retry-loop
- **Priority:** P2
- **Finding:** Failure handler triggers RequestRenderAsync so pipeline can retry; no backoff/terminal failure state shown.
- **Evidence:** external-source-reference
- **Recommendation:** Add per-key failure budget/backoff and diagnostics.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-045: Tile generation event subscriptions depend on explicit unsubscribe
- **Priority:** P2
- **Finding:** MainWindow subscribes tile events after regeneration and unsubscribes old tiles; shutdown/regeneration correctness depends on all paths reaching unsubscribe.
- **Evidence:** external-source-reference
- **Recommendation:** Move subscriptions into disposable SceneSnapshot.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-046: Feature panel still app-specific and sample-model-bound
- **Priority:** P2
- **Finding:** SelectedAnnotationFeatures comes from SampleAnnotation.GetFeatureDisplayItems and binds directly to FeatureDataGrid.
- **Evidence:** external-source-reference
- **Recommendation:** Move inspector payload to generic item metadata or production viewport host adapter.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-047: host application product goals lack acceptance mapping
- **Priority:** P2
- **Finding:** host application docs list speed, streaming, smooth scrolling, defect context, customizable queries; ICW tasks do not show a parity matrix.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Create acceptance matrix with fixtures.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-048: snapshot controller gap still unresolved for replacement scope
- **Priority:** P2
- **Finding:** Viewport ecosystem KB says snapshot controller is a gap outside the ecosystem.
- **Evidence:** external-source-reference
- **Recommendation:** Decide whether replacement includes snapshot controller and add adapter surface.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-049: Duplicate/stale tracker hygiene remains a risk
- **Priority:** P3
- **Finding:** Audit synthesis notes duplicate IDs/status divergence and legacy tracker validator errors.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Run tracker cleanup before using tickets as ground truth.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-050: Dead/unreferenced abstractions create false readiness signals
- **Priority:** P3
- **Finding:** Audit ledger lists IRenderer, IBackgroundTileSource, MipOptions, BackgroundTileDescriptor/Request/Payload only test-used/unreferenced.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Either wire into production path or remove/mark future scaffold.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-051: Frame diagnostics average is lifetime average, not windowed
- **Priority:** P3
- **Finding:** RenderFrameAsync accumulates _totalFrameTicks/_frameCount and logs every 120 frames.
- **Evidence:** external-source-reference
- **Recommendation:** Expose rolling p95/p99 and dropped-frame reasons for field diagnostics.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

### ICW-DEEP-052: Status text reports one zoom axis
- **Priority:** P3
- **Finding:** StatusText formats Zoom {camera.ScaleX:F3}x only.
- **Evidence:** external-source-reference
- **Recommendation:** Show X/Y scale or host application horizontal/vertical scale units.
- **Validation:** create a failing test, stress scenario, or parity check that demonstrates the exact failure shape; then verify the fix against the current supplied HEAD/commit.

## Recommended Next Bug Tickets
- [P0] ICW-DEEP-001: host application adapter gap - Current abstractions are generic canvas/source contracts; supplied evidence does not show production viewport host inspection, alignment layer, or overlay adapters.
- [P0] ICW-DEEP-002: 13-layer parity not implemented - production viewport layer stack has alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, labels; ICW path is generic raster/items and host overlays.
- [P0] ICW-DEEP-003: Synthetic source IDs still in production-like render path - RenderFrameAsync builds BackgroundTileCacheKey("synthetic", tile.Id, epoch, mipLevel).
- [P0] ICW-DEEP-004: Runtime concurrency not reproduced - Audit reconciliation says concurrency candidates were source-traced, but no runtime reproduction was run.
- [P1] ICW-DEEP-005: MainWindow still owns production host responsibilities - MainWindow owns spatial index, camera, FrameBufferPool, TileWorkCoordinator, cache budget, render request tracker, frame CTSs, scene state, and overlay composition.
- [P1] ICW-DEEP-006: Regeneration is not transactional - RegenerateSceneAsync mutates/swallows multiple state fields and then awaits source generation/publish; older audits identify missing partial-failure rollback.
- [P1] ICW-DEEP-007: Shutdown does not await generation gate - OnClosed cancels/disposes render/coordinator/pool/generation gate, but evidence from audit ledger flags no await on generation gate.
- [P1] ICW-DEEP-008: Frame buffer contract still relies on convention - FrameBufferPool composition fencing is present, but frame publication still hands InteropBitmap backed by memory section and then publishes/retires buffer.
- [P1] ICW-DEEP-009: Stale frame guard protects render order, not production viewport host source revision - CanvasFrame revision uses render request version; no evidence of production viewport host source revision vector in frame identity.
- [P1] ICW-DEEP-010: Canceled work can still dispatch completion - StartWorkItem records wasCanceled but always calls DispatchCompleted(pixels), relying on tile epoch checks.
- [P1] ICW-DEEP-011: Queued cancel path does not clear claimant registrations - Prior delta notes CancelWorkItem queued branch dispatches failed/removes item without clearing claimants/registrations; current TileWorkItem has registration storage/removal methods.
- [P1] ICW-DEEP-012: Reentrant coordinator/cache lock chain remains - TryReserve can call EvictCacheEntry, which calls RemoveClaimant while coordinator lock is held; source comments mark this safe only through same-thread reentrancy.
- [P1] ICW-DEEP-013: Cache accounting still has multi-mip risk - Ticket text states mip payloads can add ~33 percent more bytes and must be summed; current cache reserves per key.
- [P1] ICW-DEEP-014: Eviction can discard active/generating content - Audit synthesis tracks eviction selecting actively generating tile as ICW-104/305 follow-up.
- [P1] ICW-DEEP-015: No formal multi-viewport memory governance - Cache budget is per MainWindow instance; host application target can have multiple viewport surfaces.
- [P1] ICW-DEEP-016: Pixelometer works through scene source but remains SampleAnnotation/SampleImageTile-specific in host - TryReadResidentPixel uses _tiles geometry and QueryPoint, then casts hit items to SampleAnnotation for defect value.
- [P1] ICW-DEEP-017: Pixelometer tile-row math assumes _tiles.Count / _tileColumns - TryReadResidentPixel computes tileRows = Math.Max(1, _tiles.Count / _tileColumns), which is only safe for exact grid sizes.
- [P1] ICW-DEEP-018: QueryPoint still embeds fixed probe size - QueryPoint contains const double probeSize = 0.01.
- [P1] ICW-DEEP-019: Overlay rendering ignores generic ICanvasItem types - UpdateAnnotationLayer skips every item that is not SampleAnnotation.
- [P1] ICW-DEEP-020: Per-frame overlay element churn remains - UpdateAnnotationLayer clears AnnotationLayer and allocates Border/Grid/Rectangle/ToolTip/labels for each frame.
- [P1] ICW-DEEP-021: Tooltip ownership remains unfinished - ICW-314 remains proposed; current overlay creates DeferredAnnotationToolTip in host.
- [P1] ICW-DEEP-022: CanvasOverlayHost internals-visible-to app is a boundary smell - Wave H notes overlay host stays internal and app accesses it through InternalsVisibleTo.
- [P1] ICW-DEEP-023: Canvas control input abstraction remains deferred - ICW-313 notes CanvasControl implements pan, zoom, anchor pan, scrollbar logic directly and remains proposed/deferred.
- [P2] ICW-DEEP-024: CanvasViewModel.Zoom dead wrapper - Audit text says CanvasViewModel.Zoom has zero callers and wheel path reaches through Camera directly.
- [P2] ICW-DEEP-025: ComputeMinimumZoom lacks local zero guard - Audit synthesis confirms ComputeMinimumZoom divides by SceneBounds.Width/Height without local guard.

## Requests / Missing Evidence
- Exact current GitHub commit SHA for the supplied chunks.
- Ability to run the Windows test suite and WPF stress harness.
- production viewport host target branch and production viewport current viewport source.
- Representative inspection/alignment layer/layer-parity fixtures.


