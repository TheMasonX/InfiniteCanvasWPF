# InfiniteCanvasWPF production viewport Replacement Master Audit and Implementation Task Plan

**Description:** Consolidated master audit synthesizing the attached audits into one secret-safe engineering report, bug backlog, architecture improvement plan, new requirements list, and agent-ready implementation plan.  
**Timestamp:** 2026-08-06 12:28 CDT  
**Author:** Copilot  
**Repository / Target:** `repository` as candidate foundation for production viewport replacement  
**Review Status:** Changes Requested  
**Overall Confidence:** 78%  
**Secret Posture:** This report uses neutral names only. It intentionally avoids credentials, customer-private data, internal URLs, and proprietary adapter names. private host adapters should map private types to the neutral contracts in non-public integration code only.  
**Page Note:** The report contains explicit page breaks for printed/PDF export and is designed as a 50+ page-equivalent handoff document.  


<div style="page-break-after: always;"></div>


## 1. Executive Summary

The synthesized decision is **changes requested**. InfiniteCanvasWPF appears valuable as a reusable WPF viewport foundation, but the attached audit corpus does not support treating it as a production viewport drop-in replacement. The repeated blockers are not cosmetic: source and revision identity are incomplete, layer parity is missing, frame and scene ownership need stronger contracts, runtime stress evidence is incomplete, and the production integration boundary needs neutral contracts that keep private product details outside reusable code.

### Top conclusions
- **Do not default-enable as a production viewport replacement.** Keep behind a feature flag until all P0 gates are closed.
- **Build the neutral contract layer first.** The next agent should implement the interfaces/classes listed in this report before continuing feature polish.
- **Preserve what is working.** The reusable control boundary, frame boundary, coalescing/cache concepts, and pixelometer non-generation invariant are useful foundations.
- **Shift the work from generic canvas polish to production viewport proof.** The next phase should produce source-qualified identity, immutable snapshots, deterministic layers, frame lease semantics, diagnostics, stress tests, and side-by-side parity evidence.
- **Treat all prior AI audits as secondary analysis.** Use them for synthesis and task generation, but re-verify against source before coding or closing findings.


<div style="page-break-after: always;"></div>


## 2. Evidence Discipline and Source Corpus

This report follows the attached code review, architecture review, bug report, peer review, and task plan formats. Claims are separated by certainty. Because the current input set is mostly prior audits rather than direct source files, this report uses the following classification:

- **Source-backed within prior report:** the prior report explicitly cites opened source or product documentation. Still recheck against current HEAD before closing.
- **Strongly inferred:** multiple audit inputs converge on the same issue, but current source was not directly re-opened in this synthesis pass.
- **Implementation requirement:** a neutral design/task requirement derived from the synthesized findings. This is prescriptive guidance, not a claim that a class already exists.
- **Request / missing evidence:** evidence required before release or once code work begins.

### Input documents
- **S1:** `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_3_2026-08-06.md` (external-source-reference) - Additional requirements-fit and lifecycle findings, including source/revision identity, layer parity, shutdown references, render scheduling, cache identity, tile generation, and pixelometer issues.
- **S2:** `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_2026-08-06.md` (external-source-reference) - Deep bug sweep covering adapter gap, 13-layer parity, synthetic source IDs, runtime concurrency, MainWindow ownership, regeneration, buffer lease, stale source revisions, cancellation, cache and pixelometer hazards.
- **S3:** `InfiniteCanvasWPF_Agent_Implementation_Guidance_Delta_5_2026-08-06.md` (external-source-reference) - Implementation guidance with neutral interfaces/classes and acceptance criteria for the next agent.
- **S4:** `InfiniteCanvasWPF_Viewport_Replacement_Master_Report_2026-08-06.md` (external-source-reference) - Prior master readiness synthesis, proposed architecture, phases, and decision to keep the repo as a foundation but not a drop-in replacement.
- **S5:** `InfiniteCanvasWPF_Viewport_Replacement_Readiness_Report.md` (external-source-reference) - Readiness report with evidence corpus, source-backed concerns, test plan, readiness gates, open questions, and requests.
- **S6:** `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_2_2026-08-06.md` (external-source-reference) - Second deep sweep emphasizing lifecycle, threading, cache/coordinator semantics, diagnostics, and process evidence discipline.
- **F1:** `peer-review-SKILL.md` (external-source-reference) - Peer review addendum and checklist.
- **F2:** `task-plan-FORMAT.md` (external-source-reference) - Task plan format with requirements, roadmap, task, test plan, risk register, assumptions, requests, and references.
- **F3:** `bug-report-FORMAT.md` (external-source-reference) - Bug report output contract with certainty separation and validation expectations.
- **F4:** `code-review-SKILL.md` (external-source-reference) - Evidence-driven code review posture, source priority, workflow, and bug/smell promotion rules.
- **F5:** `Improve-Code-Arch-SKILL.md` (external-source-reference) - Architecture-first review lenses, evidence classification, and synthesis guidance.
- **F6:** `ms-copilot-AGENT.md` (external-source-reference) - Research workflow, source verification discipline, and secret-safe constraints.


<div style="page-break-after: always;"></div>


## 3. Readiness Verdict

| Area | Verdict | Rationale | Required Gate |
|---|---:|---|---|
| Replacement readiness | **Not ready** | Production source adapters, layer parity, revision semantics, runtime stress, and parity harness are open. | All P0 closed and P1 accepted/closed. |
| Reusable control foundation | **Promising** | The audits consistently preserve the reusable control boundary and note useful frame/cache/coordinator direction. | Boundary tests prevent demo/product leakage. |
| Product parity | **Insufficient** | host application workflows exceed generic image pan/zoom. | Side-by-side parity harness on representative fixtures. |
| Runtime safety | **Unproven** | Concurrency, shutdown, frame lifetime, cache pressure, and multi-viewport stress are not yet proven. | Stress harness with repeatable logs. |
| Agent handoff quality | **Actionable after contracts** | Delta 5 provides neutral interface/class guidance, but it should be consolidated and codified. | ADAPTER_GUIDANCE.md and contract package. |


<div style="page-break-after: always;"></div>


## 4. Master Findings Index

| ID | Priority | Area | Title | Source Set |
|---|---:|---|---|---|
| MA-P0-001 | P0 | Replacement readiness | Not drop-in replacement-ready | S2, S4, S5 |
| MA-P0-002 | P0 | Source adapter contract | Production inspection source model missing | S1, S2, S3, S6 |
| MA-P0-003 | P0 | Layer parity | 13-layer stack parity unimplemented | S2, S4, S5, S6 |
| MA-P0-004 | P0 | Source identity | Synthetic source IDs leak into production-like path | S2, S3, S4 |
| MA-P0-005 | P0 | Revision identity | Stale frame guard lacks source/layer semantics | S1, S2, S3, S4, S5 |
| MA-P0-006 | P0 | Runtime validation | Concurrency and stress evidence incomplete | S1, S2, S5, S6 |
| MA-P0-007 | P0 | Frame ownership | Frame surface lease semantics not formalized | S2, S4, S5 |
| MA-P0-008 | P0 | Scene atomicity | Regeneration is not transactional | S2, S5, S6 |
| MA-P0-009 | P0 | Product workflow parity | host application workflows exceed generic canvas behavior | S4, S5 |
| MA-P0-010 | P0 | Secret-safe guidance | Agent implementation must avoid internal secret leakage | S3 |
| MA-P1-001 | P1 | MainWindow ownership | MainWindow owns production host responsibilities | S1, S2, S4, S6 |
| MA-P1-002 | P1 | Shutdown lifecycle | Async void close path is fragile | S1, S6 |
| MA-P1-003 | P1 | Shutdown lifecycle | Generation gate not awaited before disposing shared resources | S2, S6 |
| MA-P1-004 | P1 | Event references | CanvasSurface/source handlers may retain host | S1 |
| MA-P1-005 | P1 | Render scheduling | Tile generated events can flood dispatcher | S1, S3 |
| MA-P1-006 | P1 | Failure retry | Tile failure retry lacks bounded policy | S1, S3, S6 |
| MA-P1-007 | P1 | Cancellation semantics | Canceled work can still dispatch success | S2, S5 |
| MA-P1-008 | P1 | Claimant cleanup | Queued cancel path may retain claimants | S2 |
| MA-P1-009 | P1 | Locking | Reentrant coordinator/cache lock chain remains risky | S2, S3 |
| MA-P1-010 | P1 | Cache accounting | Multi-mip and resident byte accounting may be incomplete | S2 |
| MA-P1-011 | P1 | Eviction | Eviction can target active/generating content | S2 |
| MA-P1-012 | P1 | Memory governance | No shared multi-viewport budget service | S1, S2 |
| MA-P1-013 | P1 | Tile identity | Pinned/visible/bounds lookup keyed by plain tile ID | S1, S3 |
| MA-P1-014 | P1 | Tile materialization | Tile generation wiring is mutable/order-dependent | S1, S3 |
| MA-P1-015 | P1 | Frame claimant lifetime | Two-frame cancellation is convention-based | S1, S3 |
| MA-P1-016 | P1 | Pixelometer contract | Pixelometer remains sample/demo type-coupled | S2, S3, S4 |
| MA-P1-017 | P1 | Selection model | Selection should be snapshot-based not host string | S3, S4 |
| MA-P1-018 | P1 | Tooltip model | Tooltip ownership should not be WPF element-specific | S3 |
| MA-P1-019 | P1 | Hit testing | Hit-test tolerance and metadata are not sufficient | S1, S3 |
| MA-P1-020 | P1 | Spatial index | Spatial identity is too shallow | S5 |
| MA-P1-021 | P1 | Render invalidation | All invalidations share one route | S1, S3 |
| MA-P1-022 | P1 | Display settings | Display settings are mutable host fields | S3 |
| MA-P1-023 | P1 | Overlay atomicity | Raster frame and overlay plan can be accepted separately | S3 |
| MA-P1-024 | P1 | Diagnostics | Diagnostics are string-heavy not support-bundle ready | S1, S3, S6 |
| MA-P1-025 | P1 | Feature flag/fallback | Fallback boundary not defined | S3, S5 |
| MA-P1-026 | P1 | Boundary tests | Demo code may leak into reusable control | S3, S4, S5 |
| MA-P1-027 | P1 | Settings persistence | Settings failures logged but not surfaced | S6 |
| MA-P1-028 | P1 | Interest set semantics | Documentation and implementation disagree on running cancellation | S6 |
| MA-P1-029 | P1 | Dispose transition | Coordinator disposed state order is ambiguous | S6 |
| MA-P1-030 | P1 | Scene notification | SceneChanged coupled to render completion | S6 |
| MA-P2-001 | P2 | Mip selection | Anisotropic mip selection uses wrong axis | S4, S5 |
| MA-P2-002 | P2 | Background noise | Per-tile noise may seam or conflict with status | S4 |
| MA-P2-003 | P2 | Resize policy | Resize debounce latency may be too high | S1 |
| MA-P2-004 | P2 | Input abstraction | Input handler abstraction remains deferred | S3 |
| MA-P2-005 | P2 | Status model | Single-axis zoom display is incomplete | S2 |
| MA-P2-006 | P2 | Priority observability | Tile scheduling priority lacks explanation | S6 |
| MA-P2-007 | P2 | Cache diagnostic model | Cache status should be structured | S6 |
| MA-P2-008 | P2 | Process discipline | Tracker evidence cannot prove readiness | S6, F3, F4, F5, F6 |


