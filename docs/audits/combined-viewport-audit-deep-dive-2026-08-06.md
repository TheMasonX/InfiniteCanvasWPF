# InfiniteCanvasWPF Combined Delta Audit Deep Dive

**Description:** Deep combined delta audit of InfiniteCanvasWPF source after prior D4/D5 findings, with expanded implementation guidance and test plan.  
**Timestamp:** 2026-08-06 12:28 CDT  
**Author:** Copilot  
**Repository / Subject:** InfiniteCanvasWPF / production viewport replacement candidate  
**Status:** Changes Requested  
**Overall Confidence:** 84%  
**Classification:** Source-backed and strongly inferred delta findings only; not a full replacement-readiness master report.  
**Secret Posture:** Uses neutral names only. No credentials, customer-private data, internal URLs, or proprietary adapter names are included.  

> This is a delta-only report. It deliberately does not repeat the entire master production viewport replacement readiness audit. It expands the latest D4/D5 delta findings into an implementation-ready handoff with clear source basis, risk mechanisms, recommendations, tests, acceptance criteria, and sequencing.

## 1. Executive Summary

The codebase has moved beyond an early prototype: there is evidence of meaningful work around scene-source abstractions, CanvasFrame boundaries, frame-buffer pooling, resident mip readout, and coordinator-backed tile work. The remaining issues are therefore more subtle and more important: they are boundary invariants that will become expensive if the control is extracted or integrated into a production viewport production viewport before the invariants are closed.

The primary decision remains **Changes Requested**. Do not spend the next engineering slice mainly on visible production viewport parity features until shutdown ownership, source-qualified identity, host-neutral readout, scheduler/coordinator correctness, and query-contract authority are hardened.

The highest-risk combined themes are:

- Lifecycle shutdown remains too implicit for close/regenerate/render races.
- Pixelometer acquisition is partially source-neutral, but composition still leaks the demo `SampleAnnotation` model.
- Tile generation still has non-cancellable or weakly bounded escape hatches.
- Source/cache identity still uses hard-coded synthetic source IDs in hot paths.
- Coordinator disposal and worker-start sequencing need exactly-once cleanup guarantees.
- The reusable API boundary still has split query authority and should not be extracted until hardened.

## 2. Evidence Corpus

| ID | Document / Result | Reference | Use |
| --- | --- | --- | --- |
| S1 | icw-concat-8-6-26.04-of-05.txt | external-source-reference / external-source-reference | Current source snapshot containing MainWindow.xaml.cs and SampleImageTile.cs snippets used for lifecycle, pixelometer, tile-generation, source-key, and dispatcher findings. |
| S2 | icw-concat-8-6-26.02-of-05.txt | external-source-reference | TileWorkCoordinator.cs source snippet and embedded audit notes used for coordinator disposal, active-count, claimant cleanup, dead-code, and lock-chain findings. |
| S3 | icw-concat-8-6-26.05-of-05.txt | external-source-reference | 22-audit reconciliation at HEAD 84a0cdb, ICW-316A/319..326 synthesis, duplicate query authority, noise seamlessness, anisotropic mip selection, and process context. |
| S4 | icw-concat-8-6-26.manifest.csv | external-source-reference | Manifest showing chunked corpus coverage and source-file mapping for the concat bundle. |
| S5 | App.xaml.cs search result | external-source-reference / external-source-reference / external-source-reference | Dispatcher exception handling and prior audit evidence for unconditional e.Handled behavior. |

Evidence confidence is intentionally conservative. Search snippets and prior AI-generated audits are treated as directional unless the snippet includes direct source text or the audit explicitly reports source-traced verification. Findings in this report are included only when either source snippets directly support the mechanism or multiple source-traced audit snippets converge on the same issue.

## 3. Methodology

- Re-read the current source snippets around `MainWindow`, tile materialization, pixelometer readout, render invalidation, shutdown, and coordinator lifecycle.
- Cross-checked against the 22-audit reconciliation and embedded source-audit notes for stale/refuted claims.
- Kept only deltas that are not merely restatements of the master audit.
- Separated direct source-backed defects from sequencing requirements and hardening recommendations.
- Avoided product-private names and kept implementation guidance neutral.

## 4. Consolidated Findings Index

