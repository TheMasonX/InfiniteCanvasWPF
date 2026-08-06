# InfiniteCanvasWPF Deep Bug Sweep Delta 2

**Description:** Second additional deep pass. These findings intentionally focus on bugs and readiness gaps not fully expanded in the prior delta, with emphasis on lifecycle, cache/coordinator semantics, geometry, rendering hot paths, generic-control boundaries, and production viewport replacement risk.

**Timestamp:** 2026-08-06 09:54 CDT  
**Author:** Copilot  
**Status:** Additional source-backed/inferred findings for triage.  
**Confidence discipline:** Items citing direct source snippets are stronger; items citing audit-ledger text should be rechecked against exact HEAD before implementation.

## Executive Summary

This pass adds another large slice of findings. The repeated pattern is that the repo has stabilized several earlier WPF/frame/coordinator problems, but it still carries a lot of hidden production risk: mutable MainWindow-owned scene state, coordinator cancellation nuance, source-ID ambiguity, cache identity/pinning assumptions, sample model leakage, direct internal overlay access, and missing parity/runtime validation.

## Findings Table

| ID | Priority | Area | Finding | Recommendation | Evidence |
|---|---:|---|---|---|---|
| ICW-DEEP2-053 | P0 | Replacement blocker | The reusable boundary still lacks a host application-specific source/revision contract; current evidence shows generic ICanvasSceneSource and Presentation/CanvasFrame seams, not production viewport host inspection-source semantics. | Do not call this replacement-ready until source IDs, inspection IDs, selected view IDs, layer revisions, and source validity state are part of the frame/input model. | external-source-reference, external-source-reference |
| ICW-DEEP2-054 | P0 | Replacement blocker | production viewport layer parity remains unimplemented in the supplied evidence; current host overlay path updates only tile-grid and annotation layers. | Implement explicit layer registry/order before integrating into production viewport. | external-source-reference, external-source-reference |
| ICW-DEEP2-055 | P0 | Replacement blocker | The current product-readiness evidence is mostly unit/build/task evidence; the audit corpus explicitly says concurrency candidates were not runtime-reproduced. | Require WPF runtime stress plus real inspection fixture parity before product use. | external-source-reference |
| ICW-DEEP2-056 | P1 | Lifecycle | OnClosed is async void and awaits render disposal while continuing shutdown afterwards; any exception after await is dispatcher-surface rather than caller-surface. | Use an explicit shutdown coordinator and catch/log all shutdown exceptions. | external-source-reference |
| ICW-DEEP2-057 | P1 | Lifecycle | OnClosed disposes _generationGate after canceling lifetime but does not show waiting for an active RegenerateSceneAsync body to exit before disposing shared resources. | Add shutdown path that acquires/observes generation completion before disposing gate, coordinator, frame pool, or lifetime CTS. | external-source-reference |
| ICW-DEEP2-058 | P1 | Lifecycle | RegenerateSceneAsync clears the frame and resets camera before the new scene is generated, so a failed generation can leave the UI with no prior visible scene. | Build new scenes offscreen and only swap UI state after success. | external-source-reference |
| ICW-DEEP2-059 | P1 | Lifecycle | RegenerateSceneAsync calls InitializeSpatialState before generating tiles, so render/pixelometer paths may see a new empty spatial index while old or partial tile state still exists. | Bundle tiles, annotations, spatial index, bounds, and tile maps into one immutable SceneSnapshot. | external-source-reference |
| ICW-DEEP2-060 | P1 | Lifecycle | SceneChanged is raised only after RequestRenderAsync completes, coupling external scene notification to render execution. | Raise source-change notification from the committed scene swap, and make render request a subscriber effect. | external-source-reference |
| ICW-DEEP2-061 | P1 | Threading | Tile event handlers enqueue async Dispatcher work without observing or handling exceptions from RequestRenderAsync inside that dispatched delegate. | Wrap dispatched async delegates in a SafeFireAndForget/ErrorSink helper. | external-source-reference |
| ICW-DEEP2-062 | P1 | Threading | OnTilePixelsGenerationFailed intentionally triggers re-render retries, but no per-key failure suppression/backoff is visible. | Track failures by BackgroundTileCacheKey and surface terminal fault states instead of infinite retry churn. | external-source-reference |
| ICW-DEEP2-063 | P1 | Threading | TileWorkCoordinator.Dispose calls CancelAll before setting _disposed, then cancels/disposes _disposeCts; in-flight Task.Run bodies can still enter paths after disposal coordination starts. | Add explicit disposed transition before/with cancellation and test shutdown storm behavior. | external-source-reference |
| ICW-DEEP2-064 | P1 | Threading | PublishInterestSet documentation says queued or running work whose key is not in the interest set is canceled, but implementation comment says running items are NOT canceled. | Fix doc/comment mismatch and make the intended policy explicit in tests. | external-source-reference |
| ICW-DEEP2-065 | P1 | Threading | PublishInterestSet culls only queued items with ClaimantCount > 0, meaning unclaimed queued items outside interest are not canceled there and rely on later drain/rebuild behavior. | Prove this no-op is intentional, or cancel all non-interest queued items regardless of claimant count. | external-source-reference |
| ICW-DEEP2-066 | P1 | Threading | Running items outside interest are intentionally allowed to complete for cache warming, which can keep CPU busy during fast-scroll source churn. | Add a policy switch: cancel stale running work under high pressure, allow cache warming only below thresholds. | external-source-reference |
| ICW-DEEP2-067 | P1 | Threading | CancelWorkItem increments canceled count for running items immediately; OperationCanceledException path can also call HandleWorkStopped with canceled state if not already marked, making counter semantics hard to reason about. | Define counters as requested-cancel vs physically-canceled vs completed-after-cancel. | external-source-reference |
| ICW-DEEP2-068 | P1 | Threading | StartWorkItem always logs COMPLETE even when wasCanceled is true and completion dispatch still fires. | Separate COMPLETE, COMPLETED_AFTER_CANCEL, and CANCELED terminal logging. | external-source-reference |
| ICW-DEEP2-069 | P1 | Threading | Task.Run is started with _disposeCts.Token, but once started the delegate must rely on item.WorkToken; dispose token does not stop already-running delegate execution. | Do not overstate Task.Run token protection; add cooperative cancellation checks inside factories. | external-source-reference |
| ICW-DEEP2-070 | P1 | Threading | TileWorkItem.DispatchCompleted snapshots callbacks under claimant lock and then invokes outside, but queued-cancel path does not clear registered claimants first. | Add terminal cleanup that disposes claimant registrations for completion, failure, and cancellation. | external-source-reference |
| ICW-DEEP2-071 | P1 | Threading | TileWorkItem has a WorkToken CTS but the shown DispatchCompleted/DispatchFailed paths do not dispose the work CTS. | Add disposal ownership for work CTS at terminal state. | external-source-reference |
| ICW-DEEP2-072 | P1 | Cache | TileCacheBudget.TryReserve inserts the new entry and increments UsedBytes before eviction, so failed/no-evict cases can transiently overshoot budget under lock. | Accept if intentional but add observable invariant tests around UsedBytes after rejection and concurrent reads. | external-source-reference |
| ICW-DEEP2-073 | P1 | Cache | TileCacheBudget.SetPinnedTiles pins only by Tile.Id, not by SourceId/ContentRevision/MipLevel, which would be ambiguous once multiple sources or revisions exist. | Pin by full cache key or source-qualified tile identity. | external-source-reference |
| ICW-DEEP2-074 | P1 | Cache | TileCacheBudget eviction chooses candidates by Tile.Id pinning; same tile ID from another source would collide under host application adapter scenarios. | Use BackgroundTileCacheKey.SourceId plus TileId plus ContentRevision in residency/pinning. | external-source-reference |
| ICW-DEEP2-075 | P1 | Cache | TileCacheBudget.Release removes by key and subtracts the original entry byte cost, but SampleImageTile can hold several resident payloads over time. | Confirm reservations are per variant and do not under/over-release after mip fallback; add multi-mip release tests. | external-source-reference, external-source-reference |
| ICW-DEEP2-076 | P1 | Cache | EvictCacheEntry calls coordinator.RemoveClaimant while cache eviction is being processed, making eviction simultaneously a memory and scheduler operation. | Split eviction selection, scheduler notification, and memory release into staged phases. | external-source-reference |
| ICW-DEEP2-077 | P1 | Rendering | DrawTile has a minimumSparseTilePixelSize parameter in the signature, and audit evidence says the setting/parameter is unused. | Wire the setting or delete it; avoid product controls that do nothing. | external-source-reference, external-source-reference |
| ICW-DEEP2-078 | P1 | Rendering | GenerateFrozenBitmap returns an InteropBitmap that explicitly references the factory memory section; frame lifetime therefore depends on buffer pool discipline. | Add tests that copy/check retired surfaces under rapid publish and disposal. | external-source-reference |
| ICW-DEEP2-079 | P1 | Rendering | RenderFrameAsync clamps rendered viewport size to 4096x4096, which may silently downsize very large/high-DPI host application windows. | Make max surface policy explicit and visible to hosts; test high-DPI/multi-monitor. | external-source-reference |
| ICW-DEEP2-080 | P1 | Rendering | UpdateAnnotationLayer creates SolidColorBrush objects per annotation per frame and freezes neither outlineBrush nor fillBrush in the shown path. | Cache/freeze brushes by class/color/options or move to retained drawing. | external-source-reference |
| ICW-DEEP2-081 | P1 | Rendering | BuildAnnotationLabel uses a fixed vertical offset of topLeft.Y - 22. | Make label layout collision-aware and viewport-clipped. | external-source-reference |
| ICW-DEEP2-082 | P1 | Rendering | UpdateAnnotationLayer uses width/height from annotation bounds times camera scale but does not clamp offscreen annotation visuals before creating WPF elements. | Clip or virtualize annotation visuals before element allocation. | external-source-reference |
| ICW-DEEP2-083 | P1 | Rendering | Selection outline animation is applied to a per-frame newly-created Rectangle; animation continuity and cleanup depend on visual replacement behavior. |  Move selection outline into retained adorners or explicit animation lifecycle. | external-source-reference |
| ICW-DEEP2-084 | P2 | Pixelometer | UpdatePixelometer computes world coordinates directly from camera Offset/Scale instead of going through a shared ScreenToWorld conversion API. | Add one coordinate-transform API and use it across render, hit-test, and pixelometer. | external-source-reference |
| ICW-DEEP2-085 | P2 | Pixelometer | TryReadResidentPixel and ResolveDisplayPixelValue can use separate annotation queries/precedence paths according to audit evidence, creating readout disagreement risk. | Compute hit annotations once and derive displayed values from one explicit precedence policy. | external-source-reference |
| ICW-DEEP2-086 | P2 | Pixelometer | Pixelometer uses current camera when hover remains set after a render; this updates readout after frames and can show values for camera not matching the last presented frame. | Tie pixelometer readout to last published frame snapshot or label as live-camera value. | external-source-reference |
| ICW-DEEP2-087 | P2 | Pixelometer | Hover readout is updated on every pointer move without visible throttling. | Throttle/debounce pixelometer sampling to frame cadence. | external-source-reference |
| ICW-DEEP2-088 | P2 | Geometry | TileGridIndexLookup uses half-open right/bottom checks while SpatialBounds.Intersects has documented closed/half-open mismatch in audit corpus. | Set a canonical boundary policy and apply it across geometry, selection, rendering, cache, and pixelometer. | external-source-reference, external-source-reference |
| ICW-DEEP2-089 | P2 | Geometry | GetSceneBounds assumes tiles is non-empty and calls Min/Max directly. | Guard empty scenes or enforce non-empty before this helper. | external-source-reference |
| ICW-DEEP2-090 | P2 | Geometry | Zero-size SpatialBounds are permitted by the type according to audit evidence, but DrawTile divides by tile.Bounds.Width/Height. | Reject zero dimensions for tile bounds or guard DrawTile. | external-source-reference |
| ICW-DEEP2-091 | P2 | Geometry | Bgra32BufferLayout documents OverflowException for very large dimensions even though the codebase has MaxWidth/GetMaxHeightForWidth helper methods. | Use explicit validation at factory entry so callers get ArgumentOutOfRangeException. | external-source-reference |
| ICW-DEEP2-092 | P2 | Settings | SaveSettings persists UI values first, then falls back only some fields if IsValid is false. | Use central validation and fail/save-previous atomically. | external-source-reference |
| ICW-DEEP2-093 | P2 | Settings | TryReadGenerationOptions validates tile count but relies on SliderTextBox clamping for per-field ranges. | Validate cross-field and per-field values in one shared service, not only UI control behavior. | external-source-reference |
| ICW-DEEP2-094 | P2 | Settings | Custom zoom parsing uses current culture by default, which can make percent input behavior locale-dependent. | Use explicit CultureInfo or clearly accept current-culture input and test it. | external-source-reference |
| ICW-DEEP2-095 | P2 | Settings | ZoomPresetComboBox.SelectedIndex is set to -1 inside SelectionChanged handler. | Add guard/reentrancy test to ensure the programmatic reset does not trigger unexpected second path. | external-source-reference |
| ICW-DEEP2-096 | P2 | Settings | RasterVisible is driven by _showBackgroundImages || _showImageTiles, so hiding backgrounds and sparse image tiles may still leave overlay layers active without a full layer visibility model. | Promote layer visibility to a single frame/layer settings snapshot. | external-source-reference |
| ICW-DEEP2-097 | P2 | Architecture | CanvasOverlayHost access through InternalsVisibleTo means the host still reaches internal visual structure. | Replace internal visual access with stable overlay APIs or layer presenter interfaces. | external-source-reference |
| ICW-DEEP2-098 | P2 | Architecture | IRenderer and background tile source abstractions are reported by audit evidence as unreferenced/test-only, creating false confidence in production abstraction readiness. | Remove or wire them to production paths before presenting them as architecture. | external-source-reference |
| ICW-DEEP2-099 | P2 | Architecture | BackgroundTileDescriptor/Request/Payload encode useful source-backed model but audit evidence says they are only test-used or not production-wired. | Use these contracts in the rendering path instead of SampleImageTile-specific APIs. | external-source-reference, external-source-reference |
| ICW-DEEP2-100 | P2 | Architecture | SampleAnnotation stores Features as Lazy<IReadOnlyDictionary<string, object>> and separate typed display presenters exist, but feature panel still binds sample data directly. | Move to typed annotation metrics or adapter payload before host application integration. | external-source-reference, external-source-reference |
| ICW-DEEP2-101 | P2 | Architecture | SampleImageTile exposes mutable Coordinator, ClaimantIdProvider, ClaimantTokenProvider, and ReleaseReservedCacheEntry properties. | Prefer constructor-injected immutable services or per-scene immutable tile descriptors with external scheduler. | external-source-reference |
| ICW-DEEP2-102 | P2 | Architecture | SampleImageTile is both source descriptor, cache holder, generation state machine, event source, and UI demo model. | Split descriptor, cache entry, generation job, and demo metadata. | external-source-reference |
| ICW-DEEP2-103 | P2 | Architecture | MainWindow implements ICanvasSceneSource directly, tying reusable source contract to demo app shell. | Create a separate AppSceneSource implementation and let MainWindow only compose services. | external-source-reference |
| ICW-DEEP2-104 | P2 | Architecture | CanvasFrame carries ICanvasItem list but host still composes overlays after FramePublished; ownership of frame visual completeness is split. | Decide whether CanvasFrame owns overlays or host owns overlays; avoid partially-owned frame meaning. | external-source-reference |
| ICW-DEEP2-105 | P2 | Architecture | FramePublished event causes host overlay update after CanvasSurface.PublishFrame; if publish is rejected as stale in control, host may still need to know no overlay update should occur. | Ensure FramePublished fires only for accepted frames and test stale rejection with overlays. | external-source-reference, external-source-reference |
| ICW-DEEP2-106 | P2 | Testing | Current fast-scroll benchmark evidence exists in task notes, but separate reconciliation says concurrency candidates were not runtime reproduced. | Do not rely on benchmark-only evidence for correctness; add WPF integration/stress tests. | external-source-reference, external-source-reference |
| ICW-DEEP2-107 | P2 | Testing | Tests show many passing counts in active tasks, but some audit text notes no post-change runtime profile for tooltip change. | Add post-change profiling/trace captures for hot UI paths. | external-source-reference |
| ICW-DEEP2-108 | P2 | Testing | Benchmark suite has known non-production-path benchmark issue. | Use real GenerateFrozenBitmap tile/annotation overload in performance gates. | external-source-reference |
| ICW-DEEP2-109 | P3 | Diagnostics | StatusText mixes frame size, elapsed, zoom, backgrounds, queue, generation, and coordinator counts in one UI string. | Expose structured diagnostics object and UI template; keep logs machine-readable. | external-source-reference |
| ICW-DEEP2-110 | P3 | Diagnostics | FrameDiag logs every 120 frames but only average ms and aggregate counters, not p95/p99 or dropped frame causes. | Add rolling percentiles and reasons: stale-discard, budget-reject, tile-fail, source-lost, buffer-unavailable. | external-source-reference |
| ICW-DEEP2-111 | P3 | Diagnostics | Tile work priority does not expose why a tile was ordered lower beyond rank/distance/mip/sequence. | Add optional debug trace for priority components when diagnosing slow fill. | external-source-reference |
| ICW-DEEP2-112 | P3 | Diagnostics | CacheStatusText is driven by DescribeStatus string instead of a typed diagnostic snapshot. | Expose cache diagnostics as structured properties. | external-source-reference |
| ICW-DEEP2-113 | P3 | Diagnostics | Saved settings failures are logged but not surfaced to user. | Set a non-blocking UI warning if settings persistence fails. | external-source-reference |
| ICW-DEEP2-114 | P3 | Process | Tracker evidence has had duplicate IDs/status divergence; relying on active-tasks alone can overstate readiness. | Treat tracker as secondary and verify source for each claim. | external-source-reference, external-source-reference |