<div style="page-break-after: always;"></div>


## 5. Detailed Bug, Improvement, and Requirement Findings

### MA-P0-001: Not drop-in replacement-ready

**Priority:** P0  
**Area:** Replacement readiness  
**Classification:** Strongly Inferred  
**Source set:** S2, S4, S5  

#### Claim
The current codebase should be treated as a reusable WPF viewport foundation, not a production viewport drop-in replacement.

#### Actual / Observed Risk
The attached audits consistently identify missing production adapters, layer parity, runtime stress evidence, source/revision semantics, and immutable snapshot boundaries.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Do not enable as default host application viewport; gate behind feature flag and side-by-side parity harness until all P0/P1 readiness gates close.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-002: Production inspection source model missing

**Priority:** P0  
**Area:** Source adapter contract  
**Classification:** Strongly Inferred  
**Source set:** S1, S2, S3, S6  

#### Claim
Generic canvas/source contracts are not sufficient for a production inspection viewport because they do not encode inspection identity, selected view identity, source health, source/layer revisions, or adapter ownership.

#### Actual / Observed Risk
Prior reports call out ICanvasSceneSource and CanvasFrame seams as generic, not production viewport host-specific. Agent guidance recommends IViewportSession, IViewportSource, ViewportSnapshot, ViewportRevisionVector, and ViewportSourceHealth.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Create a neutral viewport contract package and keep private adapters outside generic assemblies.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-003: 13-layer stack parity unimplemented

**Priority:** P0  
**Area:** Layer parity  
**Classification:** Strongly Inferred  
**Source set:** S2, S4, S5, S6  

#### Claim
production viewport layer parity remains a blocker. The current supplied evidence says host overlay paths update grid/annotation-style overlays, not a deterministic reproduction of the documented fixed layer stack.

#### Actual / Observed Risk
The audits repeatedly list alignment layer, web edges, lanes, cameras, defect images, defects, frames, selected defects, region, fiducials, film edges, slits, and labels as required parity targets.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Implement ViewportLayerRegistry, LayerOrder, IViewportLayerSource, LayerRenderPlan, and golden parity tests before replacement trials.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P0-004: Synthetic source IDs leak into production-like path

**Priority:** P0  
**Area:** Source identity  
**Classification:** Strongly Inferred  
**Source set:** S2, S3, S4  

#### Claim
The render path still constructs cache keys using a synthetic source identity in current evidence. That is acceptable only in demo fixtures, not production integration.

#### Actual / Observed Risk
The deep sweep identifies BackgroundTileCacheKey("synthetic", tile.Id, epoch, mipLevel), and agent guidance requires no production/reusable code to construct such keys.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Introduce ViewportTileKey and ITileKeyFactory; enforce source-qualified keys for cache, bounds, pinning, eviction, materialization, and diagnostics.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-005: Stale frame guard lacks source/layer semantics

**Priority:** P0  
**Area:** Revision identity  
**Classification:** Strongly Inferred  
**Source set:** S1, S2, S3, S4, S5  

#### Claim
Render request revision ordering protects against some out-of-order frame presentation, but it cannot prove the frame reflects the latest inspection source, layer, display setting, and selection state.

#### Actual / Observed Risk
Delta reports require source revisions, layer revisions, selection revisions, display settings revision, and source health in accepted frame identity.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Replace simple CanvasFrame.Revision semantics with ViewportFrameIdentity or ViewportRevisionVector carried through render, overlay, pixelometer, and diagnostics.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-006: Concurrency and stress evidence incomplete

**Priority:** P0  
**Area:** Runtime validation  
**Classification:** Strongly Inferred  
**Source set:** S1, S2, S5, S6  

#### Claim
The audits repeatedly state the concurrency candidates are source-traced but runtime reproduction and WPF runtime stress evidence remain incomplete.

#### Actual / Observed Risk
Readiness criteria require fast scroll, rapid zoom, continuous resize, close during generation, tile failure retry, stale-frame rejection, cache pressure, multi-viewport, and overnight tests.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Do not advance beyond feature-flagged prototype until runtime stress and real inspection fixture parity are repeatable.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P0-007: Frame surface lease semantics not formalized

**Priority:** P0  
**Area:** Frame ownership  
**Classification:** Strongly Inferred  
**Source set:** S2, S4, S5  

#### Claim
Mapped-surface or InteropBitmap handoff may be improved by current FrameBufferPool and composition fencing, but the production contract still needs explicit lease/retire ownership.

#### Actual / Observed Risk
Prior master and readiness reports promote FrameSurfaceLease/IFramePublisher and require that no published frame can reference overwritten or disposed memory.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Define FrameSurfaceLease, AcceptedFrameContext, IFramePublisher, and tests that prove active memory cannot be reused while retained.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-008: Regeneration is not transactional

**Priority:** P0  
**Area:** Scene atomicity  
**Classification:** Strongly Inferred  
**Source set:** S2, S5, S6  

#### Claim
Current regeneration evidence shows clearing/resetting state before new scene construction completes, so partial failure can leave the UI with no prior scene or expose an empty/partial spatial index.

#### Actual / Observed Risk
Delta 2 identifies RegenerateSceneAsync clearing frame/camera and initializing spatial state before new tiles are ready.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Build offscreen immutable SceneSnapshot/ViewportSnapshot and commit by atomic swap only after successful construction.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P0-009: host application workflows exceed generic canvas behavior

**Priority:** P0  
**Area:** Product workflow parity  
**Classification:** Strongly Inferred  
**Source set:** S4, S5  

#### Claim
production viewport replacement requirements include fast defect access, refresh, extended context, customizable queries, streaming video, 60 FPS smooth scrolling, and known operator workflows, beyond basic pan/zoom rendering.

#### Actual / Observed Risk
Readiness report and master report identify workflow parity as a P0 gate.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Create side-by-side parity harness with representative inspection fixtures before product replacement.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P0-010: Agent implementation must avoid internal secret leakage

**Priority:** P0  
**Area:** Secret-safe guidance  
**Classification:** Strongly Inferred  
**Source set:** S3  

#### Claim
The next implementation agent needs concrete interfaces/classes without exposing credentials, customer-private data, internal URLs, or proprietary adapter names.

#### Actual / Observed Risk
Agent guidance explicitly requests neutral identifiers and an ADAPTER_GUIDANCE.md that maps private types only inside internal adapters.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Keep generic contracts domain-neutral; use neutral test IDs such as source-a and layer-defects; isolate internal mappings in non-public adapter layer.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-001: MainWindow owns production host responsibilities

**Priority:** P1  
**Area:** MainWindow ownership  
**Classification:** Strongly Inferred  
**Source set:** S1, S2, S4, S6  

#### Claim
MainWindow currently owns scene state, camera, spatial index, frame buffers, coordinator, cache budget, render request tracker, frame CTS state, overlay composition, diagnostics, and demo data.

#### Actual / Observed Risk
Multiple reports call out MainWindow as retaining too much orchestration after control extraction.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Extract ViewportEngine plus DemoViewportHost and production ViewportHost. MainWindow should become composition root only.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-002: Async void close path is fragile

**Priority:** P1  
**Area:** Shutdown lifecycle  
**Classification:** Strongly Inferred  
**Source set:** S1, S6  

#### Claim
OnClosed awaits asynchronous disposal but is still an async void event path, so exceptions after await surface through dispatcher behavior and shutdown ordering remains hard to reason about.

#### Actual / Observed Risk
Delta 2 and Delta 3 call out OnClosed, render disposal, event subscriptions, and top-level try/catch gaps.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Introduce ShutdownCoordinator with explicit state transitions, guarded async Task shutdown, cancellation-first discipline, and stress tests.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-003: Generation gate not awaited before disposing shared resources

**Priority:** P1  
**Area:** Shutdown lifecycle  
**Classification:** Strongly Inferred  
**Source set:** S2, S6  

#### Claim
OnClosed cancels/disposes render/coordinator/pool/generation gate, but available evidence does not prove active regeneration has exited before shared resources can be disposed.

#### Actual / Observed Risk
Delta 2 raises this as a lifecycle finding.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Shutdown must acquire/observe generation completion before disposing the generation gate, coordinator, frame pool, lifetime CTS, scene source, and event handlers.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-004: CanvasSurface/source handlers may retain host

**Priority:** P1  
**Area:** Event references  
**Classification:** Strongly Inferred  
**Source set:** S1  

#### Claim
Constructor subscribes CanvasSurface events, Loaded, Closed, and CompositionTarget.Rendering; evidence shows some unsubscribe but not all source/surface/event references explicitly cleared.

#### Actual / Observed Risk
Delta 3 identifies missing unsubscription and clearing of SceneSource/FramePublished references.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Make control lifetime own subscriptions or detach all handlers in shutdown. Add leak tests that open/close viewports repeatedly.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-005: Tile generated events can flood dispatcher