| ID | Priority | Area | Finding | Confidence |
| --- | --- | --- | --- | --- |
| D4-001 | P1 | Shutdown / lifecycle | OnClosed teardown can race active regeneration | 84% |
| D4-002 | P1 | Pixelometer / abstraction | Pixelometer composition still depends on SampleAnnotation | 90% |
| D4-003 | P1 | Tile generation / cancellation | SampleImageTile.Pixels is a synchronous non-cancellable generation escape hatch | 88% |
| D4-004 | P1 | Tile generation / scheduler | Non-coordinator fallback bypasses bounded work | 86% |
| D4-005 | P1 | Source/cache identity | Tile/cache identity still uses hard-coded synthetic source IDs | 92% |
| D4-006 | P2 | Data ownership / safety | SampleAnnotation stores mutable defect payloads directly | 78% |
| D4-007 | P2 | UI scheduling / throughput | Tile completion floods dispatcher before render coalescing | 81% |
| D5-008 | P1 | Coordinator disposal | TileWorkCoordinator.Dispose has an admission window | 83% |
| D5-009 | P1 | Coordinator active-count / reservation cleanup | StartWorkItem can strand active count if Task.Run never enters delegate | 76% |
| D5-010 | P2 | Coordinator claimant cleanup | Queued cancellation should clear claimant registrations explicitly | 72% |
| D5-011 | P2 | Dead code / cleanup | TileWorkItem.GetClaimantIds appears orphaned | 80% |
| D5-012 | P2 | Exception policy / observability | Dispatcher exceptions are swallowed unconditionally | 80% |
| D5-013 | P2 / P1 gate | Contract sequencing | Duplicate item-query authority must be resolved before extraction | 95% |
| D5-014 | P2 | Noise / visual continuity | Noise seamlessness needs seed and normalization fixes | 82% |
| D5-015 | P2 | Mip selection | Anisotropic mip selection remains open under non-uniform scale | 88% |

## 5. Dependency and Sequencing Overview

The findings are interdependent. Fixing them in the wrong order can create churn or publish unstable contracts. The recommended sequence is:

| Order | Findings | Theme | Reason |
| --- | --- | --- | --- |
| 1 | D4-001, D5-008, D5-009 | Lifecycle first | Shutdown and coordinator disposal must be deterministic before expanding runtime behavior. |
| 2 | D4-003, D4-004, D5-010 | Generation ownership | Remove unbounded/non-cancellable generation and fix queued claimant cleanup. |
| 3 | D4-005 | Identity foundation | Source-qualified identity must exist before multi-source adapters, diagnostics, and readout are trustworthy. |
| 4 | D4-002, D5-013 | Contract authority | Resolve pixel/readout and item-query authority before extraction and hit-testing. |
| 5 | D4-007, D5-012 | UI reliability | Bound dispatcher invalidations and classify exception handling. |
| 6 | D4-006, D5-011, D5-014, D5-015 | Hardening/quality | Clean stale/dead code, immutable payloads, visual continuity, and anisotropic mip quality. |

## 6. Detailed Findings

### D4-001: OnClosed teardown can race active regeneration

**Priority:** P1  
**Area:** Shutdown / lifecycle  
**Classification:** Strongly source-backed lifecycle risk  
**Confidence:** 84%  

#### Evidence

- RegenerateSceneAsync waits on _generationGate and releases it in finally after scene changes, tile generation, spatial publish, render request, and SceneChanged invocation.
- OnClosed is async void, cancels lifetime state, awaits render-action disposal, detaches frame shell, disposes frame buffer pool, cancels/disposes tile coordinator, disposes _generationGate, and disposes _lifetime.

#### Risk Mechanism

A close during regeneration can dispose shared primitives that an active regeneration path still owns or is about to release. This is exactly the class of shutdown ordering bug that tends to appear under rapid close, cancellation, regeneration failure, or test automation rather than during happy-path manual use.

#### Recommendation

Track active regeneration/render operations, mark closing first, cancel lifetime, await active tasks, then dispose shared resources. Treat OnClosed as a wrapper around a deterministic ShutdownAsync workflow.

#### Targeted Tests

- `CloseDuringRegeneration_DoesNotDisposeGateBeforeRelease`
- `CloseDuringSpatialPublish_DoesNotThrow`
- `CloseDuringRequestRender_DoesNotThrow`
- `Shutdown_IsIdempotent`

#### Acceptance Criteria

- No ObjectDisposedException/SemaphoreFullException from close during generation.
- _generationGate.Dispose occurs only after no active code path can Release it.
- All tile events are unsubscribed or ignored before resource disposal completes.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-002: Pixelometer composition still depends on SampleAnnotation

**Priority:** P1  
**Area:** Pixelometer / abstraction  
**Classification:** Source-backed abstraction defect  
**Confidence:** 90%  

#### Evidence

- UpdatePixelometer reads background/defect through CanvasSurface.SceneSource.TryReadResidentPixel.
- ResolveDisplayPixelValue then performs a second QueryPoint and filters hits with OfType<SampleAnnotation>().

