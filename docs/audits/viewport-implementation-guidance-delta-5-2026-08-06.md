# InfiniteCanvasWPF Agent Implementation Guidance Delta 5

**Description:** Further implementation-focused audit to guide the next agent on missing interfaces/classes and requirements-fit gaps, using only secret-safe neutral names.
**Timestamp:** 2026-08-06 12:06 CDT  
**Author:** Copilot  
**Status:** Additional findings and class/interface guidance.  
**Secret posture:** No credentials, customer-private data, internal URLs, or proprietary adapter names. All proposed names are neutral and suitable for a public/prototype layer.

## Executive Summary

This pass adds another **40 implementation-guidance findings**. The recurring gap is that the current code has a useful reusable canvas boundary, but the next agent needs to implement a neutral viewport contract layer before a production replacement can be responsibly evaluated. The specific seams are: source/revision identity, layer ordering, source-qualified cache keys, frame/overlay atomicity, selection/tooltip/pixelometer contracts, render invalidation reasons, frame-surface leasing, diagnostics, and runtime stress evidence.

## Findings and Agent Actions

| ID | Priority | Finding | Agent action | Evidence |
|---|---:|---|---|---|
| ICW-REQ5-041 | P0 | Introduce a source-neutral viewport contract package: Current `ICanvasSceneSource` is intentionally generic, but visible members are not enough to express a production viewport source with source health, revision vectors, layer order, and tile source ownership. | Create `InfiniteCanvas.Contracts` or `InfiniteCanvas.Core.Viewport` with `IViewportSession`, `IViewportSource`, `ViewportSnapshot`, `ViewportRevisionVector`, and `ViewportSourceHealth`. | external-source-reference |
| ICW-REQ5-042 | P0 | Split demo scene from reusable source adapter: `MainWindow` implements `ICanvasSceneSource` directly and uses `LiveSpatialIndexService<SampleAnnotation>`. | Move demo implementation to `DemoSceneSource : IViewportSource`; keep `MainWindow` as composition root only. | external-source-reference |
| ICW-REQ5-043 | P0 | Replace simple `CanvasFrame.Revision` with semantic frame identity: `CanvasFrame` is created with the render request version, and comments say the canvas discards frames older than the last displayed frame. | Add `ViewportFrameIdentity` containing render sequence, source revision vector, layer revision vector, display settings revision, and selection revision. | external-source-reference |
| ICW-REQ5-044 | P0 | Make raster frame and interactive visible frame distinct: A `CanvasFrame` with raster/items is published, then overlays are updated via `OnCanvasFramePublished`. | Introduce `RasterFrame` and `ViewportFrame` or extend current frame so overlay plan is part of the accepted frame. | external-source-reference |
| ICW-REQ5-045 | P0 | Replace hard-coded synthetic source identity: `RenderFrameAsync` and tile generation create `BackgroundTileCacheKey("synthetic", ...)`. | Create `ViewportTileKey` and `ITileKeyFactory`; prohibit string literals for source identity in production path. | external-source-reference |
| ICW-REQ5-046 | P0 | Layer plan must be deterministic and testable: Overlay update currently has explicit tile-grid and annotation methods, not a general layer plan. | Add `ViewportLayerRegistry`, `LayerOrder`, `LayerRenderPlan`, and contract tests for deterministic layer ordering. | external-source-reference |
| ICW-REQ5-047 | P1 | Cache is still keyed around demo tile object identity: `TileCacheBudget` tracks `BackgroundTileCacheKey` to `SampleImageTile`, counts resident tile IDs via `entry.Tile.Id`, and pins by tile ID strings. | Create `ViewportCacheBudget` with `ViewportTileKey` and `ICacheResident` abstractions. | external-source-reference |
| ICW-REQ5-048 | P1 | Eviction policy should not call back into scheduler from tile object: `TileCacheBudget.TryReserve` collects evicted entries, and `SampleImageTile.EvictCacheEntry` can call coordinator `RemoveClaimant`; comments label this a reentrant lock chain. | Split eviction into `EvictionPlan` computed under cache lock and `IEvictionObserver` notifications outside all locks. | external-source-reference, external-source-reference |
| ICW-REQ5-049 | P1 | Pinned tiles are source-ambiguous: `SetPinnedTiles` pins by `tile.Id`. | Pin with `ViewportTileKey` or `ViewportTileIdentity` including source ID and content revision. | external-source-reference |
| ICW-REQ5-050 | P1 | Tile bounds lookups are source-ambiguous: Center-distance scheduling uses `_tileBoundsById.TryGetValue(key.TileId, out var bounds)`. | Move bounds into `IViewportTileCatalog` keyed by `ViewportTileKey`. | external-source-reference |
| ICW-REQ5-051 | P1 | Tile work request should carry immutable request context: Tiles receive mutable `ClaimantIdProvider`, `ClaimantTokenProvider`, and `ReleaseReservedCacheEntry` after generation. | Introduce `TileMaterializationRequest` with keys, claimant, token, priority, and cache lease callback as immutable inputs. | external-source-reference |
| ICW-REQ5-052 | P1 | Frame claimant lifetime needs a type: RenderFrameAsync swaps `_frameTileCts`, cancels previous CTS, and disposes the one from two frames ago by convention. | Add `FrameClaimantLease : IDisposable` to centralize two-frame/lifetime semantics and tests. | external-source-reference |
| ICW-REQ5-053 | P1 | Failure retry needs policy: `OnTilePixelsGenerationFailed` schedules `RequestRenderAsync` to retry; no failure classes/backoff are shown. | Add `ITileRetryPolicy`, `TileFailureState`, and retry budget keyed by `ViewportTileKey`. | external-source-reference |
| ICW-REQ5-054 | P1 | Render invalidation needs reasons and coalescing policy: Viewport changes, tile events, style changes, and failure retry all request render through one route. | Add `RenderInvalidation` and `IRenderInvalidationQueue` with reason, source revision, and priority. | external-source-reference |
| ICW-REQ5-055 | P1 | Display settings should be immutable frame input: Render path reads `_showBackgroundImages`, `_showImageTiles`, and annotation display options from mutable host fields. | Add `ViewportDisplaySettingsSnapshot` and pass it into frame/render plan. | external-source-reference |
| ICW-REQ5-056 | P1 | Annotation/hit target type is still sample-bound: `UpdateAnnotationLayer` skips all non-`SampleAnnotation` items and attaches tooltips/selection handlers to `Border` elements. | Create `IViewportVisualItem` with `Bounds`, `LayerId`, `ItemId`, `VisualStyle`, `TooltipPayload`, and `HitTest`. | external-source-reference |
| ICW-REQ5-057 | P1 | Selection should not be string ID in host: `_selectedAnnotationId` is compared with `annotation.Id` in overlay update. | Add `SelectionSnapshot` keyed by `ViewportItemId` and layer/source revision. | external-source-reference |
| ICW-REQ5-058 | P1 | Tooltip rendering should be adapter-owned payload, not sample object: Host creates `DeferredAnnotationToolTip(annotation)` while rendering annotation overlay. | Add `ICanvasTooltipPayload` and `ITooltipContentFormatter`; controls render the payload. | external-source-reference |
| ICW-REQ5-059 | P1 | Feature grid should consume typed inspection payload: `SampleAnnotation.Features` is a lazy dictionary and there is a tracked task for typed annotation metrics. | Add `IInspectionFeatureProvider` and typed `AnnotationMetrics` / `FeatureRows` records. | external-source-reference, external-source-reference |
| ICW-REQ5-060 | P1 | Pixelometer should be frame-consistent: Frame completion updates pixelometer from `_hoverPointerPosition` after the frame is rendered. | Make `PixelometerService.Read(frame, screenPoint)` use the accepted frame snapshot, not mutable current scene state. | external-source-reference |
| ICW-REQ5-061 | P1 | Pixelometer needs structured unavailable reasons: `TryReadResidentPixel` returns false with default sample, while UI formats fallback strings. | Add `ViewportPixelSample.IsAvailable` plus `UnavailableReason` enum/string. | external-source-reference |
| ICW-REQ5-062 | P1 | Pixelometer sample should include resident vs requested mip: `TryReadResidentPixel` reads a resident mip and constructs readout info, but the external sample contract does not appear to carry a rich mip provenance model. | Add `RequestedMipLevel`, `ResidentMipLevel`, and `MipSelectionPolicyId` to pixel readout. | external-source-reference |
| ICW-REQ5-063 | P1 | Hit-test tolerance should be screen-based: `QueryPoint` embeds `const double probeSize = 0.01`. | Add `HitTestPolicy` with screen-pixel tolerance converted through camera transform. | external-source-reference |
| ICW-REQ5-064 | P1 | Accepted-frame overlay sync needs explicit tests: `OnCanvasFramePublished` uses `_lastPublishedCamera` and `_lastPublishedVisibleTiles` set before `CanvasSurface.PublishFrame`. | Add stale-frame rejection tests that assert overlays are not updated for rejected frames. | external-source-reference |
| ICW-REQ5-065 | P1 | Frame buffer validity should be lease-backed: `ZeroCopyBitmapFactory` docs say returned bitmaps are backed by the factory memory section and are valid while the factory/file mapping remains alive. | Add `IFrameSurfaceLease`/`FrameBufferLease` and require frame publication to transfer lease ownership explicitly. | external-source-reference |
| ICW-REQ5-066 | P1 | Render surface policy should be host-visible: RenderFrameAsync clamps width/height to 4096. | Add `RenderSurfacePolicy` with max dimensions and scale-down behavior reported in diagnostics. | external-source-reference |
| ICW-REQ5-067 | P2 | Stage telemetry should align with profiler goals: Task notes say future profiles should separate native FastNoise generation, normalization, circle rasterization, composition, cache state, mip level, sample count, and payload bytes. | Add `RenderStageTelemetry` and `TileGenerationStageTelemetry`. | external-source-reference |
| ICW-REQ5-068 | P2 | Noise policy should be an explicit product choice: Audit pass says per-tile seed and local min/max conflict with seamless worldspace sampling. | Add `NoiseFieldPolicy` with modes such as `WorldContinuous` and `PerTileDeterministic`, plus boundary tests. | external-source-reference |
| ICW-REQ5-069 | P2 | Mip policy should be explicit under anisotropic zoom: Audit synthesis says `SelectMipLevel` under-resolves zoomed-in axis for anisotropic states. | Add `IMipSelectionPolicy` and tests for non-uniform scale. | external-source-reference |
| ICW-REQ5-070 | P2 | Spatial queries need allocation-aware contract: A ticket proposes non-allocating count/streaming query APIs for high-frequency viewport checks. | Add `ISpatialQuerySink<T>` or `QueryVisibleInto` to avoid per-frame arrays where possible. | external-source-reference |
| ICW-REQ5-071 | P2 | Layer visibility needs a typed model: Raster visibility currently combines background and image-tile booleans; annotation/label state is separate. | Add `LayerVisibilitySnapshot` keyed by `ViewportLayerId`. | external-source-reference |
| ICW-REQ5-072 | P2 | Diagnostics should be support-bundle-ready: Frame status and cache status are strings/logs, and diagnostic fields are primitive counters. | Add `ViewportDiagnosticsSnapshot` with JSON-safe fields: frame ID, source revisions, stage timings, cache stats, tile failures. | external-source-reference |
| ICW-REQ5-073 | P2 | Close/unload leak tests should be added: Constructor subscribes CanvasSurface events, Loaded/Closed, and CompositionTarget.Rendering. | Add weak-reference tests verifying window/control can be collected after close/unload. | external-source-reference |
| ICW-REQ5-074 | P2 | The control extraction test proves hosting, not replacement parity: Handoff says consumer-host test constructs the control and publishes a frame outside the app. | Add `host applicationLikeViewportHostTests` using neutral fake layers/sources/revision vectors. | external-source-reference |
| ICW-REQ5-075 | P2 | Runtime stress claims need WPF evidence: Audit synthesis says no runtime reproduction was run for concurrency candidates and profiler artifacts were not deeply inspected. | Add WPF integration stress harness: fast scroll, zoom, resize, close during generation, tile failure storms. | external-source-reference |
| ICW-REQ5-076 | P2 | Task status must not be treated as proof: Active tasks show many “Done” items, but later audit materials include validation limitations and status divergence history. | Add `EvidenceLevel` field to task entries: source-read, unit-test, integration-test, runtime-repro, field-verified. | external-source-reference, external-source-reference |
| ICW-REQ5-077 | P2 | General layer hit testing should be decoupled from visual elements: Current click handler depends on `Border { Tag: SampleAnnotation annotation }`. | Add `ILayerHitTester` and `HitTestResult` separate from WPF element identity. | external-source-reference |
| ICW-REQ5-078 | P2 | Input abstraction should be promoted depending on target parity: ADR/handoff material says ICW-313 input handler abstraction remains deferred. | Define `IViewportInputHandler`, `ViewportInputContext`, and `ViewportCommand` in the control library if replacing host application interactions is in scope. | external-source-reference |
| ICW-REQ5-079 | P2 | Fallback/feature flag boundary is not documented in source evidence: No retrieved source defines a fallback policy for production replacement. | Add `IViewportImplementationSelector` and `ViewportFallbackPolicy` at integration layer. | external-source-reference |
| ICW-REQ5-080 | P2 | Secret-safe adapter guidance should be codified: The requested agent guidance needs interfaces/classes without exposing secret domain identifiers. | Create an `ADAPTER_GUIDANCE.md` with neutral names and a rule: internal adapters may map private types to these contracts, but generic contracts must stay domain-neutral. | external-source-reference |

