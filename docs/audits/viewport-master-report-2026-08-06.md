# InfiniteCanvasWPF Master Readiness Report for production viewport Replacement
**Description:** Synthesis of the GitHub target request, provided chunked source files, production viewport requirements, and the supplied architecture/code-review/bug-report/task-plan formats.  
**Timestamp:** 2026-08-06 09:54 CDT  
**Author:** Copilot  
**Repository:** repository plus provided chunks  
**Status:** Changes Requested for direct replacement; continue as reusable viewport foundation.  
**Confidence:** 86%
---
## Abstract
The current provided chunks show meaningful maturity: reusable control extraction, CanvasFrame boundary, composition-fenced frame handoff, priority tile scheduling, cancellation hardening, and resident pixelometer behavior. That is enough to treat InfiniteCanvasWPF as a serious foundation. It is not enough to declare it a production viewport replacement because production viewport host adapters, layer parity, host application workflows, source/revision identity, and runtime stress validation remain unproven.
---
## Evidence Corpus
- <File>external requirement source</File> lists the chunk/file coverage, including MainWindow.xaml.cs, TileWorkCoordinator.cs, BackgroundTileContracts.cs, CanvasFrame.cs, ICanvasSceneSource.cs, and ICanvasItem.cs. Ref: external-source-reference.
- <File>external requirement source</File> describes the intended architecture: zero-copy InteropBitmap/file-mapping rendering, immutable STRtree indexing, async MVVM orchestration, and sub-16 ms rendering goals. Ref: external-source-reference.
- <File>external requirement source</File> shows many items as Done, including ICW-312, ICW-315, ICW-316, ICW-316A, ICW-317, ICW-318, ICW-320, ICW-321, ICW-326, ICW-327, ICW-328, ICW-329, and ICW-330; it also shows ICW-313, ICW-314, ICW-324, and ICW-325 as Proposed/future-scoped. Ref: external-source-reference.
- <File>external requirement source</File> shows MainWindow implements ICanvasSceneSource, wires CanvasSurface, owns FrameBufferPool, TileWorkCoordinator, render request tracking, frame tile CTS state, synthetic tile scene state, and RenderFrameAsync. Ref: external-source-reference.
- <File>external requirement source</File> shows TileWorkCoordinator has priority queue scheduling, ViewportInterestSet, ICacheReservation admission, cancel/coalesce semantics, and documented caller-held-lock contracts. Ref: external-source-reference.
- <File>external requirement source</File> contains audit reconciliation and later handoff material, including F-010 background noise seam conflict, F-011 anisotropic mip selection, Wave H assembly extraction, and Wave I cancellation/revision/allocation fixes. Ref: external-source-reference.
- <File>external requirement source</File> states production viewport goals: faster defect access, inspection file load time, fast defect image refresh, extended context, customizable queries, full-resolution streaming video, 60 fps smooth scrolling, and known gaps. Ref: external-source-reference.
- <File>external requirement source</File> describes the production viewport LayerManager 13-layer stack: alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels. Ref: external-source-reference.
- <File>external requirement source</File> describes the WPF viewport ecosystem, viewport control, LayerManagerFactory, MainViewModel, and snapshot controller gap. Ref: external-source-reference.
- <File>external requirement source</File>, <File>external requirement source</File>, <File>external requirement source</File>, and <File>external requirement source</File> define the requested output styles. Refs: external-source-reference, external-source-reference, external-source-reference, external-source-reference.

---
## Executive Summary
**Verdict:** not ready as a drop-in replacement. The correct next step is a host application integration slice, not generic canvas polish. The supplied chunks strongly suggest the prior buffer-reuse and shell-flash concerns have been addressed through FrameBufferPool, persistent shell, and composition fencing. The remaining blocking work is product integration: source adapters, 13-layer viewport parity, selection/tooltip ownership, real inspection fixtures, and stress validation.

### Top Next Steps
- Define production viewport host source/revision adapters.
- Replace `synthetic` cache identity in production paths.
- Define ViewportLayerStack with 13 ordered layers.
- Split and implement ICW-314.
- Resolve ICW-325.
- Build side-by-side host application parity harness.