**Priority:** P1  
**Area:** Render scheduling  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
OnTilePixelsGenerated enqueues RequestRenderAsync through Dispatcher for every generated tile event. Coalescing may exist, but event-level dispatcher pressure can still become expensive under fast scroll/cache fill.

#### Actual / Observed Risk
Delta 3 identifies per-tile render requests and recommends a dirty flag or scheduler.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Route tile completion through IRenderInvalidationQueue with reason coalescing and priority.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-006: Tile failure retry lacks bounded policy

**Priority:** P1  
**Area:** Failure retry  
**Classification:** Strongly Inferred  
**Source set:** S1, S3, S6  

#### Claim
Tile generation failure uses the same render retry path as success dirtying and no per-key backoff/suppression is visible in the audit corpus.

#### Actual / Observed Risk
Delta 2 and Delta 3 identify OnTilePixelsGenerationFailed retry churn.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add ITileRetryPolicy, TileFailureState, retry budget, failure classification, terminal fault state, and diagnostics.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-007: Canceled work can still dispatch success

**Priority:** P1  
**Area:** Cancellation semantics  
**Classification:** Strongly Inferred  
**Source set:** S2, S5  

#### Claim
TileWorkCoordinator records cancellation but can dispatch completed pixels in the known path, relying on later tile epoch checks.

#### Actual / Observed Risk
Deep sweep flags StartWorkItem wasCanceled and DispatchCompleted interaction.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add explicit canceled terminal callback and tests proving canceled claimants cannot receive success publication.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-008: Queued cancel path may retain claimants

**Priority:** P1  
**Area:** Claimant cleanup  
**Classification:** Strongly Inferred  
**Source set:** S2  

#### Claim
Prior delta notes queued cancellation removes/dispatches failure without clearly clearing claimant registrations.

#### Actual / Observed Risk
Deep sweep calls for ClearClaimants/DisposeRegistrations on all terminal paths.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Centralize terminal transition in TileWorkItem and assert registration disposal exactly once.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-009: Reentrant coordinator/cache lock chain remains risky

**Priority:** P1  
**Area:** Locking  
**Classification:** Strongly Inferred  
**Source set:** S2, S3  

#### Claim
TryReserve can lead to eviction and callback into coordinator removal while locks are held; this relies on same-thread reentrancy discipline that is fragile for future async changes.

#### Actual / Observed Risk
Deep sweep and agent guidance recommend computing eviction plan under lock and notifications outside locks.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Introduce EvictionPlan and IEvictionObserver; prohibit callbacks while cache/coordinator locks are held.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-010: Multi-mip and resident byte accounting may be incomplete

**Priority:** P1  
**Area:** Cache accounting  
**Classification:** Strongly Inferred  
**Source set:** S2  

#### Claim
Audit notes say mip payloads can add roughly a third more bytes and byte accounting must sum all resident mips and exact release paths.

#### Actual / Observed Risk
Deep sweep identifies multi-mip accounting risk.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Make ViewportCacheBudget account per ViewportTileKey and per mip with exact lease accounting and invariant tests.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-011: Eviction can target active/generating content

**Priority:** P1  
**Area:** Eviction  
**Classification:** Strongly Inferred  
**Source set:** S2  

#### Claim
Audit synthesis tracks eviction selecting active/generating tile as follow-up risk.

#### Actual / Observed Risk
Deep sweep requires in-progress residency state.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add Resident, Generating, Pinned, Visible, Retiring, and Faulted states to eviction policy.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-012: No shared multi-viewport budget service

**Priority:** P1  
**Area:** Memory governance  
**Classification:** Strongly Inferred  
**Source set:** S1, S2  

#### Claim
Cache budget is per MainWindow instance in current evidence, but host application can host multiple viewport surfaces.

#### Actual / Observed Risk
Deep sweep and Delta 3 recommend process/workspace budget service and per-viewport leases.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Build IViewportCacheBudgetService with per-viewport quotas, process ceiling, and emergency trim policy.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-013: Pinned/visible/bounds lookup keyed by plain tile ID

**Priority:** P1  
**Area:** Tile identity  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
Tile bounds, selected/visible/pinned behavior, and cache retention use demo tile IDs instead of source-qualified identities.

#### Actual / Observed Risk
Agent guidance and Delta 3 identify ViewportTileKey and IViewportTileCatalog requirements.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Replace all plain tile ID maps with ViewportTileKey or ViewportTileIdentity.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-014: Tile generation wiring is mutable/order-dependent

**Priority:** P1  
**Area:** Tile materialization  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
Tiles receive coordinator, claimant providers, token providers, and release callbacks after generation, creating half-wired lifecycle states.

#### Actual / Observed Risk
Delta 3 and agent guidance call for immutable request context.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Introduce TileMaterializationRequest with key, claimant, token, priority, and cache lease.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-015: Two-frame cancellation is convention-based

**Priority:** P1  
**Area:** Frame claimant lifetime  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
RenderFrameAsync swaps current and previous frame CTS values, making cancellation span two frames by convention rather than type contract.

#### Actual / Observed Risk
Delta 3 recommends FrameClaimantLease.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add FrameClaimantLease with tests for exact disposal order, stale frame rejection, and two-frame survival.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-016: Pixelometer remains sample/demo type-coupled

**Priority:** P1  
**Area:** Pixelometer contract  
**Classification:** Strongly Inferred  
**Source set:** S2, S3, S4  

#### Claim
Pixelometer and hover details work through the scene source but still reference sample tile/annotation semantics in host paths.

#### Actual / Observed Risk
Deep sweep identifies TryReadResidentPixel using tiles geometry and casting to SampleAnnotation, and agent guidance recommends ViewportPixelSample/LayerPixelContribution.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Move pixel readout into adapter/layer contracts with source/layer/revision-aware payload.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-017: Selection should be snapshot-based not host string

**Priority:** P1  
**Area:** Selection model  
**Classification:** Strongly Inferred  
**Source set:** S3, S4  

#### Claim
Current evidence uses selected annotation ID in host overlay update; production needs layer/source-qualified item identity and selection revision.

#### Actual / Observed Risk
Agent guidance recommends IViewportSelectionService and SelectionSnapshot keyed by ViewportItemId.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Implement selection as immutable SelectionSnapshot carried in ViewportRevisionVector.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-018: Tooltip ownership should not be WPF element-specific

**Priority:** P1  
**Area:** Tooltip model  
**Classification:** Strongly Inferred  
**Source set:** S3  

#### Claim
Overlay update attaches tooltip/selection handlers to WPF Border elements and skips non-sample items according to guidance.

#### Actual / Observed Risk
Agent guidance recommends ICanvasTooltipPayload and ITooltipContentFormatter.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Separate tooltip data from WPF presentation; generate UI through formatter from neutral payload.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-019: Hit-test tolerance and metadata are not sufficient

**Priority:** P1  
**Area:** Hit testing  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
QueryPoint hides tolerance policy and returns shallow items without full layer/source/revision/selection metadata.

#### Actual / Observed Risk
Delta 3 identifies QueryPoint tolerance and ICanvasLayerItem/ICanvasHitTarget needs.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Introduce ILayerHitTester, HitTestPolicy, HitTestResult, and explicit tolerance settings.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-020: Spatial identity is too shallow

**Priority:** P1  
**Area:** Spatial index  
**Classification:** Strongly Inferred  
**Source set:** S5  

#### Claim
Spatial entities expose bounds only in older readiness evidence, so duplicate logical IDs, tombstones, and source/layer revision semantics are not locally enforceable.

#### Actual / Observed Risk
Readiness report test plan includes SpatialIdentityTests for duplicate logical IDs, tombstones, generations, and concurrent publish behavior.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Extend spatial contracts or wrap them with ViewportSpatialEntity carrying source/layer/item/revision.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-021: All invalidations share one route

**Priority:** P1  
**Area:** Render invalidation  
**Classification:** Strongly Inferred  
**Source set:** S1, S3  

#### Claim
Viewport changes, tile events, style changes, failure retry, and regeneration all request render through one path.

#### Actual / Observed Risk
Agent guidance recommends RenderInvalidation and IRenderInvalidationQueue carrying reason, source revision, and priority.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Build priority-aware render scheduler with coalescing by reason and source revision.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-022: Display settings are mutable host fields

**Priority:** P1  
**Area:** Display settings  
**Classification:** Strongly Inferred  
**Source set:** S3  

#### Claim
Render path reads mutable host fields for display options instead of frame-stable settings.

#### Actual / Observed Risk
Agent guidance recommends ViewportDisplaySettingsSnapshot.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Capture display settings in immutable snapshot and include revision in frame identity.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-023: Raster frame and overlay plan can be accepted separately

**Priority:** P1  
**Area:** Overlay atomicity  
**Classification:** Strongly Inferred  
**Source set:** S3  

#### Claim
A raster CanvasFrame is published, then overlays update via frame-published path, creating risk of stale overlay attached to newer raster or inverse.

#### Actual / Observed Risk
Agent guidance recommends RasterFrame plus ViewportFrame or extending frame so overlay plan is accepted atomically.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Publish raster, overlay plan, snapshot, diagnostics, and revision vector as one ViewportFrame.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-024: Diagnostics are string-heavy not support-bundle ready

**Priority:** P1  
**Area:** Diagnostics  
**Classification:** Strongly Inferred  
**Source set:** S1, S3, S6  

#### Claim
CacheStatusText/StatusText and pixelometer statuses rely on formatted strings, not structured diagnostic snapshots.

#### Actual / Observed Risk
Delta 2 and Delta 3 call out CacheStatusText and Pixelometer formatted status limitations.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add ViewportDiagnosticsSnapshot with neutral IDs, counts, timings, source/layer revisions, failure reasons, and render stage telemetry.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-025: Fallback boundary not defined

**Priority:** P1  
**Area:** Feature flag/fallback  
**Classification:** Strongly Inferred  
**Source set:** S3, S5  

#### Claim
No retrieved source defines production fallback policy for replacing current viewport.

#### Actual / Observed Risk
Agent guidance recommends IViewportImplementationSelector and ViewportFallbackPolicy.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Implement feature flag, fallback selector, operator-visible fallback reason, and compatibility adapter behavior.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-026: Demo code may leak into reusable control