## Detailed Findings

### ICW-DEEP2-053: Replacement blocker

- **Priority:** P0
- **Finding:** The reusable boundary still lacks a host application-specific source/revision contract; current evidence shows generic ICanvasSceneSource and Presentation/CanvasFrame seams, not production viewport host inspection-source semantics.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Do not call this replacement-ready until source IDs, inspection IDs, selected view IDs, layer revisions, and source validity state are part of the frame/input model.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-054: Replacement blocker

- **Priority:** P0
- **Finding:** production viewport layer parity remains unimplemented in the supplied evidence; current host overlay path updates only tile-grid and annotation layers.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Implement explicit layer registry/order before integrating into production viewport.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-055: Replacement blocker

- **Priority:** P0
- **Finding:** The current product-readiness evidence is mostly unit/build/task evidence; the audit corpus explicitly says concurrency candidates were not runtime-reproduced.
- **Evidence:** external-source-reference
- **Recommendation:** Require WPF runtime stress plus real inspection fixture parity before product use.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-056: Lifecycle

- **Priority:** P1
- **Finding:** OnClosed is async void and awaits render disposal while continuing shutdown afterwards; any exception after await is dispatcher-surface rather than caller-surface.
- **Evidence:** external-source-reference
- **Recommendation:** Use an explicit shutdown coordinator and catch/log all shutdown exceptions.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-057: Lifecycle