---
## Findings Overview
| ID | Priority | Area | Claim |
|---|---:|---|---|
| F-001 | P0 | Replacement readiness | Not drop-in ready yet, but materially more mature than the prior snapshot. |
| F-002 | P1 | Reusable control boundary | The canvas component is now a separate WPF library, which is a major positive readiness step. |
| F-003 | P0 | production viewport host adapter gap | Generic scene/source seams exist; production viewport host inspection, alignment layer, and overlay source adapters are not shown. |
| F-004 | P0 | Layer parity gap | production viewport replacement must implement or preserve the LayerManager layer stack. |
| F-005 | P1 | Synthetic source identity leakage | The current demo render path still creates BackgroundTileCacheKey with SourceId "synthetic". |
| F-006 | P1 | Scene lifecycle boundary | MainWindow still mutates many host fields during regeneration rather than committing an immutable SceneSnapshot. |
| F-007 | P1 | Frame surface handoff | FrameBufferPool and composition fencing are recorded as fixes for flash/band issues, but host application integration still needs formal lease semantics. |
| F-008 | P1 | Stale frame guard | CanvasFrame.Revision is now recorded as behaviorally wired, but source revisions are still needed for production viewport host data. |
| F-009 | P1 | Selection and tooltip ownership | ICW-314 remains proposed and is explicitly called out as the priority functional slice for web-inspection viewport behavior. |
| F-010 | P2 | Anisotropic mip bug | ICW-325 remains proposed because SelectMipLevel uses Math.Min under non-uniform scale. |
| F-011 | P2 | Background noise seam/status conflict | ICW-324 remains proposed because per-tile noise seed/local normalization conflict with seamless worldspace sampling claims. |
| F-012 | P1 | Runtime stress gap | The audit reconciliation explicitly says no runtime reproduction was run for concurrency candidates. |
| F-013 | P1 | MainWindow still owns too much | Even after extraction, MainWindow still owns demo scene source, render pipeline, buffer handoff, cache budget, coordinator, and overlays. |
| F-014 | P1 | Pixelometer improvement to preserve | Hover pixelometer no longer initiates tile generation according to task notes and source comments. |
| F-015 | P1 | Cache lease model improved | The coordinator now accepts ICacheReservation leases rather than only boolean admission. |
| F-016 | P0 | host application workflow parity | production viewport goals exceed generic canvas rendering. |

---
## Detailed Findings
### F-001: Replacement readiness
**Priority:** P0  
**Claim:** Not drop-in ready yet, but materially more mature than the prior snapshot.

**Evidence:** Completed control extraction, frame boundary, composition fencing, and coordinator hardening are recorded; production viewport host adapters and 13-layer parity are not proven.

**Recommendation:** Keep as foundation behind feature flag; do not default into production viewport.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-002: Reusable control boundary
**Priority:** P1  
**Claim:** The canvas component is now a separate WPF library, which is a major positive readiness step.

**Evidence:** Wave H says another app can reference it, implement Core source interfaces, and publish a frame; it has no App, Rendering, or Spatial reference.

**Recommendation:** Preserve this boundary throughout host application integration.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-003: production viewport host adapter gap
**Priority:** P0  
**Claim:** Generic scene/source seams exist; production viewport host inspection, alignment layer, and overlay source adapters are not shown.

**Evidence:** ICW-312 added ICanvasItem/ICanvasSceneSource, but host application requirements require streaming video, defect workflows, and 13 layers.

**Recommendation:** Create ViewportInspectionSource, ViewportBackgroundTileSource, and ViewportOverlaySourceSet.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-004: Layer parity gap
**Priority:** P0  
**Claim:** production viewport replacement must implement or preserve the LayerManager layer stack.

**Evidence:** The host application layer stack source lists 13 layers; ICW evidence is generic raster/items/frame publication.

**Recommendation:** Create ViewportLayerStack and golden parity tests.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-005: Synthetic source identity leakage
**Priority:** P1  
**Claim:** The current demo render path still creates BackgroundTileCacheKey with SourceId "synthetic".

**Evidence:** MainWindow RenderFrameAsync adds visible tile keys with BackgroundTileCacheKey("synthetic", _tiles[i].Id, epoch, mipLevel).

**Recommendation:** Replace with adapter-provided source IDs before production integration.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-006: Scene lifecycle boundary
**Priority:** P1  
**Claim:** MainWindow still mutates many host fields during regeneration rather than committing an immutable SceneSnapshot.

**Evidence:** RegenerateSceneAsync clears frame, initializes spatial state, cancels tile work, disposes pools, awaits GenerateSet, then repopulates fields.

**Recommendation:** Create SceneSnapshot/RenderScene and atomic swap after successful construction.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-007: Frame surface handoff
**Priority:** P1  
**Claim:** FrameBufferPool and composition fencing are recorded as fixes for flash/band issues, but host application integration still needs formal lease semantics.

**Evidence:** ICW-318 records two CompositionTarget.Rendering passes before reuse; MainWindow calls OnCompositionFrame.

**Recommendation:** Promote to IFrameSurfaceLease/IFramePublisher contract.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-008: Stale frame guard
**Priority:** P1  
**Claim:** CanvasFrame.Revision is now recorded as behaviorally wired, but source revisions are still needed for production viewport host data.