## Recommended Class / Interface Implementation Order

| Step | Interfaces / classes | Purpose |
|---:|---|---|
| 1 | `ViewportSourceId`, `ViewportLayerId`, `ViewportItemId`, `ViewportRevision`, `ViewportRevisionVector` | Foundation for source/layer/item identity without private names. |
| 2 | `ViewportSnapshot`, `ViewportSourceHealth`, `ViewportDisplaySettingsSnapshot`, `LayerVisibilitySnapshot` | Immutable frame input model. |
| 3 | `ViewportTileKey`, `IViewportTileSource`, `IViewportTileCatalog`, `IViewportCacheBudget` | Replace `"synthetic"`, plain tile IDs, and sample-tile cache coupling. |
| 4 | `IViewportLayerSource`, `ViewportLayerRegistry`, `LayerRenderPlan`, `LayerRenderPlanEntry` | Replace ad hoc grid/annotation host overlay composition. |
| 5 | `IViewportVisualItem`, `ILayerHitTester`, `HitTestPolicy`, `HitTestResult` | Move hit testing and WPF element identity out of `MainWindow`. |
| 6 | `IViewportSelectionService`, `SelectionSnapshot`, `ICanvasTooltipPayload`, `ITooltipContentFormatter` | Move selection/tooltip ownership out of host downcasts. |
| 7 | `ViewportPixelSample`, `LayerPixelContribution`, `PixelCompositePolicy`, `PixelometerService` | Make pixelometer source/layer/revision-aware. |
| 8 | `RasterFrame`, `ViewportFrame`, `AcceptedFrameContext`, `FrameSurfaceLease`, `IFramePublisher` | Make raster, overlays, and frame-buffer lifetime explicit. |
| 9 | `RenderInvalidation`, `RenderInvalidationReason`, `IRenderInvalidationQueue`, `IRenderScheduler` | Stop treating every invalidation as the same render request. |
| 10 | `ViewportDiagnosticsSnapshot`, `RenderStageTelemetry`, `TileGenerationStageTelemetry` | Support support-bundle-ready diagnostics and target-hardware evidence. |