- **Priority:** P1
- **Finding:** OnClosed disposes _generationGate after canceling lifetime but does not show waiting for an active RegenerateSceneAsync body to exit before disposing shared resources.
- **Evidence:** external-source-reference
- **Recommendation:** Add shutdown path that acquires/observes generation completion before disposing gate, coordinator, frame pool, or lifetime CTS.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-058: Lifecycle

- **Priority:** P1
- **Finding:** RegenerateSceneAsync clears the frame and resets camera before the new scene is generated, so a failed generation can leave the UI with no prior visible scene.
- **Evidence:** external-source-reference
- **Recommendation:** Build new scenes offscreen and only swap UI state after success.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-059: Lifecycle

- **Priority:** P1
- **Finding:** RegenerateSceneAsync calls InitializeSpatialState before generating tiles, so render/pixelometer paths may see a new empty spatial index while old or partial tile state still exists.
- **Evidence:** external-source-reference
- **Recommendation:** Bundle tiles, annotations, spatial index, bounds, and tile maps into one immutable SceneSnapshot.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-060: Lifecycle

- **Priority:** P1
- **Finding:** SceneChanged is raised only after RequestRenderAsync completes, coupling external scene notification to render execution.
- **Evidence:** external-source-reference
- **Recommendation:** Raise source-change notification from the committed scene swap, and make render request a subscriber effect.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-061: Threading