#### Risk Mechanism

The readout path looks source-neutral at the acquisition boundary but is not source-neutral at the composition boundary. A non-demo host can feed valid ICanvasItem data and still fail to participate in final value composition because the host data is not SampleAnnotation.

#### Recommendation

Move final pixel composition into the scene/readout source contract. Return a single composite readout snapshot that includes background, defect/contribution values, final value, tile/source identity, revision, and display metadata.

#### Targeted Tests

- `Pixelometer_ExternalHostItem_ComposesWithoutSampleAnnotation`
- `Pixelometer_CompositeSample_UsesSingleSnapshot`
- `Pixelometer_DoesNotRunSecondIndependentQuery`

#### Acceptance Criteria

- No pixelometer path uses OfType<SampleAnnotation>.
- Composite readout is produced by one immutable readout source snapshot.
- External host item type can generate correct pixelometer value.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-003: SampleImageTile.Pixels is a synchronous non-cancellable generation escape hatch

**Priority:** P1  
**Area:** Tile generation / cancellation  
**Classification:** Source-backed cancellation/scheduler defect  
**Confidence:** 88%  

#### Evidence

- The Pixels getter calls _pixelFactory(CancellationToken.None) under _cacheGate when no cached pixels exist.

#### Risk Mechanism

This bypasses tile coordinator concurrency, cancellation, cache reservation, viewport interest, diagnostics, and scheduling. It also performs expensive work while holding the cache lock. Even if current hot paths avoid it, the public property is a future production footgun.

#### Recommendation

Remove the public sync materialization path or mark it test-only/obsolete with error=true. Production code should use non-blocking resident reads or explicit async scheduler/coordinator materialization.

#### Targeted Tests

- `SampleImageTile_PixelsProperty_NotCallableFromProduction`
- `SourceScan_NoProductionReadsOfSampleImageTilePixels`
- `TileGeneration_AllProductionPathsAcceptCancellationToken`

#### Acceptance Criteria

- No production path calls _pixelFactory(CancellationToken.None) through a property.
- Synchronous generation exists only in explicitly test-only code, if at all.
- All production generation flows participate in cancellation.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-004: Non-coordinator fallback bypasses bounded work

**Priority:** P1  
**Area:** Tile generation / scheduler  
**Classification:** Source-backed architecture defect  
**Confidence:** 86%  

#### Evidence

- The no-coordinator path uses bare Task.Run and calls _pixelFactory(CancellationToken.None).

#### Risk Mechanism

Coordinator-backed semantics are where bounded concurrency, claimant ownership, cache reservation, cancellation, and diagnostics live. If the coordinator is optional, production invariants become optional.

#### Recommendation

Replace optional coordinator with an injected ITileGenerationScheduler. Demo/test can use an immediate or limited scheduler, but production should fail fast if no scheduler is provided.

#### Targeted Tests

- `TileGeneration_WithoutScheduler_FailsFast`
- `TileGeneration_ProductionPath_UsesInjectedScheduler`
- `TileGeneration_CancellationToken_ReachesFactory`

#### Acceptance Criteria

- No hot-path tile generation uses bare Task.Run except test-only code.
- No production path silently falls back to unbounded uncancellable generation.
- Scheduler path emits diagnostics and honors token propagation.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-005: Tile/cache identity still uses hard-coded synthetic source IDs

**Priority:** P1  
**Area:** Source/cache identity  
**Classification:** Source-backed identity defect  
**Confidence:** 92%  

#### Evidence

- RenderFrameAsync constructs BackgroundTileCacheKey("synthetic", tile.Id, epoch, mipLevel).
- ResetImageCache constructs old keys with BackgroundTileCacheKey("synthetic", Id, oldRevision, mip).

#### Risk Mechanism

The cache/coordinator/readout identity model is not ready for multiple sources, views, source revisions, or production adapters. It can collide same tile IDs across sources and will obscure diagnostics.

#### Recommendation

Promote a source-qualified ViewportTileKey or BackgroundTileKey with SourceId, LayerId, TileId, ContentRevision, and MipLevel. Make source identity required at tile construction.

#### Targeted Tests

- `CacheKey_SourceIdentity_DistinguishesSameTileAcrossSources`
- `ResetImageCache_ReleasesOnlyMatchingSourceKeys`
- `Diagnostics_IncludeSourceIdInTileReadout`

#### Acceptance Criteria

- No reusable/production code constructs BackgroundTileCacheKey("synthetic", ...).
- Same TileId across two source IDs does not collide.
- Pixelometer/cache/coordinator diagnostics include neutral source identity.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-006: SampleAnnotation stores mutable defect payloads directly