## Skeleton Interfaces for the Agent

```csharp
public readonly record struct ViewportSourceId(string Value);
public readonly record struct ViewportLayerId(string Value);
public readonly record struct ViewportItemId(string Value);
public readonly record struct ViewportRevision(long Value);

public sealed record ViewportRevisionVector(
    ViewportRevision RenderRevision,
    IReadOnlyDictionary<ViewportSourceId, ViewportRevision> SourceRevisions,
    IReadOnlyDictionary<ViewportLayerId, ViewportRevision> LayerRevisions,
    ViewportRevision DisplaySettingsRevision,
    ViewportRevision SelectionRevision);

public sealed record ViewportSnapshot(
    ViewportRevisionVector Revisions,
    SpatialBounds SceneBounds,
    CameraSnapshot Camera,
    ViewportDisplaySettingsSnapshot DisplaySettings,
    LayerVisibilitySnapshot LayerVisibility,
    ViewportSourceHealth SourceHealth);

public readonly record struct ViewportTileKey(
    ViewportSourceId SourceId,
    string TileId,
    ViewportRevision ContentRevision,
    int MipLevel);

public interface IViewportTileSource
{
    ViewportSourceId SourceId { get; }
    bool TryGetDescriptor(ViewportTileKey key, out ViewportTileDescriptor descriptor);
    ValueTask<ViewportTilePayload> MaterializeAsync(ViewportTileKey key, CancellationToken cancellationToken);
}

public interface IViewportLayerSource
{
    ViewportLayerId LayerId { get; }
    ViewportRevision CurrentRevision { get; }
    IReadOnlyList<IViewportVisualItem> QueryVisible(ViewportSnapshot snapshot, SpatialBounds viewport);
}

public interface IViewportVisualItem : ICanvasItem
{
    ViewportItemId ItemId { get; }
    ViewportLayerId LayerId { get; }
    int ZOrder { get; }
    ICanvasTooltipPayload? Tooltip { get; }
}

public sealed record LayerRenderPlan(ViewportSnapshot Snapshot, IReadOnlyList<LayerRenderPlanEntry> Entries);
public sealed record LayerRenderPlanEntry(ViewportLayerId LayerId, ViewportRevision Revision, IReadOnlyList<IViewportVisualItem> Items, bool IsVisible);

public sealed record ViewportFrame(BitmapSource Raster, ViewportSnapshot Snapshot, LayerRenderPlan Plan, ViewportDiagnosticsSnapshot Diagnostics);
```