- **Priority:** P1
- **Finding:** Tile event handlers enqueue async Dispatcher work without observing or handling exceptions from RequestRenderAsync inside that dispatched delegate.
- **Evidence:** external-source-reference
- **Recommendation:** Wrap dispatched async delegates in a SafeFireAndForget/ErrorSink helper.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-062: Threading

- **Priority:** P1
- **Finding:** OnTilePixelsGenerationFailed intentionally triggers re-render retries, but no per-key failure suppression/backoff is visible.
- **Evidence:** external-source-reference
- **Recommendation:** Track failures by BackgroundTileCacheKey and surface terminal fault states instead of infinite retry churn.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-063: Threading

- **Priority:** P1
- **Finding:** TileWorkCoordinator.Dispose calls CancelAll before setting _disposed, then cancels/disposes _disposeCts; in-flight Task.Run bodies can still enter paths after disposal coordination starts.
- **Evidence:** external-source-reference
- **Recommendation:** Add explicit disposed transition before/with cancellation and test shutdown storm behavior.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-064: Threading

- **Priority:** P1
- **Finding:** PublishInterestSet documentation says queued or running work whose key is not in the interest set is canceled, but implementation comment says running items are NOT canceled.
- **Evidence:** external-source-reference
- **Recommendation:** Fix doc/comment mismatch and make the intended policy explicit in tests.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-065: Threading