**Priority:** P2  
**Area:** Data ownership / safety  
**Classification:** Source-backed hardening defect  
**Confidence:** 78%  

#### Evidence

- SampleAnnotation takes byte[] defectPixels and assigns it to public DefectPixels without visible defensive copy in the reviewed snippet.

#### Risk Mechanism

Trusted demo generation may be safe, but reusable/product adapter boundaries need defensive ownership. Mutable arrays can be changed after construction and malformed dimensions can fail later in rendering or readout.

#### Recommendation

Validate dimensions and copy payloads, or replace tuple-like fields with an immutable Gray8Patch value object.

#### Targeted Tests

- `SampleAnnotation_DefectPixels_InvalidLengthThrows`
- `SampleAnnotation_DefectPixels_DefensiveCopy`
- `SampleAnnotation_DefectPixels_DisallowsZeroDimensions`

#### Acceptance Criteria

- Invalid payload size fails at construction.
- Caller mutation after construction cannot affect rendering/readout.
- Payload ownership is documented.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D4-007: Tile completion floods dispatcher before render coalescing

**Priority:** P2  
**Area:** UI scheduling / throughput  
**Classification:** Source-backed performance/reliability risk  
**Confidence:** 81%  

#### Evidence

- Each PixelsGenerated/PixelsGenerationFailed event schedules Dispatcher.InvokeAsync and then calls RequestRenderAsync. Render body coalescing happens later in _renderAction.RequestAsync.

#### Risk Mechanism

A cold-cache tile burst can enqueue many dispatcher callbacks even when actual rendering coalesces. Dispatcher saturation can make the UI feel sluggish and can delay input, close, and error propagation.

#### Recommendation

Add a dispatcher-level render invalidation gate before RequestRenderAsync. Coalesce at the dispatcher-entry level as well as at the render-body level.

#### Targeted Tests

- `TileCompletionStorm_QueuesSingleDispatcherInvalidation`
- `TileFailureStorm_IsBounded`
- `RenderInvalidationCounter_ColdCacheScroll`

#### Acceptance Criteria

- N tile completions while an invalidation is pending produce one queued dispatcher callback.
- Failures still trigger retry through bounded route.
- Diagnostics record invalidation count and reason.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-008: TileWorkCoordinator.Dispose has an admission window

**Priority:** P1  
**Area:** Coordinator disposal  
**Classification:** New source-backed concurrency/lifecycle risk  
**Confidence:** 83%  

#### Evidence

- Dispose calls CancelAll, cancels/disposes _disposeCts, then locks and sets _disposed=true.
- Request checks _disposed under _lock and creates TileWorkItem using the dispose token.

#### Risk Mechanism

A concurrent Request can observe _disposed=false while disposal is in progress or after cancellation infrastructure is being torn down. This can admit work during teardown or create items tied to canceled/disposed infrastructure.

#### Recommendation

Mark disposed first under lock, then cancel/drain existing items and dispose shared token source. If CancelAll requires the lock, split state transition from cleanup.

#### Targeted Tests

- `Request_DuringDispose_DoesNotAdmitWork`
- `Dispose_IsIdempotent`
- `Dispose_BlocksNewAdmissionBeforeCancelAll`

#### Acceptance Criteria

- Concurrent Request during Dispose is consistently rejected.
- No item is linked to disposed _disposeCts.
- Dispose can be called repeatedly without inconsistent counters.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-009: StartWorkItem can strand active count if Task.Run never enters delegate

**Priority:** P1  
**Area:** Coordinator active-count / reservation cleanup  
**Classification:** New strongly inferred source-backed risk  
**Confidence:** 76%  

#### Evidence

- StartWorkItem sets running state and increments _activeCount before Task.Run.
- Cleanup and active count decrement are inside the delegate or HandleWorkStopped.
- Task.Run is supplied _disposeCts.Token.

#### Risk Mechanism

If the scheduling token is already canceled before the task body starts, the delegate may never run, leaving active count and reservation cleanup dependent on code that never executes. This is most plausible during disposal/start races.

#### Recommendation

Do not pass dispose token to Task.Run scheduling. Use the work token inside the delegate/factory and ensure scheduling exceptions/cancellations call cleanup exactly once.

#### Targeted Tests

- `DisposeDuringStart_DoesNotLeakActiveCount`
- `DisposeDuringStart_DoesNotLeakReservation`
- `TaskSchedulingFailure_CleansUpWorkItem`

#### Acceptance Criteria