**Priority:** P1  
**Area:** Boundary tests  
**Classification:** Strongly Inferred  
**Source set:** S3, S4, S5  

#### Claim
Generic code must not depend on sample fixtures or private production adapter names.

#### Actual / Observed Risk
Agent guidance acceptance criteria prohibit generic code downcasts to SampleAnnotation/reference to demo types.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add project-reference tests and source scanner that fails if generic assemblies reference demo/internal adapter types.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-027: Settings failures logged but not surfaced

**Priority:** P1  
**Area:** Settings persistence  
**Classification:** Strongly Inferred  
**Source set:** S6  

#### Claim
Delta 2 notes saved settings failures are logged but not surfaced to the user.

#### Actual / Observed Risk
This degrades supportability because operators may believe settings persisted when they did not.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Emit non-blocking UI warning and structured diagnostic event for settings persistence failure.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-028: Documentation and implementation disagree on running cancellation

**Priority:** P1  
**Area:** Interest set semantics  
**Classification:** Strongly Inferred  
**Source set:** S6  

#### Claim
PublishInterestSet documentation says queued or running work outside interest is canceled, but implementation comment says running items are not canceled.

#### Actual / Observed Risk
Delta 2 flags the mismatch.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Clarify intended policy and add tests for queued/running/unclaimed paths under interest-set updates.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P1-029: Coordinator disposed state order is ambiguous

**Priority:** P1  
**Area:** Dispose transition  
**Classification:** Strongly Inferred  
**Source set:** S6  

#### Claim
TileWorkCoordinator.Dispose calls CancelAll before setting disposed per audit description, allowing in-flight Task.Run paths to enter during disposal coordination.

#### Actual / Observed Risk
Delta 2 flags shutdown storm behavior.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Set disposed transition atomically before cancellation or make transition explicit with allowed terminal callbacks.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P1-030: SceneChanged coupled to render completion

**Priority:** P1  
**Area:** Scene notification  
**Classification:** Strongly Inferred  
**Source set:** S6  

#### Claim
SceneChanged is raised only after RequestRenderAsync completes, tying source change notification to render execution.

#### Actual / Observed Risk
Delta 2 recommends raising from committed scene swap and making render a subscriber effect.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Publish committed snapshot event first; scheduler observes it and requests render separately.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-001: Anisotropic mip selection uses wrong axis

**Priority:** P2  
**Area:** Mip selection  
**Classification:** Inferred / Improvement  
**Source set:** S4, S5  

#### Claim
Prior master report identifies ICW-325 as proposed because SelectMipLevel uses Math.Min under non-uniform scale.

#### Actual / Observed Risk
This can produce wrong level choice for asymmetric horizontal/vertical scale scaling.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Fix mip policy and add asymmetric scale tests for each axis and display percent behavior.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-002: Per-tile noise may seam or conflict with status

**Priority:** P2  
**Area:** Background noise  
**Classification:** Inferred / Improvement  
**Source set:** S4  

#### Claim
Prior master report identifies conflict between per-tile seed/local normalization and seamless worldspace sampling claims.

#### Actual / Observed Risk
This matters if synthetic/noise backgrounds remain in demo/diagnostic visualization.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Resolve requirement: either implement worldspace-continuous noise or document non-seamless variance.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P2-003: Resize debounce latency may be too high

**Priority:** P2  
**Area:** Resize policy  
**Classification:** Inferred / Improvement  
**Source set:** S1  

#### Claim
Delta 3 notes resize uses a 150 ms DispatcherTimer, which may add latency or stale frames during continuous resize.

#### Actual / Observed Risk
This should be explicit and tested, not accidental.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Define ResizeRenderPolicy with high-DPI and continuous resize tests.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-004: Input handler abstraction remains deferred

**Priority:** P2  
**Area:** Input abstraction  
**Classification:** Inferred / Improvement  
**Source set:** S3  

#### Claim
Agent guidance says input abstraction should be promoted if host application interaction parity is in scope.

#### Actual / Observed Risk
host application replacement likely needs command routing for selection, navigation, hover, context menu, keyboard, and multi-tool interactions.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Define IViewportInputHandler, ViewportInputContext, and ViewportCommand when parity scope includes interactions.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-005: Single-axis zoom display is incomplete

**Priority:** P2  
**Area:** Status model  
**Classification:** Inferred / Improvement  
**Source set:** S2  

#### Claim
Deep sweep notes StatusText reports one zoom axis and recommends X/Y scale or host application horizontal/vertical scale units.

#### Actual / Observed Risk
Non-uniform scale and operator units require explicit presentation.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Expose ViewportScaleStatus with horizontal/vertical scale terms, units, and display policy.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


### MA-P2-006: Tile scheduling priority lacks explanation

**Priority:** P2  
**Area:** Priority observability  
**Classification:** Inferred / Improvement  
**Source set:** S6  

#### Claim
Delta 2 notes priority does not expose why a tile was ordered lower beyond rank/distance/mip/sequence.

#### Actual / Observed Risk
This harms support diagnosis of slow fill-in under cache pressure.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Add optional priority trace/report with reason components.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-007: Cache status should be structured

**Priority:** P2  
**Area:** Cache diagnostic model  
**Classification:** Inferred / Improvement  
**Source set:** S6  

#### Claim
CacheStatusText is driven by DescribeStatus string rather than typed diagnostics.

#### Actual / Observed Risk
Operators/support need counts, bytes, hit/miss, eviction reasons, pins, visible tiles, faults, and revisions.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Expose typed cache diagnostics and serialize secret-safe snapshots.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.
### MA-P2-008: Tracker evidence cannot prove readiness

**Priority:** P2  
**Area:** Process discipline  
**Classification:** Inferred / Improvement  
**Source set:** S6, F3, F4, F5, F6  

#### Claim
Delta 2 notes duplicate IDs/status divergence and warns against relying on active tasks alone.

#### Actual / Observed Risk
Task trackers and AI reports are secondary only.

#### Why this matters for production viewport
production viewport is a production inspection viewport, not only a generic canvas. The risk is severe when state, identity, source validity, frame ownership, or operator workflow parity cannot be proven under live updates, failure, shutdown, and support scenarios. This finding should be used to guide both code implementation and validation design.

#### Recommendation
Gate every readiness claim on source/test/parity evidence, not task status.

#### Counterarguments and Downgrade Conditions
- This finding should be downgraded if current HEAD already implements the recommended contract and tests prove the failure cannot happen.
- If the behavior is intentionally scoped out of the first feature-flagged prototype, record that decision with owner, fallback behavior, and acceptance criteria.
- If the source cited by prior audits was stale, reconcile the exact current code path and update the finding instead of carrying it forward unchanged.

#### Validation Criteria
- Add at least one failing unit, integration, stress, or parity test that reproduces the risk shape.
- Fix the implementation through a neutral contract or isolated adapter boundary.
- Prove the fix with repeatable test output and a short engineering note or ADR when behavior changes.
- Reclassify as source-backed only after direct source/test evidence is available.


<div style="page-break-after: always;"></div>


## 6. Consolidated Requirements

### Functional Requirements
| ID | Requirement | Priority | Acceptance Criteria |
|---|---|---:|---|
| FR-001 | Provide neutral source, layer, item, and revision identity types. | Must | All production viewport APIs use neutral IDs and revision vectors. |
| FR-002 | Support deterministic ordered layer plans for the documented layer stack. | Must | Golden tests prove ordering and visibility toggles for every layer. |
| FR-003 | Support source-qualified tile cache keys. | Must | No reusable/production code path constructs synthetic keys except demo fixtures. |
| FR-004 | Publish raster, overlays, diagnostics, and snapshot atomically. | Must | A stale raster cannot update overlays and a stale overlay cannot attach to newer raster. |
| FR-005 | Provide source/layer/revision-aware selection and tooltip model. | Must | Selection, tooltip, and hit-test outputs include item/layer/source identity. |
| FR-006 | Provide layer-aware pixelometer payload. | Must | Pixel samples include source revision, sampled mip, layer contributions, and composite policy. |
| FR-007 | Support fallback selector and feature flag. | Must | Production can disable ICW and return to existing viewport behavior. |
| FR-008 | Emit secret-safe diagnostics snapshots. | Must | Diagnostics include neutral IDs, counts, timings, failure reasons, and no private data. |

### Non-Functional Requirements
| ID | Requirement | Priority | Acceptance Criteria |
|---|---|---:|---|
| NFR-001 | No production frame can reference overwritten/disposed memory. | Must | FrameSurfaceLease tests prove active surfaces are not reused. |
| NFR-002 | No render can mix scene generations. | Must | Snapshot generation tests prove active render observes one committed generation. |
| NFR-003 | Stress evidence must cover fast scroll, zoom, resize, close, failures, cache pressure, and multi-viewport. | Must | Stress harness produces repeatable pass/fail logs. |
| NFR-004 | Generic assemblies remain product-agnostic. | Must | Project reference/source scanning tests fail on product-specific dependencies. |
| NFR-005 | Runtime diagnostics support remote support. | Should | Support bundle includes serialized viewport state and recent render/tile events. |


<div style="page-break-after: always;"></div>


## 7. Proposed Neutral Architecture

### 7.1 Component layout

```text
Product host application Host
  -> ViewportImplementationSelector
      -> ExistingViewportAdapter
      -> InfiniteCanvasViewportHost
          -> InfiniteCanvas.Controls.CanvasControl
          -> InfiniteCanvas.Core.Viewport.ViewportEngine
              -> RenderInvalidationQueue
              -> FramePublisher
              -> FrameSurfaceLeaseProvider
              -> TileWorkCoordinator
              -> ViewportCacheBudgetService
              -> ViewportDiagnosticsService
          -> private host adapters Layer
              -> InspectionSourceAdapter
              -> BackgroundTileSourceAdapter
              -> LayerSourceSetAdapter
              -> ViewSelectionSnapshotProvider
```