- **Priority:** P1
- **Finding:** PublishInterestSet culls only queued items with ClaimantCount > 0, meaning unclaimed queued items outside interest are not canceled there and rely on later drain/rebuild behavior.
- **Evidence:** external-source-reference
- **Recommendation:** Prove this no-op is intentional, or cancel all non-interest queued items regardless of claimant count.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-066: Threading

- **Priority:** P1
- **Finding:** Running items outside interest are intentionally allowed to complete for cache warming, which can keep CPU busy during fast-scroll source churn.
- **Evidence:** external-source-reference
- **Recommendation:** Add a policy switch: cancel stale running work under high pressure, allow cache warming only below thresholds.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-067: Threading

- **Priority:** P1
- **Finding:** CancelWorkItem increments canceled count for running items immediately; OperationCanceledException path can also call HandleWorkStopped with canceled state if not already marked, making counter semantics hard to reason about.
- **Evidence:** external-source-reference
- **Recommendation:** Define counters as requested-cancel vs physically-canceled vs completed-after-cancel.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-068: Threading

- **Priority:** P1
- **Finding:** StartWorkItem always logs COMPLETE even when wasCanceled is true and completion dispatch still fires.
- **Evidence:** external-source-reference
- **Recommendation:** Separate COMPLETE, COMPLETED_AFTER_CANCEL, and CANCELED terminal logging.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-069: Threading

- **Priority:** P1
- **Finding:** Task.Run is started with _disposeCts.Token, but once started the delegate must rely on item.WorkToken; dispose token does not stop already-running delegate execution.
- **Evidence:** external-source-reference
- **Recommendation:** Do not overstate Task.Run token protection; add cooperative cancellation checks inside factories.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-070: Threading

- **Priority:** P1
- **Finding:** TileWorkItem.DispatchCompleted snapshots callbacks under claimant lock and then invokes outside, but queued-cancel path does not clear registered claimants first.
- **Evidence:** external-source-reference
- **Recommendation:** Add terminal cleanup that disposes claimant registrations for completion, failure, and cancellation.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-071: Threading

- **Priority:** P1
- **Finding:** TileWorkItem has a WorkToken CTS but the shown DispatchCompleted/DispatchFailed paths do not dispose the work CTS.
- **Evidence:** external-source-reference
- **Recommendation:** Add disposal ownership for work CTS at terminal state.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-072: Cache

- **Priority:** P1
- **Finding:** TileCacheBudget.TryReserve inserts the new entry and increments UsedBytes before eviction, so failed/no-evict cases can transiently overshoot budget under lock.
- **Evidence:** external-source-reference
- **Recommendation:** Accept if intentional but add observable invariant tests around UsedBytes after rejection and concurrent reads.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-073: Cache

- **Priority:** P1
- **Finding:** TileCacheBudget.SetPinnedTiles pins only by Tile.Id, not by SourceId/ContentRevision/MipLevel, which would be ambiguous once multiple sources or revisions exist.
- **Evidence:** external-source-reference
- **Recommendation:** Pin by full cache key or source-qualified tile identity.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-074: Cache

- **Priority:** P1
- **Finding:** TileCacheBudget eviction chooses candidates by Tile.Id pinning; same tile ID from another source would collide under host application adapter scenarios.
- **Evidence:** external-source-reference
- **Recommendation:** Use BackgroundTileCacheKey.SourceId plus TileId plus ContentRevision in residency/pinning.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-075: Cache

- **Priority:** P1
- **Finding:** TileCacheBudget.Release removes by key and subtracts the original entry byte cost, but SampleImageTile can hold several resident payloads over time.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Confirm reservations are per variant and do not under/over-release after mip fallback; add multi-mip release tests.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-076: Cache