**Evidence:** Wave I states RenderRequestTracker request version is threaded into CanvasFrame and stale frames are discarded.

**Recommendation:** Add production viewport host source revision vector into frame/snapshot identity.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-009: Selection and tooltip ownership
**Priority:** P1  
**Claim:** ICW-314 remains proposed and is explicitly called out as the priority functional slice for web-inspection viewport behavior.

**Evidence:** Task notes say MainWindow still owns selected annotation ID, DeferredAnnotationToolTip, and selection writeback.

**Recommendation:** Extend item/hit/tooltip contracts and move selection/hover into control/service.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-010: Anisotropic mip bug
**Priority:** P2  
**Claim:** ICW-325 remains proposed because SelectMipLevel uses Math.Min under non-uniform scale.

**Evidence:** ICW-325 notes ADR-0005 requires the binding axis to be the larger scale.

**Recommendation:** Fix mip level selection and add asymmetric tests.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-011: Background noise seam/status conflict
**Priority:** P2  
**Claim:** ICW-324 remains proposed because per-tile noise seed/local normalization conflict with seamless worldspace sampling claims.

**Evidence:** Ticket states per-tile seed and local min/max normalization defeat continuous seams, and ICW-129 status diverges.

**Recommendation:** Resolve requirement before code change, then test seam or document variance.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-012: Runtime stress gap
**Priority:** P1  
**Claim:** The audit reconciliation explicitly says no runtime reproduction was run for concurrency candidates.

**Evidence:** Mechanism tracing and tests exist, but runtime stress is still a gap.

**Recommendation:** Add fast-scroll, cancellation-storm, multi-viewport, and overnight tests.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-013: MainWindow still owns too much
**Priority:** P1  
**Claim:** Even after extraction, MainWindow still owns demo scene source, render pipeline, buffer handoff, cache budget, coordinator, and overlays.

**Evidence:** ICW-315 handoff says render pipeline stays in host, including RenderFrameAsync, back-buffer lifecycle, tile coordinator, cache budget, epoch guard, interest-set computation, and FrameBufferPool handoff.

**Recommendation:** Extract ViewportEngine/ViewportHost.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-014: Pixelometer improvement to preserve
**Priority:** P1  
**Claim:** Hover pixelometer no longer initiates tile generation according to task notes and source comments.

**Evidence:** ICW-312 closed ICW-P0-PIXELOMETER-READOUT; MainWindow.TryReadResidentPixel comments non-blocking resident read.

**Recommendation:** Keep as hard invariant in host application adapter tests.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-015: Cache lease model improved
**Priority:** P1  
**Claim:** The coordinator now accepts ICacheReservation leases rather than only boolean admission.

**Evidence:** BackgroundTileContracts defines ICacheReservation and TileWorkCoordinator Request accepts Func<BackgroundTileCacheKey, ICacheReservation?>.

**Recommendation:** Add multi-viewport budget tests and exact-once disposal validation.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

### F-016: host application workflow parity
**Priority:** P0  
**Claim:** production viewport goals exceed generic canvas rendering.

**Evidence:** host application docs identify fast defect access, full-resolution streaming video, smooth scrolling, and known feature gaps.

**Recommendation:** Build side-by-side viewport parity harness using real inspection fixtures.

**Validation Criteria:** add a focused regression test, an integration test where applicable, and a task tracker update with one status and one validation command.

---
## Architecture Assessment
### Strengths
- Reusable WPF control boundary exists.
- CanvasFrame boundary exists.
- Composition handoff has been hardened.
- Coordinator and cache admission are more mature.
- The repo has unusually good task/audit/ADR discipline.

### Remaining Architectural Gaps
- host application source adapters are not proven.
- 13-layer production viewport host parity is not implemented by evidence.
- MainWindow remains too much of the render host template.
- Selection/tooltip ownership is still proposed.
- Runtime concurrency validation remains incomplete.

### Proposed Architecture
```text
production viewport Host
  -> ViewportHost
      -> InfiniteCanvas.Controls.CanvasControl
      -> ViewportEngine
          -> RenderRequestTracker
          -> FrameBufferPool / FrameSurfaceLease
          -> TileWorkCoordinator
          -> TileCacheBudget
          -> ViewportLayerRenderPlanBuilder
      -> production viewport hostAdapter Layer
          -> ViewportInspectionSource
          -> ViewportBackgroundTileSource
          -> ViewportOverlaySourceSet
          -> ViewportViewSelectionSnapshotProvider
```