- Every StartWorkItem path has exactly-once active-count decrement.
- Every admitted reservation is released exactly once.
- Scheduling cancellation/failure is covered by tests.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-010: Queued cancellation should clear claimant registrations explicitly

**Priority:** P2  
**Area:** Coordinator claimant cleanup  
**Classification:** New hardening recommendation  
**Confidence:** 72%  

#### Evidence

- Bundled audit notes say queued CancelWorkItem removes/dispatches the item without clearing CancellationTokenRegistration objects. Current frame-scoped tokens bound the retention, but the Request API does not require that lifetime.

#### Risk Mechanism

A future caller using long-lived cancellable tokens could retain orphaned work items longer than intended. This is a reusable-library hardening issue.

#### Recommendation

Add TileWorkItem.ClearClaimants that disposes registrations and empties claimant list. Call it from queued cancellation and disposal cleanup paths.

#### Targeted Tests

- `QueuedCancel_DisposesClaimantRegistrations`
- `QueuedCancel_LongLivedToken_DoesNotRetainWorkItem`
- `ClearClaimants_IsIdempotent`

#### Acceptance Criteria

- Queued cancellation disposes all claimant registrations immediately.
- Claimant callback does not fire after cleanup.
- Long-lived token scenario does not retain item through registration closure.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-011: TileWorkItem.GetClaimantIds appears orphaned

**Priority:** P2  
**Area:** Dead code / cleanup  
**Classification:** Cleanup / maintenance  
**Confidence:** 80%  

#### Evidence

- Bundled source audit states GetClaimantIds is defined once and no longer referenced after PublishInterestSet switched to direct cancellation.

#### Risk Mechanism

Small, but it preserves stale mental model and unnecessary public/internal surface. Comments referencing removed behavior can mislead future agents.

#### Recommendation

Delete GetClaimantIds and update ICW-143 deferred notes so this is not treated as allocation tuning.

#### Targeted Tests

- `SourceScan_NoGetClaimantIds`
- `Build_AllTestsPass`

#### Acceptance Criteria

- Method and stale XML documentation are removed.
- Tracker note no longer frames dead method as performance task.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-012: Dispatcher exceptions are swallowed unconditionally

**Priority:** P2  
**Area:** Exception policy / observability  
**Classification:** Observability/reliability defect  
**Confidence:** 80%  

#### Evidence

- App.xaml.cs wires DispatchUnhandledException and audit snippets state the handler logs and sets e.Handled=true for all dispatcher exceptions.

#### Risk Mechanism

Serious dispatcher failures can be hidden, especially from async void handlers and dispatcher-scheduled render/tile events. Silent continuation after invariant failures can make corruption harder to diagnose.

#### Recommendation

Classify exceptions as recoverable/degraded/fatal. Log and surface recoverable errors; fail fast or show fatal UI for invariant/native-resource/corruption failures. Do not blanket-handle all dispatcher exceptions.

#### Targeted Tests

- `DispatcherUnhandledException_FatalPolicy_NotAlwaysHandled`
- `DispatcherUnhandledException_RecoverablePolicy_ShowsDegradedState`
- `AsyncVoidHandler_Exception_IsObserved`

#### Acceptance Criteria

- Fatal dispatcher exceptions are not swallowed silently.
- Policy is documented.
- Tests/harness prove behavior for at least one fatal and one recoverable exception.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-013: Duplicate item-query authority must be resolved before extraction

**Priority:** P2 / P1 gate  
**Area:** Contract sequencing  
**Classification:** Source-backed sequencing requirement  
**Confidence:** 95%  

#### Evidence

- 22-audit reconciliation confirms ICanvasSceneSource and ICanvasSpatialQuerySource both declare QueryVisible and are wired as dependency properties. It gates ICW-314 and ICW-316A on resolving the split-brain API.

#### Risk Mechanism

If extraction happens first, the ambiguity becomes public library API and is harder to change later.

#### Recommendation

Pick one query authority or split methods by purpose. Update tests atomically with the API change.

#### Targeted Tests

- `CanvasBoundaryZeroReferenceTests_UpdateForSingleAuthority`
- `CanvasSceneSourceContractsTests_QueryAuthority`
- `CanvasControl_ConsumesExactlyOneQueryContract`

#### Acceptance Criteria

- CanvasControl has exactly one item-query authority.
- Selection/hit-testing target the resolved contract.
- ICW-316 extraction is blocked until this lands.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-014: Noise seamlessness needs seed and normalization fixes

**Priority:** P2  
**Area:** Noise / visual continuity  
**Classification:** Source-backed visual correctness/open requirement  
**Confidence:** 82%  

#### Evidence