- **Priority:** P1
- **Finding:** EvictCacheEntry calls coordinator.RemoveClaimant while cache eviction is being processed, making eviction simultaneously a memory and scheduler operation.
- **Evidence:** external-source-reference
- **Recommendation:** Split eviction selection, scheduler notification, and memory release into staged phases.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-077: Rendering

- **Priority:** P1
- **Finding:** DrawTile has a minimumSparseTilePixelSize parameter in the signature, and audit evidence says the setting/parameter is unused.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Wire the setting or delete it; avoid product controls that do nothing.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-078: Rendering

- **Priority:** P1
- **Finding:** GenerateFrozenBitmap returns an InteropBitmap that explicitly references the factory memory section; frame lifetime therefore depends on buffer pool discipline.
- **Evidence:** external-source-reference
- **Recommendation:** Add tests that copy/check retired surfaces under rapid publish and disposal.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-079: Rendering

- **Priority:** P1
- **Finding:** RenderFrameAsync clamps rendered viewport size to 4096x4096, which may silently downsize very large/high-DPI host application windows.
- **Evidence:** external-source-reference
- **Recommendation:** Make max surface policy explicit and visible to hosts; test high-DPI/multi-monitor.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-080: Rendering

- **Priority:** P1
- **Finding:** UpdateAnnotationLayer creates SolidColorBrush objects per annotation per frame and freezes neither outlineBrush nor fillBrush in the shown path.
- **Evidence:** external-source-reference
- **Recommendation:** Cache/freeze brushes by class/color/options or move to retained drawing.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-081: Rendering

- **Priority:** P1
- **Finding:** BuildAnnotationLabel uses a fixed vertical offset of topLeft.Y - 22.
- **Evidence:** external-source-reference
- **Recommendation:** Make label layout collision-aware and viewport-clipped.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-082: Rendering

- **Priority:** P1
- **Finding:** UpdateAnnotationLayer uses width/height from annotation bounds times camera scale but does not clamp offscreen annotation visuals before creating WPF elements.
- **Evidence:** external-source-reference
- **Recommendation:** Clip or virtualize annotation visuals before element allocation.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-083: Rendering

- **Priority:** P1
- **Finding:** Selection outline animation is applied to a per-frame newly-created Rectangle; animation continuity and cleanup depend on visual replacement behavior.
- **Evidence:** external-source-reference
- **Recommendation:**  Move selection outline into retained adorners or explicit animation lifecycle.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-084: Pixelometer

- **Priority:** P2
- **Finding:** UpdatePixelometer computes world coordinates directly from camera Offset/Scale instead of going through a shared ScreenToWorld conversion API.
- **Evidence:** external-source-reference
- **Recommendation:** Add one coordinate-transform API and use it across render, hit-test, and pixelometer.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-085: Pixelometer

- **Priority:** P2
- **Finding:** TryReadResidentPixel and ResolveDisplayPixelValue can use separate annotation queries/precedence paths according to audit evidence, creating readout disagreement risk.
- **Evidence:** external-source-reference
- **Recommendation:** Compute hit annotations once and derive displayed values from one explicit precedence policy.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-086: Pixelometer

- **Priority:** P2
- **Finding:** Pixelometer uses current camera when hover remains set after a render; this updates readout after frames and can show values for camera not matching the last presented frame.
- **Evidence:** external-source-reference
- **Recommendation:** Tie pixelometer readout to last published frame snapshot or label as live-camera value.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-087: Pixelometer

- **Priority:** P2
- **Finding:** Hover readout is updated on every pointer move without visible throttling.
- **Evidence:** external-source-reference
- **Recommendation:** Throttle/debounce pixelometer sampling to frame cadence.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-088: Geometry

- **Priority:** P2
- **Finding:** TileGridIndexLookup uses half-open right/bottom checks while SpatialBounds.Intersects has documented closed/half-open mismatch in audit corpus.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Set a canonical boundary policy and apply it across geometry, selection, rendering, cache, and pixelometer.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-089: Geometry

- **Priority:** P2
- **Finding:** GetSceneBounds assumes tiles is non-empty and calls Min/Max directly.
- **Evidence:** external-source-reference
- **Recommendation:** Guard empty scenes or enforce non-empty before this helper.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-090: Geometry

- **Priority:** P2
- **Finding:** Zero-size SpatialBounds are permitted by the type according to audit evidence, but DrawTile divides by tile.Bounds.Width/Height.
- **Evidence:** external-source-reference
- **Recommendation:** Reject zero dimensions for tile bounds or guard DrawTile.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-091: Geometry

- **Priority:** P2
- **Finding:** Bgra32BufferLayout documents OverflowException for very large dimensions even though the codebase has MaxWidth/GetMaxHeightForWidth helper methods.
- **Evidence:** external-source-reference
- **Recommendation:** Use explicit validation at factory entry so callers get ArgumentOutOfRangeException.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-092: Settings

- **Priority:** P2
- **Finding:** SaveSettings persists UI values first, then falls back only some fields if IsValid is false.
- **Evidence:** external-source-reference
- **Recommendation:** Use central validation and fail/save-previous atomically.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-093: Settings