---
## Super Detailed Implementation Plan
### Phase 0 - Evidence lock
**Tasks:**
- Record exact GitHub commit SHA and production viewport host target branch.
- Map concat chunks to repo paths and current HEAD.
- Reconcile all prior findings against current source.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 1 - Preserve control boundary
**Tasks:**
- Keep InfiniteCanvas.Controls product-agnostic.
- Add boundary tests for references and public API.
- Prevent App/Rendering/Spatial references from leaking into the control.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 2 - Production host seam
**Tasks:**
- Extract ViewportEngine/ViewportHost from MainWindow patterns.
- Move demo generation behind AppDemo host.
- Run engine tests without MainWindow.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 3 - production viewport host adapters
**Tasks:**
- Define inspection/source/revision model.
- Implement background tile/alignment layer source contracts.
- Replace synthetic cache source IDs.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 4 - Layer parity
**Tasks:**
- Define ordered ViewportLayerStack.
- Implement 13 layer slots.
- Add golden-image and toggle tests.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 5 - Scene snapshots
**Tasks:**
- Introduce SceneSnapshot and ViewportFrameSnapshot.
- Build scenes off to side.
- Atomically swap only after successful construction.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 6 - Tile/cancellation/cache stress
**Tasks:**
- Validate leases exactly once.
- Stress re-coalesce and cancellation storms.
- Benchmark priority queue with inspection-like tiles.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 7 - Mip/zoom correctness
**Tasks:**
- Resolve ICW-325.
- Add non-uniform camera tests.
- Expose horizontal/vertical scale style zoom state.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 8 - Selection/tooltip
**Tasks:**
- Split ICW-314 into selection/hit testing and tooltip payload.
- Extend ICanvasItem or add hit-target contracts.
- Remove demo-only downcasts from reusable paths.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 9 - Pixelometer parity
**Tasks:**
- Preserve resident-only behavior.
- Define composite vs layer readout.
- Add host application unit/axis units labels.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 10 - host application parity harness
**Tasks:**
- Render old and new viewport side-by-side.
- Compare layer visibility, coordinates, selection, pixelometer, and defect context.
- Track deltas as tickets.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 11 - Reliability/diagnostics
**Tasks:**
- Log source revision, frame revision, buffer ID, cache key, layer status, and drop reasons.
- Add source disconnect/reconnect tests.
- Add support bundle hooks.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

### Phase 12 - Release gate
**Tasks:**
- Run overnight live-mode stress.
- Run large inspections and rapid defect switching.
- Document fallback and rollout criteria.

**Deliverables:** source changes, tests, task tracker update, and ADR/update if behavior changes.

**Exit Criteria:** no product-specific dependency leaks into generic control, no synthetic source identity in production paths, all claims backed by tests or parity harness results.

---
## Test Strategy
### Unit
- CanvasFrame revision discard.
- TileWorkCoordinator re-coalesce and cancellation.
- ICacheReservation exact-once disposal.
- BackgroundTileMipPolicy asymmetric zoom.
- Pixelometer resident-only read.
- SceneSnapshot atomic swap.

### Integration
- Demo host through InfiniteCanvas.Controls.
- host application adapter fixture rendering.
- Layer toggle parity.
- Selection/hit-test/tooltip.
- Source disconnect/reconnect.

### Stress
- Fast scroll.
- Cancellation storms.
- Multi-viewport cache pressure.
- High-DPI/multi-monitor.
- Overnight live-mode.
---
## Risk Register
| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| production viewport host adapter complexity exceeds generic contracts | High | High | Build adapter first. |
| Layer parity found late | High | High | Implement layer contract early. |
| MainWindow copied into host application | Medium | High | Extract ViewportEngine. |
| Runtime concurrency bug escapes unit tests | Medium | High | Add stress harness. |
| Synthetic source identity leaks | Medium | High | Source scan and adapter-only keys. |
---
## Final Recommendation
Do not use InfiniteCanvasWPF as a direct production viewport replacement yet. Use it as a strong foundation for a feature-flagged replacement path after the host application adapter, 13-layer parity, ICW-314, ICW-325, scene snapshot, runtime stress, and side-by-side parity gates are complete.

---
## Appendix A - Machine-Readable Backlog
```text
P0 production viewport host source adapter gap
P0 13-layer ViewportLayerStack parity
P0 Synthetic source identity removal
P0 host application workflow parity harness
P1 SceneSnapshot / ViewportFrameSnapshot
P1 ICW-314 selection and tooltip ownership
P1 Production ViewportEngine extraction
P1 Runtime stress validation
P1 Multi-viewport cache governance
P2 ICW-325 anisotropic mip selection
P2 ICW-324 noise seam/status reconciliation
P3 ICW-313 input handler abstraction
```