### 7.2 Boundary rules
- `InfiniteCanvas.Controls` must remain WPF-control-focused and product-agnostic.
- `InfiniteCanvas.Core.Viewport` owns neutral identity, revision, snapshot, frame, layer, hit-test, selection, tile, cache, scheduler, and diagnostics contracts.
- Demo code may use synthetic fixtures, but reusable and production code must not construct synthetic source keys directly.
- Product adapters map private product concepts to neutral records only inside internal integration assemblies.
- Diagnostics export neutral IDs and counts only. No paths, credentials, customer-specific records, or internal URLs are emitted.


<div style="page-break-after: always;"></div>


## 8. Agent-Ready Interface and Class Skeleton

The next agent can use the following neutral names. This skeleton is intentionally not tied to private product types.

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


<div style="page-break-after: always;"></div>


## 9. Roadmap

| Phase | Goal | Deliverables | Exit Criteria |
|---|---|---|---|
| Phase 0 | Evidence lock and baseline | Capture exact GitHub commit, source snapshot, target branch, and reconcile all prior AI-audit claims against primary source before coding. | Baseline metadata recorded; unknowns moved to Requests; no finding promoted without primary evidence. |
| Phase 1 | Contract package | Create neutral identity, revision, snapshot, source-health, tile-key, layer, hit-test, selection, pixelometer, diagnostics, and frame contracts. | Package compiles independently; no private adapter names; public API review passes. |
| Phase 2 | Demo extraction | Move demo source and sample item generation out of MainWindow and out of reusable control packages. | MainWindow is composition root; DemoSceneSource implements neutral contracts. |
| Phase 3 | Viewport engine | Extract ViewportEngine with render scheduling, invalidation queue, accepted frame handling, lifecycle, and diagnostics. | Engine tests run without WPF window and without demo model downcasts. |
| Phase 4 | Frame surface leasing | Introduce FrameSurfaceLease, AcceptedFrameContext, and IFramePublisher. | No active surface reuse; publish/retire rules are test-proven. |
| Phase 5 | Scene snapshot atomicity | Implement immutable ViewportSnapshot/SceneSnapshot and swap-only-on-success regeneration. | Failed regeneration leaves prior scene visible; no mixed generation reads. |
| Phase 6 | Tile/cache subsystem | Source-qualify keys, add tile catalog, cache budget service, retry policy, and terminal claimant cleanup. | Cache and coordinator tests prove exact-once leases and cancellation semantics. |
| Phase 7 | Layer/render plan | Implement ViewportLayerRegistry and deterministic LayerRenderPlan. | Golden tests prove all layers, ordering, visibility, stale rejection, and overlay atomicity. |
| Phase 8 | Interaction model | Move selection, tooltip, hit test, input, and pixelometer to neutral services. | No host string IDs or sample downcasts remain in production/control paths. |
| Phase 9 | Diagnostics and support | Implement ViewportDiagnosticsSnapshot, telemetry events, cache/status snapshots, and secret-safe JSON export. | Support bundle includes neutral IDs only and enough counts/timings/failures to diagnose field issues. |
| Phase 10 | Adapter slice | Implement internal adapter mapping current product data to neutral contracts behind feature flag. | Adapter integration tests run on representative fixtures and can fall back safely. |
| Phase 11 | Parity harness | Run side-by-side old/new viewport comparison for real inspection workflows and the full layer stack. | Screenshot/data parity gates pass or deviations are recorded with owner and acceptance. |
| Phase 12 | Release hardening | Run long-haul, multi-monitor/high-DPI, live/pause/catch-up, source loss/reconnect, and memory pressure tests. | All P0 closed; P1 closed or accepted with fallback; release decision documented. |


<div style="page-break-after: always;"></div>


## 10. Detailed Task Plan

### TASK-001: Not drop-in replacement-ready

**Priority:** P0  
**Area:** Replacement readiness  
**Depends on:** None  
**Related finding:** MA-P0-001  

#### Implementation Steps
- Implement: Do not enable as default host application viewport; gate behind feature flag and side-by-side parity harness until all P0/P1 readiness gates close.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-002: Production inspection source model missing

**Priority:** P0  
**Area:** Source adapter contract  
**Depends on:** None  
**Related finding:** MA-P0-002  

#### Implementation Steps
- Implement: Create a neutral viewport contract package and keep private adapters outside generic assemblies.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-003: 13-layer stack parity unimplemented

**Priority:** P0  
**Area:** Layer parity  
**Depends on:** None  
**Related finding:** MA-P0-003  

#### Implementation Steps
- Implement: Implement ViewportLayerRegistry, LayerOrder, IViewportLayerSource, LayerRenderPlan, and golden parity tests before replacement trials.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-004: Synthetic source IDs leak into production-like path

**Priority:** P0  
**Area:** Source identity  
**Depends on:** TASK-003  
**Related finding:** MA-P0-004  

#### Implementation Steps
- Implement: Introduce ViewportTileKey and ITileKeyFactory; enforce source-qualified keys for cache, bounds, pinning, eviction, materialization, and diagnostics.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-005: Stale frame guard lacks source/layer semantics

**Priority:** P0  
**Area:** Revision identity  
**Depends on:** TASK-004  
**Related finding:** MA-P0-005  

#### Implementation Steps
- Implement: Replace simple CanvasFrame.Revision semantics with ViewportFrameIdentity or ViewportRevisionVector carried through render, overlay, pixelometer, and diagnostics.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-006: Concurrency and stress evidence incomplete

**Priority:** P0  
**Area:** Runtime validation  
**Depends on:** TASK-005  
**Related finding:** MA-P0-006  

#### Implementation Steps
- Implement: Do not advance beyond feature-flagged prototype until runtime stress and real inspection fixture parity are repeatable.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-007: Frame surface lease semantics not formalized

**Priority:** P0  
**Area:** Frame ownership  
**Depends on:** TASK-006  
**Related finding:** MA-P0-007  

#### Implementation Steps
- Implement: Define FrameSurfaceLease, AcceptedFrameContext, IFramePublisher, and tests that prove active memory cannot be reused while retained.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-008: Regeneration is not transactional

**Priority:** P0  
**Area:** Scene atomicity  
**Depends on:** TASK-007  
**Related finding:** MA-P0-008  

#### Implementation Steps
- Implement: Build offscreen immutable SceneSnapshot/ViewportSnapshot and commit by atomic swap only after successful construction.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-009: host application workflows exceed generic canvas behavior

**Priority:** P0  
**Area:** Product workflow parity  
**Depends on:** TASK-008  
**Related finding:** MA-P0-009  

#### Implementation Steps
- Implement: Create side-by-side parity harness with representative inspection fixtures before product replacement.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-010: Agent implementation must avoid internal secret leakage

**Priority:** P0  
**Area:** Secret-safe guidance  
**Depends on:** TASK-009  
**Related finding:** MA-P0-010  

#### Implementation Steps
- Implement: Keep generic contracts domain-neutral; use neutral test IDs such as source-a and layer-defects; isolate internal mappings in non-public adapter layer.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-011: MainWindow owns production host responsibilities

**Priority:** P1  
**Area:** MainWindow ownership  
**Depends on:** TASK-010  
**Related finding:** MA-P1-001  

#### Implementation Steps
- Implement: Extract ViewportEngine plus DemoViewportHost and production ViewportHost. MainWindow should become composition root only.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-012: Async void close path is fragile

**Priority:** P1  
**Area:** Shutdown lifecycle  
**Depends on:** TASK-011  
**Related finding:** MA-P1-002  

#### Implementation Steps
- Implement: Introduce ShutdownCoordinator with explicit state transitions, guarded async Task shutdown, cancellation-first discipline, and stress tests.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-013: Generation gate not awaited before disposing shared resources

**Priority:** P1  
**Area:** Shutdown lifecycle  
**Depends on:** TASK-012  
**Related finding:** MA-P1-003  

#### Implementation Steps
- Implement: Shutdown must acquire/observe generation completion before disposing the generation gate, coordinator, frame pool, lifetime CTS, scene source, and event handlers.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-014: CanvasSurface/source handlers may retain host

**Priority:** P1  
**Area:** Event references  
**Depends on:** TASK-013  
**Related finding:** MA-P1-004  

#### Implementation Steps
- Implement: Make control lifetime own subscriptions or detach all handlers in shutdown. Add leak tests that open/close viewports repeatedly.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-015: Tile generated events can flood dispatcher

**Priority:** P1  
**Area:** Render scheduling  
**Depends on:** TASK-014  
**Related finding:** MA-P1-005  

#### Implementation Steps
- Implement: Route tile completion through IRenderInvalidationQueue with reason coalescing and priority.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-016: Tile failure retry lacks bounded policy

**Priority:** P1  
**Area:** Failure retry  
**Depends on:** TASK-015  
**Related finding:** MA-P1-006  

#### Implementation Steps
- Implement: Add ITileRetryPolicy, TileFailureState, retry budget, failure classification, terminal fault state, and diagnostics.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-017: Canceled work can still dispatch success

**Priority:** P1  
**Area:** Cancellation semantics  
**Depends on:** TASK-016  
**Related finding:** MA-P1-007  

#### Implementation Steps
- Implement: Add explicit canceled terminal callback and tests proving canceled claimants cannot receive success publication.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-018: Queued cancel path may retain claimants

**Priority:** P1  
**Area:** Claimant cleanup  
**Depends on:** TASK-017  
**Related finding:** MA-P1-008  

#### Implementation Steps
- Implement: Centralize terminal transition in TileWorkItem and assert registration disposal exactly once.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-019: Reentrant coordinator/cache lock chain remains risky

**Priority:** P1  
**Area:** Locking  
**Depends on:** TASK-018  
**Related finding:** MA-P1-009  

#### Implementation Steps
- Implement: Introduce EvictionPlan and IEvictionObserver; prohibit callbacks while cache/coordinator locks are held.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-020: Multi-mip and resident byte accounting may be incomplete

**Priority:** P1  
**Area:** Cache accounting  
**Depends on:** TASK-019  
**Related finding:** MA-P1-010  

#### Implementation Steps
- Implement: Make ViewportCacheBudget account per ViewportTileKey and per mip with exact lease accounting and invariant tests.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-021: Eviction can target active/generating content

**Priority:** P1  
**Area:** Eviction  
**Depends on:** TASK-020  
**Related finding:** MA-P1-011  