- **Priority:** P2
- **Finding:** TryReadGenerationOptions validates tile count but relies on SliderTextBox clamping for per-field ranges.
- **Evidence:** external-source-reference
- **Recommendation:** Validate cross-field and per-field values in one shared service, not only UI control behavior.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-094: Settings

- **Priority:** P2
- **Finding:** Custom zoom parsing uses current culture by default, which can make percent input behavior locale-dependent.
- **Evidence:** external-source-reference
- **Recommendation:** Use explicit CultureInfo or clearly accept current-culture input and test it.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-095: Settings

- **Priority:** P2
- **Finding:** ZoomPresetComboBox.SelectedIndex is set to -1 inside SelectionChanged handler.
- **Evidence:** external-source-reference
- **Recommendation:** Add guard/reentrancy test to ensure the programmatic reset does not trigger unexpected second path.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-096: Settings

- **Priority:** P2
- **Finding:** RasterVisible is driven by _showBackgroundImages || _showImageTiles, so hiding backgrounds and sparse image tiles may still leave overlay layers active without a full layer visibility model.
- **Evidence:** external-source-reference
- **Recommendation:** Promote layer visibility to a single frame/layer settings snapshot.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-097: Architecture

- **Priority:** P2
- **Finding:** CanvasOverlayHost access through InternalsVisibleTo means the host still reaches internal visual structure.
- **Evidence:** external-source-reference
- **Recommendation:** Replace internal visual access with stable overlay APIs or layer presenter interfaces.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-098: Architecture

- **Priority:** P2
- **Finding:** IRenderer and background tile source abstractions are reported by audit evidence as unreferenced/test-only, creating false confidence in production abstraction readiness.
- **Evidence:** external-source-reference
- **Recommendation:** Remove or wire them to production paths before presenting them as architecture.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-099: Architecture

- **Priority:** P2
- **Finding:** BackgroundTileDescriptor/Request/Payload encode useful source-backed model but audit evidence says they are only test-used or not production-wired.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Use these contracts in the rendering path instead of SampleImageTile-specific APIs.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-100: Architecture

- **Priority:** P2
- **Finding:** SampleAnnotation stores Features as Lazy<IReadOnlyDictionary<string, object>> and separate typed display presenters exist, but feature panel still binds sample data directly.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Move to typed annotation metrics or adapter payload before host application integration.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-101: Architecture

- **Priority:** P2
- **Finding:** SampleImageTile exposes mutable Coordinator, ClaimantIdProvider, ClaimantTokenProvider, and ReleaseReservedCacheEntry properties.
- **Evidence:** external-source-reference
- **Recommendation:** Prefer constructor-injected immutable services or per-scene immutable tile descriptors with external scheduler.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-102: Architecture

- **Priority:** P2
- **Finding:** SampleImageTile is both source descriptor, cache holder, generation state machine, event source, and UI demo model.
- **Evidence:** external-source-reference
- **Recommendation:** Split descriptor, cache entry, generation job, and demo metadata.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-103: Architecture

- **Priority:** P2
- **Finding:** MainWindow implements ICanvasSceneSource directly, tying reusable source contract to demo app shell.
- **Evidence:** external-source-reference
- **Recommendation:** Create a separate AppSceneSource implementation and let MainWindow only compose services.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-104: Architecture

- **Priority:** P2
- **Finding:** CanvasFrame carries ICanvasItem list but host still composes overlays after FramePublished; ownership of frame visual completeness is split.
- **Evidence:** external-source-reference
- **Recommendation:** Decide whether CanvasFrame owns overlays or host owns overlays; avoid partially-owned frame meaning.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-105: Architecture

- **Priority:** P2
- **Finding:** FramePublished event causes host overlay update after CanvasSurface.PublishFrame; if publish is rejected as stale in control, host may still need to know no overlay update should occur.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Ensure FramePublished fires only for accepted frames and test stale rejection with overlays.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-106: Testing

- **Priority:** P2
- **Finding:** Current fast-scroll benchmark evidence exists in task notes, but separate reconciliation says concurrency candidates were not runtime reproduced.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Do not rely on benchmark-only evidence for correctness; add WPF integration/stress tests.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-107: Testing

- **Priority:** P2
- **Finding:** Tests show many passing counts in active tasks, but some audit text notes no post-change runtime profile for tooltip change.
- **Evidence:** external-source-reference
- **Recommendation:** Add post-change profiling/trace captures for hot UI paths.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-108: Testing

- **Priority:** P2
- **Finding:** Benchmark suite has known non-production-path benchmark issue.
- **Evidence:** external-source-reference
- **Recommendation:** Use real GenerateFrozenBitmap tile/annotation overload in performance gates.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-109: Diagnostics

- **Priority:** P3
- **Finding:** StatusText mixes frame size, elapsed, zoom, backgrounds, queue, generation, and coordinator counts in one UI string.
- **Evidence:** external-source-reference
- **Recommendation:** Expose structured diagnostics object and UI template; keep logs machine-readable.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-110: Diagnostics

- **Priority:** P3
- **Finding:** FrameDiag logs every 120 frames but only average ms and aggregate counters, not p95/p99 or dropped frame causes.
- **Evidence:** external-source-reference
- **Recommendation:** Add rolling percentiles and reasons: stale-discard, budget-reject, tile-fail, source-lost, buffer-unavailable.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-111: Diagnostics