- Audit pass 9 says per-tile seed variation defeats seamless worldspace sampling and per-tile local min/max normalization can still seam adjacent tiles.

#### Risk Mechanism

The implementation can be deterministic while still visibly discontinuous at tile boundaries. This is a product-quality issue if seamless synthetic/background imagery is a requirement.

#### Recommendation

Use one seed per source/revision for noise field continuity, use world coordinates for spatial variation, and replace per-tile normalization with stable global/analytic normalization or explicitly document non-seamless policy.

#### Targeted Tests

- `Noise_IsContinuousAcrossHorizontalBoundary`
- `Noise_IsContinuousAcrossVerticalBoundary`
- `Noise_MipBoundary_StableAcrossTiles`

#### Acceptance Criteria

- Boundary continuity meets tolerance across adjacent tiles and mips.
- If rejected as requirement, docs remove seamless wording and tests assert intended behavior.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

### D5-015: Anisotropic mip selection remains open under non-uniform scale

**Priority:** P2  
**Area:** Mip selection  
**Classification:** Source-backed rendering requirement  
**Confidence:** 88%  

#### Evidence

- ICW-325 says SelectMipLevel uses Math.Min(ScaleX, ScaleY), which can under-resolve the zoomed-in axis under anisotropic scale, while ADR-0005 requires sufficient texel density on both axes.

#### Risk Mechanism

Non-uniform scale can request too-coarse mip data along the demanding axis, degrading quality and making pixelometer/raster output less predictable.

#### Recommendation

Compute selected mip from the more demanding axis or evaluate horizontal/vertical texel density explicitly. Keep rasterizer and pixelometer mip semantics aligned.

#### Targeted Tests

- `SelectMipLevel_AnisotropicScaleXGreater_UsesDemandingAxis`
- `SelectMipLevel_AnisotropicScaleYGreater_UsesDemandingAxis`
- `Pixelometer_AnisotropicMip_AgreesWithRasterizer`

#### Acceptance Criteria

- Selected mip preserves required texel density on both axes.
- Tests cover ScaleX != ScaleY in both directions.

#### Counterarguments / Downgrade Conditions

- Downgrade if current HEAD already changed the relevant mechanism and tests prove the risk cannot occur.
- Downgrade if product scope explicitly keeps the code demo-only and blocks production/extraction use of the affected path.
- Keep as a hardening recommendation if the mechanism is real but impact is bounded by current single-host usage.

## 7. Implementation Guidance

### 7.1 Shutdown Coordinator

Create an explicit shutdown state machine rather than allowing `OnClosed`, render action disposal, regeneration cancellation, tile events, and frame-buffer disposal to coordinate implicitly.

```csharp
private int _shutdownStarted;
private Task? _activeRegenerationTask;

private async Task ShutdownAsync()
{
    if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        return;

    _lifetime.Cancel();

    if (_activeRegenerationTask is not null)
    {
        try { await _activeRegenerationTask.ConfigureAwait(true); }
        catch (OperationCanceledException) { }
    }

    await _renderAction.DisposeAsync().ConfigureAwait(true);

    // Detach UI/event sources before disposing shared resources.
    UnsubscribeTileGenerationEvents(_tiles);
    CompositionTarget.Rendering -= OnCompositionTargetRendering;
    CanvasSurface.DetachFrameShell();

    _tileCoordinator.Dispose();
    _frameBufferPool.Dispose();
    _generationGate.Dispose();
    _lifetime.Dispose();
}
```

### 7.2 Source-Qualified Tile Identity

Replace implicit demo identity with first-class neutral identity. Treat any hard-coded source ID as a demo-only fixture, never reusable or production logic.

```csharp
public readonly record struct ViewportSourceId(string Value);
public readonly record struct ViewportLayerId(string Value);
public readonly record struct ViewportTileId(string Value);
public readonly record struct ViewportRevision(long Value);

public sealed record ViewportTileKey(
    ViewportSourceId SourceId,
    ViewportLayerId LayerId,
    ViewportTileId TileId,
    ViewportRevision ContentRevision,
    int MipLevel);
```

### 7.3 Host-Neutral Pixel Readout

The pixelometer should consume one source-generated composite sample. It should not independently query generic items and downcast them to demo annotation types.

```csharp
public sealed record CanvasCompositePixelSample(
    ViewportSourceId SourceId,
    ViewportTileId TileId,
    ViewportRevision SourceRevision,
    int RequestedMipLevel,
    int ResidentMipLevel,
    byte Background,
    byte? Defect,
    byte FinalValue,
    IReadOnlyList<CanvasPixelContribution> Contributions,
    string DisplayText);

public interface ICanvasPixelReadoutSource
{
    bool TryReasource connection losspositePixel(
        double worldX,
        double worldY,
        int mipLevel,
        out CanvasCompositePixelSample sample);
}
```