#### Implementation Steps
- Implement: Add Resident, Generating, Pinned, Visible, Retiring, and Faulted states to eviction policy.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-022: No shared multi-viewport budget service

**Priority:** P1  
**Area:** Memory governance  
**Depends on:** TASK-021  
**Related finding:** MA-P1-012  

#### Implementation Steps
- Implement: Build IViewportCacheBudgetService with per-viewport quotas, process ceiling, and emergency trim policy.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-023: Pinned/visible/bounds lookup keyed by plain tile ID

**Priority:** P1  
**Area:** Tile identity  
**Depends on:** TASK-022  
**Related finding:** MA-P1-013  

#### Implementation Steps
- Implement: Replace all plain tile ID maps with ViewportTileKey or ViewportTileIdentity.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-024: Tile generation wiring is mutable/order-dependent

**Priority:** P1  
**Area:** Tile materialization  
**Depends on:** TASK-023  
**Related finding:** MA-P1-014  

#### Implementation Steps
- Implement: Introduce TileMaterializationRequest with key, claimant, token, priority, and cache lease.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-025: Two-frame cancellation is convention-based

**Priority:** P1  
**Area:** Frame claimant lifetime  
**Depends on:** TASK-024  
**Related finding:** MA-P1-015  

#### Implementation Steps
- Implement: Add FrameClaimantLease with tests for exact disposal order, stale frame rejection, and two-frame survival.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-026: Pixelometer remains sample/demo type-coupled

**Priority:** P1  
**Area:** Pixelometer contract  
**Depends on:** TASK-025  
**Related finding:** MA-P1-016  

#### Implementation Steps
- Implement: Move pixel readout into adapter/layer contracts with source/layer/revision-aware payload.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-027: Selection should be snapshot-based not host string

**Priority:** P1  
**Area:** Selection model  
**Depends on:** TASK-026  
**Related finding:** MA-P1-017  

#### Implementation Steps
- Implement: Implement selection as immutable SelectionSnapshot carried in ViewportRevisionVector.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-028: Tooltip ownership should not be WPF element-specific

**Priority:** P1  
**Area:** Tooltip model  
**Depends on:** TASK-027  
**Related finding:** MA-P1-018  

#### Implementation Steps
- Implement: Separate tooltip data from WPF presentation; generate UI through formatter from neutral payload.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-029: Hit-test tolerance and metadata are not sufficient

**Priority:** P1  
**Area:** Hit testing  
**Depends on:** TASK-028  
**Related finding:** MA-P1-019  

#### Implementation Steps
- Implement: Introduce ILayerHitTester, HitTestPolicy, HitTestResult, and explicit tolerance settings.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-030: Spatial identity is too shallow

**Priority:** P1  
**Area:** Spatial index  
**Depends on:** TASK-029  
**Related finding:** MA-P1-020  

#### Implementation Steps
- Implement: Extend spatial contracts or wrap them with ViewportSpatialEntity carrying source/layer/item/revision.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-031: All invalidations share one route

**Priority:** P1  
**Area:** Render invalidation  
**Depends on:** TASK-030  
**Related finding:** MA-P1-021  

#### Implementation Steps
- Implement: Build priority-aware render scheduler with coalescing by reason and source revision.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-032: Display settings are mutable host fields

**Priority:** P1  
**Area:** Display settings  
**Depends on:** TASK-031  
**Related finding:** MA-P1-022  

#### Implementation Steps
- Implement: Capture display settings in immutable snapshot and include revision in frame identity.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-033: Raster frame and overlay plan can be accepted separately

**Priority:** P1  
**Area:** Overlay atomicity  
**Depends on:** TASK-032  
**Related finding:** MA-P1-023  

#### Implementation Steps
- Implement: Publish raster, overlay plan, snapshot, diagnostics, and revision vector as one ViewportFrame.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-034: Diagnostics are string-heavy not support-bundle ready

**Priority:** P1  
**Area:** Diagnostics  
**Depends on:** TASK-033  
**Related finding:** MA-P1-024  

#### Implementation Steps
- Implement: Add ViewportDiagnosticsSnapshot with neutral IDs, counts, timings, source/layer revisions, failure reasons, and render stage telemetry.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-035: Fallback boundary not defined

**Priority:** P1  
**Area:** Feature flag/fallback  
**Depends on:** TASK-034  
**Related finding:** MA-P1-025  

#### Implementation Steps
- Implement: Implement feature flag, fallback selector, operator-visible fallback reason, and compatibility adapter behavior.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-036: Demo code may leak into reusable control

**Priority:** P1  
**Area:** Boundary tests  
**Depends on:** TASK-035  
**Related finding:** MA-P1-026  

#### Implementation Steps
- Implement: Add project-reference tests and source scanner that fails if generic assemblies reference demo/internal adapter types.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-037: Settings failures logged but not surfaced

**Priority:** P1  
**Area:** Settings persistence  
**Depends on:** TASK-036  
**Related finding:** MA-P1-027  

#### Implementation Steps
- Implement: Emit non-blocking UI warning and structured diagnostic event for settings persistence failure.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-038: Documentation and implementation disagree on running cancellation

**Priority:** P1  
**Area:** Interest set semantics  
**Depends on:** TASK-037  
**Related finding:** MA-P1-028  

#### Implementation Steps
- Implement: Clarify intended policy and add tests for queued/running/unclaimed paths under interest-set updates.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-039: Coordinator disposed state order is ambiguous

**Priority:** P1  
**Area:** Dispose transition  
**Depends on:** TASK-038  
**Related finding:** MA-P1-029  

#### Implementation Steps
- Implement: Set disposed transition atomically before cancellation or make transition explicit with allowed terminal callbacks.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-040: SceneChanged coupled to render completion

**Priority:** P1  
**Area:** Scene notification  
**Depends on:** TASK-039  
**Related finding:** MA-P1-030  

#### Implementation Steps
- Implement: Publish committed snapshot event first; scheduler observes it and requests render separately.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-041: Anisotropic mip selection uses wrong axis

**Priority:** P2  
**Area:** Mip selection  
**Depends on:** TASK-040  
**Related finding:** MA-P2-001  

#### Implementation Steps
- Implement: Fix mip policy and add asymmetric scale tests for each axis and display percent behavior.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-042: Per-tile noise may seam or conflict with status

**Priority:** P2  
**Area:** Background noise  
**Depends on:** TASK-041  
**Related finding:** MA-P2-002  

#### Implementation Steps
- Implement: Resolve requirement: either implement worldspace-continuous noise or document non-seamless variance.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-043: Resize debounce latency may be too high

**Priority:** P2  
**Area:** Resize policy  
**Depends on:** TASK-042  
**Related finding:** MA-P2-003  

#### Implementation Steps
- Implement: Define ResizeRenderPolicy with high-DPI and continuous resize tests.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-044: Input handler abstraction remains deferred

**Priority:** P2  
**Area:** Input abstraction  
**Depends on:** TASK-043  
**Related finding:** MA-P2-004  

#### Implementation Steps
- Implement: Define IViewportInputHandler, ViewportInputContext, and ViewportCommand when parity scope includes interactions.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-045: Single-axis zoom display is incomplete

**Priority:** P2  
**Area:** Status model  
**Depends on:** TASK-044  
**Related finding:** MA-P2-005  

#### Implementation Steps
- Implement: Expose ViewportScaleStatus with horizontal/vertical scale terms, units, and display policy.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-046: Tile scheduling priority lacks explanation

**Priority:** P2  
**Area:** Priority observability  
**Depends on:** TASK-045  
**Related finding:** MA-P2-006  

#### Implementation Steps
- Implement: Add optional priority trace/report with reason components.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-047: Cache status should be structured

**Priority:** P2  
**Area:** Cache diagnostic model  
**Depends on:** TASK-046  
**Related finding:** MA-P2-007  

#### Implementation Steps
- Implement: Expose typed cache diagnostics and serialize secret-safe snapshots.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-048: Tracker evidence cannot prove readiness

**Priority:** P2  
**Area:** Process discipline  
**Depends on:** TASK-047  
**Related finding:** MA-P2-008  

#### Implementation Steps
- Implement: Gate every readiness claim on source/test/parity evidence, not task status.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-101: Create ViewportSourceId, ViewportLayerId, ViewportItemId, ViewportRevision, and ViewportRevisionVector

**Priority:** P0  
**Area:** Contracts  
**Depends on:** TASK-002  
**Related finding:** MA-P0-002  

#### Implementation Steps
- Implement: Implement value types and revision vector equality/hash semantics with tests.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-102: Create ViewportSnapshot and source health model

**Priority:** P0  
**Area:** Contracts  
**Depends on:** TASK-101  
**Related finding:** MA-P0-005  

#### Implementation Steps
- Implement: Capture scene bounds, camera, display settings, layer visibility, source health, and revisions in immutable object graph.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-103: Create ViewportTileKey and tile catalog

**Priority:** P0  
**Area:** Tiles  
**Depends on:** TASK-101  
**Related finding:** MA-P0-004  

#### Implementation Steps
- Implement: Replace plain string tile IDs in cache, bounds, pinning, materialization, and diagnostics.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-104: Create IViewportLayerSource and ViewportLayerRegistry

**Priority:** P0  
**Area:** Layers  
**Depends on:** TASK-101  
**Related finding:** MA-P0-003  

#### Implementation Steps
- Implement: Register deterministic layer order and produce LayerRenderPlan from snapshot.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-105: Create IViewportVisualItem, ILayerHitTester, HitTestPolicy, HitTestResult

**Priority:** P1  
**Area:** Interactions  
**Depends on:** TASK-104  
**Related finding:** MA-P1-019  

#### Implementation Steps
- Implement: Move hit testing and WPF element identity out of MainWindow.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-106: Create IViewportSelectionService and SelectionSnapshot

**Priority:** P1  
**Area:** Interactions  
**Depends on:** TASK-105  
**Related finding:** MA-P1-017  

#### Implementation Steps
- Implement: Selection state must be source/layer/item/revision-aware and immutable per frame.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-107: Create ICanvasTooltipPayload and ITooltipContentFormatter

**Priority:** P1  
**Area:** Interactions  
**Depends on:** TASK-105  
**Related finding:** MA-P1-018  