## Acceptance Criteria for the Next Agent

- No production/reusable code constructs `BackgroundTileCacheKey("synthetic", ...)`; demo code may use a single constant only inside demo fixtures.
- All cache, bounds, pinning, eviction, and tile materialization APIs use `ViewportTileKey` or equivalent source-qualified identity.
- Frame acceptance publishes raster and overlay plan under one `ViewportRevisionVector`; stale raster cannot update overlays and stale overlay cannot attach to newer raster.
- Generic control/library code does not downcast to `SampleAnnotation` or reference demo-only types.
- Selection, tooltip, hit testing, and pixelometer all operate on neutral visual items and snapshots.
- Runtime stress harness includes fast scroll, rapid zoom, continuous resize, close during generation, tile failure retry, stale-frame rejection, and cache pressure scenarios.
- Diagnostics can emit a secret-safe JSON snapshot with neutral IDs, counts, timings, and failure reasons only.

## Secret-Safe Agent Instructions

- Use neutral identifiers such as `source-a`, `layer-defects`, `item-1`, and synthetic generated pixel data in tests.
- Do not include customer names, private inspection names, internal paths, credentials, or raw production payloads in contracts, fixtures, logs, or docs.
- Keep proprietary adapter mapping in an internal adapter layer. The shared contracts should remain generic and safe to discuss.
- Treat task tracker evidence as directional unless backed by current source reads or runtime tests.