- **Priority:** P3
- **Finding:** Tile work priority does not expose why a tile was ordered lower beyond rank/distance/mip/sequence.
- **Evidence:** external-source-reference
- **Recommendation:** Add optional debug trace for priority components when diagnosing slow fill.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-112: Diagnostics

- **Priority:** P3
- **Finding:** CacheStatusText is driven by DescribeStatus string instead of a typed diagnostic snapshot.
- **Evidence:** external-source-reference
- **Recommendation:** Expose cache diagnostics as structured properties.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-113: Diagnostics

- **Priority:** P3
- **Finding:** Saved settings failures are logged but not surfaced to user.
- **Evidence:** external-source-reference
- **Recommendation:** Set a non-blocking UI warning if settings persistence fails.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

### ICW-DEEP2-114: Process

- **Priority:** P3
- **Finding:** Tracker evidence has had duplicate IDs/status divergence; relying on active-tasks alone can overstate readiness.
- **Evidence:** external-source-reference, external-source-reference
- **Recommendation:** Treat tracker as secondary and verify source for each claim.
- **Validation:** add a focused failing regression test or integration/stress case; then verify against the exact current GitHub commit and supplied chunks.

## Highest-ROI Next Triage Cluster

- **ICW-DEEP2-053 (P0)**: The reusable boundary still lacks a host application-specific source/revision contract; current evidence shows generic ICanvasSceneSource and Presentation/CanvasFrame seams, not production viewport host inspection-source semantics.
- **ICW-DEEP2-054 (P0)**: production viewport layer parity remains unimplemented in the supplied evidence; current host overlay path updates only tile-grid and annotation layers.
- **ICW-DEEP2-055 (P0)**: The current product-readiness evidence is mostly unit/build/task evidence; the audit corpus explicitly says concurrency candidates were not runtime-reproduced.
- **ICW-DEEP2-056 (P1)**: OnClosed is async void and awaits render disposal while continuing shutdown afterwards; any exception after await is dispatcher-surface rather than caller-surface.
- **ICW-DEEP2-057 (P1)**: OnClosed disposes _generationGate after canceling lifetime but does not show waiting for an active RegenerateSceneAsync body to exit before disposing shared resources.
- **ICW-DEEP2-058 (P1)**: RegenerateSceneAsync clears the frame and resets camera before the new scene is generated, so a failed generation can leave the UI with no prior visible scene.
- **ICW-DEEP2-059 (P1)**: RegenerateSceneAsync calls InitializeSpatialState before generating tiles, so render/pixelometer paths may see a new empty spatial index while old or partial tile state still exists.
- **ICW-DEEP2-060 (P1)**: SceneChanged is raised only after RequestRenderAsync completes, coupling external scene notification to render execution.
- **ICW-DEEP2-061 (P1)**: Tile event handlers enqueue async Dispatcher work without observing or handling exceptions from RequestRenderAsync inside that dispatched delegate.
- **ICW-DEEP2-062 (P1)**: OnTilePixelsGenerationFailed intentionally triggers re-render retries, but no per-key failure suppression/backoff is visible.
- **ICW-DEEP2-063 (P1)**: TileWorkCoordinator.Dispose calls CancelAll before setting _disposed, then cancels/disposes _disposeCts; in-flight Task.Run bodies can still enter paths after disposal coordination starts.
- **ICW-DEEP2-064 (P1)**: PublishInterestSet documentation says queued or running work whose key is not in the interest set is canceled, but implementation comment says running items are NOT canceled.
- **ICW-DEEP2-065 (P1)**: PublishInterestSet culls only queued items with ClaimantCount > 0, meaning unclaimed queued items outside interest are not canceled there and rely on later drain/rebuild behavior.
- **ICW-DEEP2-066 (P1)**: Running items outside interest are intentionally allowed to complete for cache warming, which can keep CPU busy during fast-scroll source churn.
- **ICW-DEEP2-067 (P1)**: CancelWorkItem increments canceled count for running items immediately; OperationCanceledException path can also call HandleWorkStopped with canceled state if not already marked, making counter semantics hard to reason about.
- **ICW-DEEP2-068 (P1)**: StartWorkItem always logs COMPLETE even when wasCanceled is true and completion dispatch still fires.
- **ICW-DEEP2-069 (P1)**: Task.Run is started with _disposeCts.Token, but once started the delegate must rely on item.WorkToken; dispose token does not stop already-running delegate execution.
- **ICW-DEEP2-070 (P1)**: TileWorkItem.DispatchCompleted snapshots callbacks under claimant lock and then invokes outside, but queued-cancel path does not clear registered claimants first.
- **ICW-DEEP2-071 (P1)**: TileWorkItem has a WorkToken CTS but the shown DispatchCompleted/DispatchFailed paths do not dispose the work CTS.
- **ICW-DEEP2-072 (P1)**: TileCacheBudget.TryReserve inserts the new entry and increments UsedBytes before eviction, so failed/no-evict cases can transiently overshoot budget under lock.

## Notes

This is a delta report, not a replacement for the master report. It should be merged into the master audit using the existing source-backed / inferred / speculative separation. Do not promote audit-ledger-only items to source-backed master findings until the exact current source is re-read.