#### Implementation Steps
- Implement: Separate tooltip data from WPF display elements.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-108: Create ViewportPixelSample and LayerPixelContribution

**Priority:** P1  
**Area:** Pixelometer  
**Depends on:** TASK-104  
**Related finding:** MA-P1-016  

#### Implementation Steps
- Implement: Pixelometer must report source, layer, revision, mip sampled, and composite policy.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-109: Create RasterFrame, ViewportFrame, AcceptedFrameContext, FrameSurfaceLease, IFramePublisher

**Priority:** P0  
**Area:** Frames  
**Depends on:** TASK-102  
**Related finding:** MA-P0-007  

#### Implementation Steps
- Implement: Frame publication must atomically bind raster, overlays, snapshot, and diagnostics.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-110: Create RenderInvalidation, RenderInvalidationReason, IRenderInvalidationQueue, IRenderScheduler

**Priority:** P1  
**Area:** Scheduling  
**Depends on:** TASK-103  
**Related finding:** MA-P1-021  

#### Implementation Steps
- Implement: Render requests must be reasoned, coalesced, prioritized, and revision-aware.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-111: Create ViewportDiagnosticsSnapshot and stage telemetry records

**Priority:** P1  
**Area:** Diagnostics  
**Depends on:** TASK-109  
**Related finding:** MA-P1-024  

#### Implementation Steps
- Implement: Diagnostics must be structured and secret-safe.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-112: Create ADAPTER_GUIDANCE.md

**Priority:** P2  
**Area:** Guidance  
**Depends on:** TASK-101  
**Related finding:** MA-P0-010  

#### Implementation Steps
- Implement: Document neutral names, public/private boundary rules, and test data hygiene.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.


<div style="page-break-after: always;"></div>


### TASK-113: Create IViewportImplementationSelector and ViewportFallbackPolicy

**Priority:** P1  
**Area:** Fallback  
**Depends on:** TASK-102  
**Related finding:** MA-P1-025  

#### Implementation Steps
- Implement: Production integration must support controlled fallback.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-114: Create side-by-side parity harness

**Priority:** P0  
**Area:** Tests  
**Depends on:** TASK-104  
**Related finding:** MA-P0-009  

#### Implementation Steps
- Implement: Compare legacy and ICW viewport output on representative fixtures.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
### TASK-115: Create runtime stress harness

**Priority:** P0  
**Area:** Stress  
**Depends on:** TASK-110  
**Related finding:** MA-P0-006  

#### Implementation Steps
- Implement: Cover fast scroll, rapid zoom, continuous resize, close during generation, retry, stale rejection, cache pressure, and multi-viewport.
- Keep names neutral and avoid product-private identifiers in generic assemblies.
- Add tests before or alongside implementation so the old behavior fails and the new behavior passes.
- Update engineering note, ADR, or adapter guidance if public contracts or lifecycle behavior change.

#### Acceptance Criteria
- The task has a direct automated verification path or a documented manual parity check.
- The implementation does not introduce demo/product dependency leakage into reusable packages.
- Diagnostics emitted by the implementation are secret-safe and include enough neutral IDs/revisions to debug failures.
- Any unresolved uncertainty is captured in Requests rather than silently assumed.

#### Rollback
- Leave existing viewport implementation selectable through fallback policy.
- If the task changes public contract behavior, preserve adapter shims until callers are migrated.
## 11. Test Plan

### Unit Tests
- FrameSurfaceLeaseTests: active surface is not reused while retained; rapid publish/retire cannot invalidate current frame.
- SceneSnapshotTests: failed regeneration leaves prior scene intact; active render observes one generation only.
- SpatialIdentityTests: duplicate logical IDs replace/deduplicate; tombstones remove prior records; publish returns included generation.
- TileCoordinatorTests: canceled claimant cannot receive success; reservation lease disposed exactly once; failed work releases budget.
- ViewportRevisionVectorTests: stale source, layer, display, or selection revision rejects raster and overlay updates.
- LayerRenderPlanTests: layer order is deterministic; visibility toggles affect only intended layer entries.
- HitTestTests: hit-test tolerance is explicit; result includes item, layer, source, z-order, and revision.
- SelectionSnapshotTests: selection is immutable and source/layer/item-qualified.
- PixelometerTests: resident pixel read does not trigger generation; samples include layer contributions and mip sampled.
- RenderInvalidationQueueTests: repeated tile completions coalesce; failure retry uses backoff and terminal fault states.
- DiagnosticsSnapshotTests: diagnostic JSON contains neutral IDs only and no private paths or adapter names.
- BoundaryTests: generic projects do not reference demo-only or product-specific types.

### Integration Tests
- Load representative inspection and compare current viewport vs ICW viewport screenshots/data snapshots.
- Pan/zoom rapidly while source revisions change and verify stale frames are rejected.
- Switch selected views while tile generation is active and verify no mixed revision frame is accepted.
- Toggle every layer in the target layer stack and verify ordering/visibility parity.
- Exercise alignment layer/static/live/pause/catch-up paths as scoped for the first feature-flagged prototype.
- Simulate source loss and reconnect; verify fallback/operator recovery behavior.
- Run high-DPI, multi-monitor, minimized/restored, and continuous resize tests.
- Run multi-viewport cache pressure with shared process budget and per-viewport leases.

### Stress / Soak / Fault Injection
- Fast scroll for long inspections with cache pressure.
- Rapid zoom in/out under tile generation.
- Continuous resize across DPI changes.
- Close during scene generation, tile generation, and frame publication.
- Tile materialization failure storm and retry backoff.
- Stale-frame rejection under source and layer revision churn.
- Overnight live-mode memory and handle leak test.
- Multi-viewport open/close loop to detect event/reference leaks.


<div style="page-break-after: always;"></div>


## 12. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| R-001: Overfitting adapter to current mutable product ViewModels instead of immutable snapshots | High | High | Keep adapter thin; neutral snapshots are the only core boundary. |
| R-002: Feature flag hides missing parity instead of proving it | Medium | High | Feature flag must require parity evidence and explicit fallback behavior. |
| R-003: Zero-copy optimization is preserved before correctness is proven | Medium | High | Prioritize lease semantics and correctness before throughput tuning. |
| R-004: Demo sample types leak into reusable packages | Medium | Medium | Boundary tests and source scanners reject product/demo dependencies. |
| R-005: Stress tests are too synthetic | Medium | High | Use representative fixtures and real WPF runtime harness. |
| R-006: Diagnostics omit the IDs needed for remote support | Medium | Medium | Define secret-safe support schema early and assert it in tests. |
| R-007: Layer parity is treated as approximate | Medium | High | Golden layer-order and visibility tests are mandatory. |
| R-008: Shutdown bugs only appear under field timing | Medium | High | Add close-during-generation and multi-viewport soak loops. |


<div style="page-break-after: always;"></div>


## 13. Assumptions

- **A-001 (Medium):** The attached audits reflect a close-enough snapshot of the current repo for synthesis, but exact current HEAD must still be verified before coding. Handling: Use as planning input only; re-open current source before closing findings.
- **A-002 (High):** production viewport replacement scope includes more than a standalone image viewer. Handling: Product docs and prior reports support workflow and layer parity requirements.
- **A-003 (High):** Layer parity is required for operator confidence. Handling: Treat as P0 until product owner accepts a narrower prototype scope.
- **A-004 (High):** Correctness and supportability outrank zero-copy performance during first integration slice. Handling: Do not optimize away frame lease/snapshot invariants.
- **A-005 (Medium-high):** The current repository can evolve rather than be discarded. Handling: Preserve useful foundation while extracting contracts and hardening lifecycle.

## 14. Open Questions
- What exact GitHub commit SHA is the review baseline?
- Which product branch/version is the integration target?
- Is the first prototype inspection view-only, inspection view-only, Snapshot-only, or all viewport surfaces?
- Is alignment layer parity required in V1 or can it be explicitly deferred behind feature flag?
- Which representative inspection fixtures are acceptable for parity harness?
- What are the memory ceilings per viewport and per process on target systems?
- What operator-visible fallback behavior is desired when source/adapter mismatch is detected?
- Which current layers are mandatory for the first customer-facing acceptance scenario?
- Can the current viewport host ecosystem be extended or must the new control replace the current WPF viewport entirely?
- What diagnostics must be included in first-line support bundle for field triage?


<div style="page-break-after: always;"></div>


## 15. Requests / Missing Evidence
- Exact GitHub commit SHA and branch for the InfiniteCanvasWPF baseline.
- Full source tree export or accessible clone for the current commit.
- Current target product branch and commit for the production viewport integration point.
- Current viewport/layer/display/view-selection source files from target branch.
- Representative inspection fixtures covering raster, overlays, selected items, labels, fiducials, film edges, lanes, frames, and source updates.
- Current acceptance criteria for frame rate, memory, alignment layer/live mode, source reconnect, operator fallback, and diagnostics.
- Decision on whether first integration is compatibility adapter, side-by-side viewport, or direct replacement.
- Pinned list of customer workflows that must not regress.

## 16. Peer Review Checklist
- [ ] Every major claim has a source set and certainty classification.
- [ ] Prior AI reports are treated as secondary unless backed by source/test evidence.
- [ ] P0/P1 findings have actionable recommendations and validation criteria.
- [ ] Neutral contract names avoid secret or proprietary leakage.
- [ ] Task plan includes rollback/fallback for production integration.
- [ ] Test plan includes unit, integration, parity, stress, and diagnostics checks.
- [ ] Open evidence gaps are listed explicitly.
- [ ] Final decision is explicit: changes requested before replacement readiness.


<div style="page-break-after: always;"></div>


## 17. Final Decision

**Decision:** Changes Requested.  
**Rationale:** The repository is a promising foundation but not a production replacement until neutral contracts, source-qualified identity, deterministic layer parity, immutable snapshots, frame-surface leasing, coordinator/cache correctness, diagnostics, fallback, and runtime parity evidence are implemented and verified.  
**Next Best Action:** Start with Phase 0 and Phase 1. The next agent should implement the neutral contract package and tests first, then use that boundary to extract demo code and begin production adapter work.  