### 7.4 Scheduler Contract

Tile materialization should require a scheduler rather than silently falling back to bare `Task.Run`.

```csharp
public sealed record TileGenerationRequest(
    ViewportTileKey Key,
    object ClaimantId,
    CancellationToken ClaimantToken,
    Func<CancellationToken, ValueTask<byte[]>> Factory,
    Action<ViewportTileKey, byte[]> OnCompleted,
    Action<ViewportTileKey, Exception> OnFailed);

public interface ITileGenerationScheduler
{
    bool TrySchedule(TileGenerationRequest request);
}
```

### 7.5 Dispatcher Invalidation Gate

```csharp
private int _renderInvalidationQueued;

private void QueueRenderInvalidation(RenderInvalidationReason reason)
{
    if (Interlocked.Exchange(ref _renderInvalidationQueued, 1) == 1)
        return;

    _ = Dispatcher.InvokeAsync(async () =>
    {
        Interlocked.Exchange(ref _renderInvalidationQueued, 0);
        if (!IsLoaded || _lifetime.IsCancellationRequested)
            return;
        await RequestRenderAsync().ConfigureAwait(true);
    }, DispatcherPriority.Render);
}
```

## 8. Detailed Task Plan

| Task | Priority | Area | Action | Finding |
| --- | --- | --- | --- | --- |
| ICW-D4-T001 | P1 | Lifecycle | Introduce shutdown coordinator and active regeneration tracking. | D4-001 |
| ICW-D5-T002 | P1 | Coordinator | Mark TileWorkCoordinator disposed before token cancellation and block concurrent admission. | D5-008 |
| ICW-D5-T003 | P1 | Coordinator | Guarantee StartWorkItem scheduling cleanup and exactly-once active/reservation accounting. | D5-009 |
| ICW-D4-T004 | P1 | Tile API | Remove or obsolete public SampleImageTile.Pixels production access. | D4-003 |
| ICW-D4-T005 | P1 | Tile scheduler | Replace non-coordinator Task.Run fallback with injected scheduler. | D4-004 |
| ICW-D4-T006 | P1 | Identity | Replace hard-coded synthetic source keys with source-qualified tile identity. | D4-005 |
| ICW-D4-T007 | P1 | Pixelometer | Move final-value composition into host-neutral readout source. | D4-002 |
| ICW-D5-T008 | P1/P2 | Contracts | Resolve duplicate QueryVisible authority before hit-testing/extraction. | D5-013 |
| ICW-D4-T009 | P2 | UI scheduling | Add dispatcher-level render invalidation gate. | D4-007 |
| ICW-D5-T010 | P2 | Coordinator | Clear claimant registrations on queued cancellation. | D5-010 |
| ICW-D4-T011 | P2 | Data ownership | Validate/copy defect pixel payloads or introduce immutable patch type. | D4-006 |
| ICW-D5-T012 | P2 | Cleanup | Delete orphaned GetClaimantIds and stale tracker wording. | D5-011 |
| ICW-D5-T013 | P2 | Observability | Classify dispatcher exceptions instead of blanket e.Handled=true. | D5-012 |
| ICW-D5-T014 | P2 | Visual quality | Fix or explicitly de-scope seamless noise requirement. | D5-014 |
| ICW-D5-T015 | P2 | Mip quality | Fix anisotropic mip selection policy and tests. | D5-015 |

## 9. Test Matrix

### 9.1 P1 Blocking Tests

- `CloseDuringRegeneration_DoesNotDisposeGateBeforeRelease`
- `CloseDuringSpatialPublish_DoesNotThrow`
- `Request_DuringDispose_DoesNotAdmitWork`
- `DisposeDuringStart_DoesNotLeakActiveCount`
- `DisposeDuringStart_DoesNotLeakReservation`
- `SampleImageTile_PixelsProperty_NotCallableFromProduction`
- `TileGeneration_WithoutScheduler_FailsFast`
- `CacheKey_SourceIdentity_DistinguishesSameTileAcrossSources`
- `Pixelometer_ExternalHostItem_ComposesWithoutSampleAnnotation`
- `CanvasControl_ConsumesExactlyOneQueryContract`

### 9.2 P2 Hardening Tests