## 18. Appendix A - Source Mapping
- `S1` = `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_3_2026-08-06.md` / `external-source-reference` / Additional requirements-fit and lifecycle findings, including source/revision identity, layer parity, shutdown references, render scheduling, cache identity, tile generation, and pixelometer issues.
- `S2` = `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_2026-08-06.md` / `external-source-reference` / Deep bug sweep covering adapter gap, 13-layer parity, synthetic source IDs, runtime concurrency, MainWindow ownership, regeneration, buffer lease, stale source revisions, cancellation, cache and pixelometer hazards.
- `S3` = `InfiniteCanvasWPF_Agent_Implementation_Guidance_Delta_5_2026-08-06.md` / `external-source-reference` / Implementation guidance with neutral interfaces/classes and acceptance criteria for the next agent.
- `S4` = `InfiniteCanvasWPF_Viewport_Replacement_Master_Report_2026-08-06.md` / `external-source-reference` / Prior master readiness synthesis, proposed architecture, phases, and decision to keep the repo as a foundation but not a drop-in replacement.
- `S5` = `InfiniteCanvasWPF_Viewport_Replacement_Readiness_Report.md` / `external-source-reference` / Readiness report with evidence corpus, source-backed concerns, test plan, readiness gates, open questions, and requests.
- `S6` = `InfiniteCanvasWPF_Deep_Bug_Sweep_Delta_2_2026-08-06.md` / `external-source-reference` / Second deep sweep emphasizing lifecycle, threading, cache/coordinator semantics, diagnostics, and process evidence discipline.
- `F1` = `peer-review-SKILL.md` / `external-source-reference` / Peer review addendum and checklist.
- `F2` = `task-plan-FORMAT.md` / `external-source-reference` / Task plan format with requirements, roadmap, task, test plan, risk register, assumptions, requests, and references.
- `F3` = `bug-report-FORMAT.md` / `external-source-reference` / Bug report output contract with certainty separation and validation expectations.
- `F4` = `code-review-SKILL.md` / `external-source-reference` / Evidence-driven code review posture, source priority, workflow, and bug/smell promotion rules.
- `F5` = `Improve-Code-Arch-SKILL.md` / `external-source-reference` / Architecture-first review lenses, evidence classification, and synthesis guidance.
- `F6` = `ms-copilot-AGENT.md` / `external-source-reference` / Research workflow, source verification discipline, and secret-safe constraints.

## 19. Appendix B - Secret-Safe Agent Instructions
- Use neutral IDs such as `source-a`, `source-b`, `layer-defects`, `layer-labels`, `item-1`, and `frame-42` in tests and docs.
- Do not include credentials, customer-private data, internal URLs, or proprietary adapter names in reusable code or public guidance.
- Internal adapter code may map private product concepts to the neutral contracts, but that mapping should not appear in generic libraries.
- Error messages and diagnostics should include neutral identifiers, counts, revisions, stage names, and failure classes only.
- Treat prior reports, task trackers, KBs, and AI-generated analysis as directional until source/test evidence verifies them.


<div style="page-break-after: always;"></div>


## 20. Appendix C - Task Traceability Matrix
| Task | Priority | Area | Finding | Verification Theme |
|---|---:|---|---|---|
| TASK-001 | P0 | Replacement readiness | MA-P0-001 | unit/integration/stress/parity test plus source review |
| TASK-002 | P0 | Source adapter contract | MA-P0-002 | unit/integration/stress/parity test plus source review |
| TASK-003 | P0 | Layer parity | MA-P0-003 | unit/integration/stress/parity test plus source review |
| TASK-004 | P0 | Source identity | MA-P0-004 | unit/integration/stress/parity test plus source review |
| TASK-005 | P0 | Revision identity | MA-P0-005 | unit/integration/stress/parity test plus source review |
| TASK-006 | P0 | Runtime validation | MA-P0-006 | unit/integration/stress/parity test plus source review |
| TASK-007 | P0 | Frame ownership | MA-P0-007 | unit/integration/stress/parity test plus source review |
| TASK-008 | P0 | Scene atomicity | MA-P0-008 | unit/integration/stress/parity test plus source review |
| TASK-009 | P0 | Product workflow parity | MA-P0-009 | unit/integration/stress/parity test plus source review |
| TASK-010 | P0 | Secret-safe guidance | MA-P0-010 | unit/integration/stress/parity test plus source review |
| TASK-011 | P1 | MainWindow ownership | MA-P1-001 | unit/integration/stress/parity test plus source review |
| TASK-012 | P1 | Shutdown lifecycle | MA-P1-002 | unit/integration/stress/parity test plus source review |
| TASK-013 | P1 | Shutdown lifecycle | MA-P1-003 | unit/integration/stress/parity test plus source review |
| TASK-014 | P1 | Event references | MA-P1-004 | unit/integration/stress/parity test plus source review |
| TASK-015 | P1 | Render scheduling | MA-P1-005 | unit/integration/stress/parity test plus source review |
| TASK-016 | P1 | Failure retry | MA-P1-006 | unit/integration/stress/parity test plus source review |
| TASK-017 | P1 | Cancellation semantics | MA-P1-007 | unit/integration/stress/parity test plus source review |
| TASK-018 | P1 | Claimant cleanup | MA-P1-008 | unit/integration/stress/parity test plus source review |
| TASK-019 | P1 | Locking | MA-P1-009 | unit/integration/stress/parity test plus source review |
| TASK-020 | P1 | Cache accounting | MA-P1-010 | unit/integration/stress/parity test plus source review |
| TASK-021 | P1 | Eviction | MA-P1-011 | unit/integration/stress/parity test plus source review |
| TASK-022 | P1 | Memory governance | MA-P1-012 | unit/integration/stress/parity test plus source review |
| TASK-023 | P1 | Tile identity | MA-P1-013 | unit/integration/stress/parity test plus source review |
| TASK-024 | P1 | Tile materialization | MA-P1-014 | unit/integration/stress/parity test plus source review |
| TASK-025 | P1 | Frame claimant lifetime | MA-P1-015 | unit/integration/stress/parity test plus source review |
| TASK-026 | P1 | Pixelometer contract | MA-P1-016 | unit/integration/stress/parity test plus source review |
| TASK-027 | P1 | Selection model | MA-P1-017 | unit/integration/stress/parity test plus source review |
| TASK-028 | P1 | Tooltip model | MA-P1-018 | unit/integration/stress/parity test plus source review |
| TASK-029 | P1 | Hit testing | MA-P1-019 | unit/integration/stress/parity test plus source review |
| TASK-030 | P1 | Spatial index | MA-P1-020 | unit/integration/stress/parity test plus source review |
| TASK-031 | P1 | Render invalidation | MA-P1-021 | unit/integration/stress/parity test plus source review |
| TASK-032 | P1 | Display settings | MA-P1-022 | unit/integration/stress/parity test plus source review |
| TASK-033 | P1 | Overlay atomicity | MA-P1-023 | unit/integration/stress/parity test plus source review |
| TASK-034 | P1 | Diagnostics | MA-P1-024 | unit/integration/stress/parity test plus source review |
| TASK-035 | P1 | Feature flag/fallback | MA-P1-025 | unit/integration/stress/parity test plus source review |
| TASK-036 | P1 | Boundary tests | MA-P1-026 | unit/integration/stress/parity test plus source review |
| TASK-037 | P1 | Settings persistence | MA-P1-027 | unit/integration/stress/parity test plus source review |
| TASK-038 | P1 | Interest set semantics | MA-P1-028 | unit/integration/stress/parity test plus source review |
| TASK-039 | P1 | Dispose transition | MA-P1-029 | unit/integration/stress/parity test plus source review |
| TASK-040 | P1 | Scene notification | MA-P1-030 | unit/integration/stress/parity test plus source review |
| TASK-041 | P2 | Mip selection | MA-P2-001 | unit/integration/stress/parity test plus source review |
| TASK-042 | P2 | Background noise | MA-P2-002 | unit/integration/stress/parity test plus source review |
| TASK-043 | P2 | Resize policy | MA-P2-003 | unit/integration/stress/parity test plus source review |
| TASK-044 | P2 | Input abstraction | MA-P2-004 | unit/integration/stress/parity test plus source review |
| TASK-045 | P2 | Status model | MA-P2-005 | unit/integration/stress/parity test plus source review |
| TASK-046 | P2 | Priority observability | MA-P2-006 | unit/integration/stress/parity test plus source review |
| TASK-047 | P2 | Cache diagnostic model | MA-P2-007 | unit/integration/stress/parity test plus source review |
| TASK-048 | P2 | Process discipline | MA-P2-008 | unit/integration/stress/parity test plus source review |
| TASK-101 | P0 | Contracts | MA-P0-002 | unit/integration/stress/parity test plus source review |
| TASK-102 | P0 | Contracts | MA-P0-005 | unit/integration/stress/parity test plus source review |
| TASK-103 | P0 | Tiles | MA-P0-004 | unit/integration/stress/parity test plus source review |
| TASK-104 | P0 | Layers | MA-P0-003 | unit/integration/stress/parity test plus source review |
| TASK-105 | P1 | Interactions | MA-P1-019 | unit/integration/stress/parity test plus source review |
| TASK-106 | P1 | Interactions | MA-P1-017 | unit/integration/stress/parity test plus source review |
| TASK-107 | P1 | Interactions | MA-P1-018 | unit/integration/stress/parity test plus source review |
| TASK-108 | P1 | Pixelometer | MA-P1-016 | unit/integration/stress/parity test plus source review |
| TASK-109 | P0 | Frames | MA-P0-007 | unit/integration/stress/parity test plus source review |
| TASK-110 | P1 | Scheduling | MA-P1-021 | unit/integration/stress/parity test plus source review |
| TASK-111 | P1 | Diagnostics | MA-P1-024 | unit/integration/stress/parity test plus source review |
| TASK-112 | P2 | Guidance | MA-P0-010 | unit/integration/stress/parity test plus source review |
| TASK-113 | P1 | Fallback | MA-P1-025 | unit/integration/stress/parity test plus source review |
| TASK-114 | P0 | Tests | MA-P0-009 | unit/integration/stress/parity test plus source review |
| TASK-115 | P0 | Stress | MA-P0-006 | unit/integration/stress/parity test plus source review |