- `QueuedCancel_DisposesClaimantRegistrations`
- `TileCompletionStorm_QueuesSingleDispatcherInvalidation`
- `SampleAnnotation_DefectPixels_DefensiveCopy`
- `SampleAnnotation_DefectPixels_InvalidLengthThrows`
- `DispatcherUnhandledException_FatalPolicy_NotAlwaysHandled`
- `Noise_IsContinuousAcrossHorizontalBoundary`
- `Noise_IsContinuousAcrossVerticalBoundary`
- `SelectMipLevel_AnisotropicScaleXGreater_UsesDemandingAxis`
- `SelectMipLevel_AnisotropicScaleYGreater_UsesDemandingAxis`
- `Pixelometer_AnisotropicMip_AgreesWithRasterizer`

### 9.3 Manual / Stress Validation

- Rapid pan/zoom for at least 30 seconds with cold-cache tile completion instrumentation.
- Repeated close while regeneration and tile work are in flight.
- Multi-source same-tile-ID fixture to validate cache and readout identity separation.
- External host scene source using non-demo item types to validate pixelometer composition.
- High-DPI and window-resize run to validate invalidation coalescing and frame buffer lifecycle.

## 10. Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R-001 | Lifecycle disposal remains implicit | Medium | High | Implement shutdown coordinator and active-operation tracking before more runtime features. |
| R-002 | Reusable contract extracted before query authority is resolved | Medium | High | Gate ICW-316 extraction on ICW-316A and single query authority. |
| R-003 | Scheduler escape hatch survives into production adapter | Medium | High | Fail fast without scheduler; source scan for bare Task.Run and CancellationToken.None in generation path. |
| R-004 | Source identity remains synthetic through first adapter | Medium | High | Make SourceId required in tile/readout/key diagnostics before adapter integration. |
| R-005 | Dispatcher exception handler hides production failures | Medium | Medium | Classify exceptions and surface fatal/degraded states. |
| R-006 | Visual continuity issues are mistaken for performance limitations | Low-Medium | Medium | Run seam tests before optimizing noise/mip paths. |

## 11. Assumptions

| ID | Assumption | Confidence | Handling |
| --- | --- | --- | --- |
| A-001 | The current concat source snapshot corresponds closely to the current working tree, but exact commit should be confirmed before filing line-specific tickets. | Medium | Tie every ticket to SHA before implementation. |
| A-002 | production viewport replacement requires host-neutral item, tile, source, and pixel readout contracts. | High | If product scope narrows to demo-only, some findings downgrade to hardening. |
| A-003 | TileWorkCoordinator is intended to become reusable infrastructure, not only an app-local demo helper. | Medium-high | If it stays private/demo-only, D5-010 and D5-011 are lower severity but still cleanup. |
| A-004 | Seamless background/noise imagery is a desired product/visual requirement. | Medium | If not required, update ICW-129/324 acceptance language. |
| A-005 | Non-uniform scale is important enough that mips must be correct under anisotropic zoom. | Medium-high | If non-uniform scale is removed/disallowed, ICW-325 changes shape. |

## 12. Open Questions

- What exact commit SHA does icw-concat-8-6-26.04-of-05 represent?
- Should SampleImageTile be declared demo-only, or should it be hardened into reusable tile infrastructure?
- Which contract should own item queries: ICanvasSceneSource, ICanvasSpatialQuerySource, or a newly split read/query pair?
- Is seamless synthetic/background noise a product requirement or a demo-only nice-to-have?
- Should dispatcher exceptions fail fast in debug/test builds?
- What minimum source identity should production adapters expose: source only, source plus layer, or source plus selected-view/revision?

## 13. Requests / Missing Evidence

- Exact Git commit SHA for each concat chunk used as evidence.
- Full source for CanvasControl.xaml.cs, CanvasFrame.cs, CanvasViewModel.cs, BackgroundTileContracts.cs, TileWorkCoordinator.cs, FrameBufferPool.Windows.cs, TileCacheBudget.cs, and CanvasPixelSample.cs at the same SHA.
- Current test files for ICW-312, ICW-315, ICW-316A, ICW-318, ICW-320, ICW-327, ICW-329, ICW-330, and frame-buffer pool tests.
- Runtime traces for close-during-generation and cold-cache tile completion storms.
- Representative external-host fixture with non-SampleAnnotation items.

## 14. Final Recommendation

**Decision: Changes Requested.** The codebase is trending in the right direction, but the next work slice should be hardening, not feature expansion. The priority order is lifecycle, coordinator disposal, generation ownership, source identity, host-neutral readout, query authority, and then visual/diagnostic hardening.

If this report is converted into tickets, file the P1 items first and gate any production viewport production adapter work on the P1 acceptance criteria. The P2 items can be batched, but they should not be ignored because several of them become public API or field-support debt after extraction.



