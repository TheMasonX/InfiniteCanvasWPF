# Audit Synthesis Report: 22-Audit Reconciliation at HEAD 84a0cdb

**Description:** Reconcile 22 external audits against the current source at HEAD, discard stale claims, and turn the surviving findings into a collision-free ICW backlog with council-reviewed dispositions.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1` (working tree HEAD)
**Latest commit:** `84a0cdb` - `feat(canvas): inject canvas data sources and migrate to CanvasFrame boundary`
**ID Hash:** `icw-audit-synthesis-26-08-04-84a0cdb`
**Author:** InfiniteCanvas Agent (Copilot)
**Timestamp:** 2026-08-04 22:15 US Central
**Review mode:** full reconciliation
**Scope:** 22 audit files under `docs/audits/`, current trackers, requirements registry, ADRs, and source at HEAD. Excludes all audits not on the supplied input list.

## Executive Summary

This run reconciled 22 external audit files against the repository at HEAD `84a0cdb`. Three extraction subagents read every audit, built a source ledger, extracted 85 distinct candidate claims, and verified each against the working tree. A three-seat council then reviewed the surviving candidates and produced task actions.

Result counts:

- Candidates extracted: 85.
- Confirmed: 63. Partially confirmed: 10. Refuted: 8. Unverified: 1. Duplicate disposition: 4.
- Already tracked at HEAD: 46. Net-new candidates: 29, consolidated into 10 new ticket actions and 12 existing-ticket updates.
- New tickets created: ICW-316A, ICW-319, ICW-320, ICW-321, ICW-322, ICW-323, ICW-324, ICW-325, ICW-326. ICW-316 is rescoped to the physical-move phase.
- Refuted and recorded so they are not re-filed: 8 claims (C2-006, C2-011, C2-014, C2-023, C2-025, C3-001, C3-003, C3-013, C3-014, C3-016).

Highest-risk result: the duplicate item-query authority between `ICanvasSceneSource` and `ICanvasSpatialQuerySource` (`QueryVisible` on both), which must be resolved before ICW-314 builds hit-testing and before the ICW-316 assembly move publishes the split-brain contract as library API.

Material provenance corrections: delta-6's headline finding (permanent loss of cancellation registration, ICW-204) is refuted; its valid core survives as the bounded cancel-and-re-request window in ICW-320. The `ICW-129` noise work is status-divergent (Done vs In Progress vs missing in task tracker) and its "seamless worldspace sampling" acceptance criterion is un-met. The `ICW-008/062/063` publish-hardening tickets are a three-file near-duplicate family.

Validation limits: the reconciliation is source-traced at HEAD for every code-path claim. No runtime reproduction was run for the concurrency candidates; those rest on mechanism tracing and the council's proposed regression tests.

## Review Method and Coverage

- Fixed point verified with `git rev-parse HEAD` before delegation.
- Three extraction subagents read all 22 audits, the trackers, the requirements registry, relevant ADRs, and source at HEAD. Each candidate was dispositioned with repository-relative `file:line` evidence.
- Three council seats (Viewport/Rendering, Coordinator/Concurrency, Implementation Sequencing/Corpus) independently re-traced the highest-impact candidates and produced scoped task actions with acceptance criteria.
- Validation commands for the proposed work are listed per finding. No implementation was performed.
- Not inspected in depth: benchmark result files, `BenchmarkDotNet.Artifacts/`, profiler captures, and the FastNoise2 submodule internals. The run did not re-run the test suites; tracker evidence from HEAD records was used instead.

## Table of Findings

Ranked by severity. Severity and confidence are independent.

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task | Sources |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F-001 | Duplicate item-query authority on canvas source contracts | Spec | Update | Confirmed | P1 | 95% | ICW-312 + ICW-316A gate | S1, S2, S3, S15 |
| F-002 | Split ICW-316 into harden (316A) then move (316B) | Spec | Create | Confirmed | P1 | 90% | ICW-316A + rescope ICW-316 | S1, S2, S3, S15 |
| F-003 | `CanvasFrame` claims snapshot semantics but borrows mutable state | Spec | Create | Confirmed | P2 | 92% | ICW-316A | S1, S3 |
| F-004 | `CanvasViewModel` public setters permit impossible frame states | Spec | Create | Confirmed | P2 | 95% | ICW-316A | S1, S3 |
| F-005 | Raw WPF element surface leaks from `CanvasControl` | Standards | Create | Confirmed | P2 | 95% | ICW-319 | S2, S7, S9, S10 |
| F-006 | Wave-F cancel-and-re-request window swallows a regeneration round trip | Spec | Create | Confirmed | P2 | 95% | ICW-320 | S7, S9, S11, S17 |
| F-007 | `HandleWorkStopped` keyed remove can clobber a newer item | Standards | Create | Confirmed | P2 | 90% | ICW-320 | S7, S17 |
| F-008 | Dead `DefectBitmap`/`LockBits` sampling in `DrawDefectPatch` | Standards | Create | Confirmed | P2 | 95% | ICW-321 | S16, S17, S21 |
| F-009 | Reentrant lock chain in cache eviction | Standards | Create | Confirmed | P2 | 95% | ICW-322 | S17, S21 |
| F-010 | Per-tile noise seed and local normalization defeat seamless noise | Spec | Create | Confirmed | P2 | 85% | ICW-324 | S19, S21, S22 |
| F-011 | `SelectMipLevel` under-resolves the zoomed-in axis in anisotropic states | Spec | Create | Confirmed | P2 | 85% | ICW-325 | S19, S21 |
| F-012 | Tile-grid overlay rebuilds from the whole scene per frame | Standards | Create | Confirmed | P2 | 90% | ICW-326 | S7, S8, S9 |
| F-013 | No epoch-wiring behavioral regression test | Spec | Create | Confirmed | P3 | 90% | ICW-323 | S16, S20 |
| F-014 | `AddClaimant` registers the token callback before adding the claimant | Standards | Create | Confirmed | P3 | 95% | ICW-320 | S17, S21 |
| F-015 | `ComputeMinimumZoom` divides by `SceneBounds` with no guard | Standards | Update | Confirmed | P3 | 90% | ICW-304 | S7, S10 |
| F-016 | Boundary-edge conventions undocumented and inconsistent | Standards | Update | Confirmed | P2 | 90% | ICW-308 | S1, S16, S19 |
| F-017 | `CanvasViewModel.Zoom` has zero callers; wheel path bypasses it | Standards | Update | Confirmed | P3 | 95% | ICW-313 | S7, S9 |
| F-018 | No `Loaded`/`Unloaded` lifecycle stops anchor-pan timer or cursor | Spec | Update | Confirmed | P2 | 90% | ICW-313 | S1, S2 |
| F-019 | ICW-102 premise "bitmaps never disposed" is stale | Spec | Update | Confirmed | P3 | 90% | ICW-102 | S16, S17 |
| F-020 | `BoundedNumeric` Integer branch throws for narrow fractional bounds | Standards | Update | Confirmed | P3 | 95% | ICW-067 | S7, S8 |
| F-021 | Corpus integrity: duplicate YAML keys, three-file ticket family, status divergence | Standards | Update | Confirmed | P2 | 95% | ICW-081 | S6, S9, S24, S29 |
| F-022 | ICW-129 status divergence (Done vs In Progress vs missing) | Spec | Update | Confirmed | P2 | 85% | ICW-129 via ICW-324 | S19, S23, S24 |
| F-023 | Eviction can select an actively generating tile; documented fallback | Spec | Update | Confirmed | P3 | 85% | ICW-104/ICW-305 | S17, S21, S25 |
| F-024 | Delta-6 "permanent cancellation loss" mechanism refuted | Spec | Reject | Confirmed | none | 95% | none (record) | S13, S17 |
| F-025 | Stale pre-refactor claims (dual-path shell, dead Spatial ref, FIFO queue, slider handlers, GenerateSet null path) | Spec | Reject | Refuted | none | 95% | none (record) | S6, S9, S16, S17, S21 |
| F-026 | RowDefinition `17*`/`925*` ratio observation | none | Defer | Unverified | none | 40% | none | S8 |

## Findings

### F-001 Duplicate Item-Query Authority on Canvas Source Contracts

**Axis:** Spec
**Provenance:** Net-new (verified against newly landed ICW-312/315 code)
**Task disposition:** Update (ICW-312) + gate (ICW-316A)
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 95%, from direct source reads of both interfaces at HEAD.
**Origin:** D1a (S1), D1d (S2), C2-016 (S7, S9, S10), council-1 dissent.

#### Description

`ICanvasSceneSource` and `ICanvasSpatialQuerySource` both declare `QueryVisible` with the same shape (`ICanvasSceneSource.cs:14/20`, `ICanvasSpatialQuerySource.cs:8/13`). Both are wired as dependency properties on `CanvasControl`, and neither is consumed by the control at HEAD. This is a split-brain item authority: ICW-314 selection and hit-testing must consume one of them, and the ICW-316 move would publish the ambiguity as library API.

#### Rationale

Source reads at `src/InfiniteCanvas.Core/ICanvasSceneSource.cs` and `src/InfiniteCanvas.Core/ICanvasSpatialQuerySource.cs` confirm the duplicated member. `CanvasBoundaryZeroReferenceTests` asserts both source dependency properties exist, which locks in the duplication. The council confirmed the two extraction seats describe the same mechanism and must not produce two tickets.

#### Counter-evidence and Deduplication

`ICanvasSpatialQuerySource` has no consumer at HEAD, so collapsing the duplication is safe today. The existing tests encode the current shape and must change atomically with the fix. Distinct from F-017 (dead `Zoom` wrapper), which is a different mechanism.

#### Recommendation and Validation

Resolve to a single item-query authority before ICW-314. Cheapest test: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter "CanvasBoundaryZeroReferenceTests|CanvasSceneSourceContractsTests"` after consolidation, plus a source scan showing the control consumes exactly one contract.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-312, ICW-314, ICW-316A | authority resolution home and consumers |

#### Finding Sources

S1, S2, S3, S15, S28, S29.

---

### F-002 Split ICW-316 into Harden (316A) Then Move (316B)

**Axis:** Spec
**Provenance:** Net-new (council synthesis)
**Task disposition:** Create (ICW-316A) + rescope (ICW-316)
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 90%, from the council's cross-extract trace of every seam that a mechanical move would publish as public API.
**Origin:** C1-022 (S1), council-1 section 6, council-3 section 3.

#### Description

A mechanical extraction before hardening would fossilize F-001, F-003, F-004, and F-005 as stable library API. Reversing that later is a breaking change. The council recommends splitting ICW-316 into ICW-316A (harden in place) and a rescoped ICW-316 (physical move), with the boundary items landing in order.

#### Rationale

Council-1 traced each seam to source: duplicate query authority, mutable `CanvasFrame`, public `CanvasViewModel` setters, and seven raw element properties plus two overlay canvases on `CanvasControl`. Council-3 verified ICW-319..326 are the first collision-free keys (highest used key is ICW-318).

#### Counter-evidence and Deduplication

None. This is a sequencing decision, not a defect claim.

#### Recommendation and Validation

Create ICW-316A with the F-001..F-004 items and lifecycle work. Rescope ICW-316 to the physical move with `CanvasFrame` in scope (C1-015/C1-020/C1-021). Gate the move on ICW-316A and ICW-319. Validation: `dotnet build InfiniteCanvasWPF.slnx --configuration Release` plus a consumer-host reference test.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-316, ICW-319, ADR-0007 | sequencing chain |

#### Finding Sources

S1, S2, S3, S15, S23, S29.

---

### F-003 `CanvasFrame` Claims Snapshot Semantics but Borrows Mutable State

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create (ICW-316A)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 92%, from direct source read of `CanvasFrame.cs:22-52`.
**Origin:** C1-003, C1-013 (S1), C1-009 (S1).

#### Description

`CanvasFrame` is presented as a frozen snapshot but borrows mutable lists, has no `IsFrozen` check, no revision identity, and does not validate raster dimensions against `ImageSource` metadata. `Stretch.Fill` is the only display path, so a dimension mismatch would stretch silently.

#### Rationale

Source read at `src/InfiniteCanvas.App/Controls/CanvasFrame.cs` confirms the borrow semantics and missing validation. The frame is the boundary value the host hands to the control each publish.

#### Counter-evidence and Deduplication

The single producer (`RenderFrameAsync`) passes matching dimensions today, so severity stays P2. Distinct from F-004 (view-model setters), which is the view-model side of the same hardening.

#### Recommendation and Validation

Make `CanvasFrame` immutable by contract with count-consistency and raster-dimension validation plus a revision field. Cheapest test: a unit test constructing a frame with mismatched counts/dimensions and asserting construction or validation failure.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-316A, ICW-315 | frame boundary |

#### Finding Sources

S1, S28.

---

### F-004 `CanvasViewModel` Public Setters Permit Impossible Frame States

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create (ICW-316A)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from direct source read of `CanvasViewModel.cs:6-18,43-46`.
**Origin:** C1-023, C1-024, C1-027, C1-031, C1-032 (S1), council-1.

#### Description

Public setters on `CanvasViewModel` allow `VisibleItemCount > TotalItemCount` and list/count divergence. The optional-items `ApplyFrame` overload silently diverges, and `ApplyFrame` raises four sequential property notifications, so subscribers see torn state. An existing test (`CanvasSceneSourceContractsTests.cs:112-119`) encodes the harmful fallback.

#### Rationale

Source reads at `src/InfiniteCanvas.ViewModels/CanvasViewModel.cs` confirm the setters and the optional-items path. `HasScene` is a manual notification that a public setter can bypass.

#### Counter-evidence and Deduplication

The invariant "visible count must not exceed total" is a Spec-axis contract for a reusable view model. The notification-batching concern is Standards axis. Both belong to one hardening ticket to keep the diff atomic.

#### Recommendation and Validation

Require a non-null visible-items list in `ApplyFrame`, publish state as one notification batch, and remove setters that permit impossible states. Update `CanvasSceneSourceContractsTests` in the same change. Cheapest test: `dotnet test --filter "CanvasViewModel"`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-316A, ICW-315 | view-model boundary |

#### Finding Sources

S1, S28.

---

### F-005 Raw WPF Element Surface Leaks from `CanvasControl`

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Create (ICW-319)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from grep-level source evidence of the public aliases.
**Origin:** C2-005, C2-007 (S2, S7, S9, S10), C1-004 (S1), C2-010 (S8).

#### Description

`CanvasControl` exposes seven public element properties (`SurfaceHost`, `FrameHost`, `LoadingText`, `WorldReadout`, `TileReadout`, `ValueReadout`, `BusyBar`) and two overlay canvases (`TileGridLayer`, `AnnotationLayer`) that `MainWindow` mutates directly. The `LoadingOverlay` has a hardcoded `Margin="0,446,0,0"` that breaks the any-host, any-size reuse goal. This blocks a clean ICW-316 extraction.

#### Rationale

Source read of `CanvasControl.xaml.cs` and `MainWindow.xaml.cs` confirms the mutation sites and the hardcoded margin. Council-1 folded C1-004/C2-005/C2-007/C2-010 into one ticket to avoid duplicate ICW keys.

#### Counter-evidence and Deduplication

One mechanism (public surface) with one remediation (method-based API). Not duplicated with F-001, which is about source contracts, not element properties.

#### Recommendation and Validation

Replace the aliases with methods (`SetLoadingState`, `SetBusyIndicatorVisible`, `SetPixelometerReadout`, `ClearFrame`, `SetViewportSize`) and route `MainWindow` mutation sites behind them. Cheapest validation: a source scan asserting no `CanvasSurface.TileGridLayer`/`SurfaceHost` references remain, plus `FrameShellWiringTests` and `CanvasScrollbarWiringTests` stay green.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-319, ICW-316 | boundary API before move |

#### Finding Sources

S1, S2, S7, S8, S9, S10, S28.

---

### F-006 Wave-F Cancel-and-Re-Request Window Swallows a Regeneration Round Trip

**Axis:** Spec
**Provenance:** Net-new (follow-up to ICW-WAVE-F-VIEWPORT-CANCELLATION)
**Task disposition:** Create (ICW-320)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from council-2's independent trace of `Request` and `CancelWorkItem`.
**Origin:** C2-017 (S7, S9, S11), C2-023 core (S13).

#### Description

`Request` coalesces a fresh claimant onto an already-canceled still-running item (TileWorkCoordinator.cs:176-178, 553-556). On scroll-away-and-back, the re-request lands on the dying item, and one regeneration round trip is swallowed. The mechanism is bounded and self-healing: the dying worker's `DispatchFailed` resets `_generationQueued` and triggers a re-render.

#### Rationale

Council-2 traced the mechanism: coalesce-on-presence, cancel keeps the `Canceled` item in `_items`, and the re-request hits the same entry. The council downgraded the severity from P1 to P2 because the failure is one swallowed round trip and a possible placeholder frame, not a permanent strand.

#### Counter-evidence and Deduplication

This is the valid core of delta-6's refuted headline (F-024). Distinct from F-007, which is the safety prerequisite for the same fix.

#### Recommendation and Validation

Do not coalesce when the existing item is in a terminal state. Land with F-007 and F-014 in one atomic change. Test: `RunningWorkCanceled_ReRequest_AdmitsFreshItem` (fails on HEAD). Link to ICW-P0-ACTIVECOUNT-residuals and ICW-WAVE-F-VIEWPORT-CANCELLATION. Land before ICW-144 closes so benchmark evidence does not measure the bug.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-320, ICW-WAVE-F-VIEWPORT-CANCELLATION, ICW-P0-ACTIVECOUNT-residuals | parent and sibling |

#### Finding Sources

S7, S9, S11, S13, S17, S28.

---

### F-007 `HandleWorkStopped` Keyed Remove Can Clobber a Newer Item

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Create (ICW-320)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, from council-2's trace of `HandleWorkStopped` (TileWorkCoordinator.cs:510).
**Origin:** C2-018 (S7).

#### Description

`HandleWorkStopped` removes `_items[key]` by key without reference equality. After the F-006 fix, a late old-worker stop could remove a newly admitted item. `DrainQueueWithLivenessCheck` would then skip the orphaned heap entry without `DispatchFailed`, leaking the reservation and stranding the tile until the next frame-token fire.

#### Rationale

Council-2 traced the orphaned-heap-entry path and classified F-007 as the blocking prerequisite for F-006: the two must land together.

#### Counter-evidence and Deduplication

Same ticket as F-006 by design. Distinct mechanism from F-014 (registration order).

#### Recommendation and Validation

Guard the remove with `ReferenceEquals(current, item)`. Test: `LateWorkerStop_DoesNotRemoveNewerItem`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-320 | atomic change with F-006 |

#### Finding Sources

S7, S28.

---

### F-008 Dead `DefectBitmap`/`LockBits` Sampling in `DrawDefectPatch`

**Axis:** Standards
**Provenance:** Net-new (corroborated by three audits)
**Task disposition:** Create (ICW-321)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from source read of `ZeroCopyBitmapFactory.Windows.cs:337-380`.
**Origin:** C3-009 (S16, S17, S21), C2-019 (S9).

#### Description

`DrawDefectPatch` locks a `DefectBitmap`, reads `sourceRow[sourceX * 3]`, and discards the value. The display value comes from `DefectPixels` via the sampler. The dead read adds a native-resource category and is the remaining surface of the dispose-vs-render race.

#### Rationale

Source read confirms the discarded read. `SampleImageTile.cs:904` and `AnnotationGenerator.cs:57` hold the other `DefectBitmap` references. Removal also dissolves most of the ICW-102 race surface.

#### Counter-evidence and Deduplication

C2-019 (unused `sourceRow[sourceX * 3]` read) is the same code region and merges into this ticket. Council-3 dissent rejected folding it into ICW-023 to avoid conflicting edits.

#### Recommendation and Validation

Remove `LockBits`/`UnlockBits` and the dead read; remove `DefectBitmap` from `SampleAnnotation` and its assignment. Accept: byte-identical rendered output, zero remaining references. Cheapest validation: a Windows render test comparing golden bytes plus a source-text assertion that `DrawDefectPatch` never calls `LockBits`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-321, ICW-102 | removal precedes ICW-102 rescope |

#### Finding Sources

S9, S16, S17, S21, S28.

---

### F-009 Reentrant Lock Chain in Cache Eviction

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Create (ICW-322)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from source read of the chain at `Request:186` → `TryReserve:1070` → `EvictCacheEntry:487` → `RemoveClaimant:223`.
**Origin:** C3-010 (S17, S21).

#### Description

Cache eviction calls back into `TileWorkCoordinator._lock` while `Request` still holds it. The chain is safe today only through same-thread `Lock` reentrancy and becomes a hard deadlock if any site gains an `await` or a thread hop.

#### Rationale

Council-2 traced the full chain at HEAD and classified it as a latent hazard with a design-review trigger.

#### Counter-evidence and Deduplication

Distinct from F-006/F-007/F-014 (claimant window). It is a structural hazard, not a live defect.

#### Recommendation and Validation

Document the chain at all three sites, or return evicted keys to `Request` and notify after the lock exits. Trigger: mandatory before ICW-P0-LEASE-RELEASE or any async memory-governor work. Discriminating validation is a review gate, not a regression test.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-322, ICW-P0-LEASE-RELEASE | trigger dependency |

#### Finding Sources

S17, S21, S28.

---

### F-010 Per-Tile Noise Seed and Local Normalization Defeat Seamless Noise

**Axis:** Spec
**Provenance:** Net-new (correction to ICW-129)
**Task disposition:** Create (ICW-324)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 85%, from source reads of `SampleImageGenerator.cs:187,551-572`; the requirement decision is open.
**Origin:** C3-011, C3-012, C3-018 (S19, S21, S22).

#### Description

`SampleImageGenerator` seeds each tile's noise with `options.Seed + 3 * tileIndex` and normalizes each tile against its local min/max. Both defeat world-continuous seamless sampling at tile boundaries. The `ICW-129` ticket claims "seamless worldspace sampling" but is status-divergent.

#### Rationale

Source reads confirm the per-tile seed and local normalization. Council-1 and council-3 both flagged the requirement conflict: the registry row "Deterministic tile generation" requires independent per-tile streams, which conflicts with seamless sampling.

#### Counter-evidence and Deduplication

`annotationSeed` per-tile is separate and correct. C3-012 (local normalization) is the same seam mechanism and folds in.

#### Recommendation and Validation

Resolve the requirement conflict first. Either adopt a world-continuous seed with a documented registry change, or strike "seamless" from ICW-129 scope and document per-tile variance as intended. Add an adjacent-tile boundary test. Cheapest validation: a two-tile boundary test asserting no edge discontinuity, plus a tracker-status check.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-324, ICW-129, registry "Deterministic tile generation" | requirement tension |

#### Finding Sources

S19, S21, S22, S25, S28.

---

### F-011 `SelectMipLevel` Under-Resolves the Zoomed-In Axis

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create (ICW-325)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 85%, from source read of `BackgroundTileContracts.cs:175-176` plus the ADR-0005 texel-density rule.
**Origin:** C3-021 (S19, S21).

#### Description

`SelectMipLevel` uses `Math.Min(ScaleX, ScaleY)`. ADR-0005 requires the coarsest mip whose texel density stays at or above one texel per screen pixel on both axes. With texel density `1/(Scale * 2^L)` per axis, the binding axis is the larger scale, so `Math.Min` under-resolves the zoomed-in axis in any real anisotropic state (ICW-011 axis-clamped zoom).

#### Rationale

Council-1 supplied the Spec-axis proof the extraction omitted.

#### Counter-evidence and Deduplication

This is a spec violation, not a quality judgement. It changes which payload is sampled in anisotropic states and must land with a visual regression, not only a unit test.

#### Recommendation and Validation

Decide the selection rule against ADR-0005 (use `Math.Max`, per-axis LOD, or an explicit anisotropic decision) and add a non-uniform-camera test. Gate on an ADR-0005 alignment decision.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-325, ADR-0005, ICW-076 | mip contract |

#### Finding Sources

S19, S21, S26, S28.

---

### F-012 Tile-Grid Overlay Rebuilds from the Whole Scene Per Frame

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Create (ICW-326)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, from source read of `MainWindow.xaml.cs:676-713`.
**Origin:** C2-024 (S7, S8, S9).

#### Description

`UpdateTileGridLayer` enumerates the entire `_tiles` collection on every publish, even though the camera-visible set is already computed. Per-frame cost scales with total scene size in the publish hot path and was carried forward by the ICW-317 shell rewrite.

#### Rationale

Source read confirms the full-scene enumeration in the publish path.

#### Counter-evidence and Deduplication

Host-side concern; independent of the ICW-316 boundary work. Distinct from the overlay pooling task (ICW-007).

#### Recommendation and Validation

Thread the computed `visibleTiles` set into the grid layer; optionally skip rebuild when camera and tile set are unchanged. Preserve camera-synchronization and non-hit-testability. Cheapest validation: a source assertion that `UpdateTileGridLayer` no longer touches `_tiles`, or a large-scene timing probe.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-326, ICW-317, ICW-318 | shell cluster follow-up |

#### Finding Sources

S7, S8, S9, S28.

---

### F-013 No Epoch-Wiring Behavioral Regression Test

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Create (ICW-323)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 90%, from the absence of any wiring-level test plus the 2026-07-26 revert history.
**Origin:** C3-017 (S16, S20).

#### Description

`RenderRequestTrackerTests` test the primitive, not the wiring. Nothing fails if `MainWindow` stops calling `BeginRequest`/`IsCurrent`/`Advance` in `RenderFrameAsync`. The 2026-07-26 epoch-guard revert slipped exactly this way.

#### Rationale

The guard is wired at `MainWindow.xaml.cs:52,530,550,554` after ICW-100 re-applied it, but no test pins the wiring.

#### Counter-evidence and Deduplication

ICW-078/ICW-100 are Done; this is a test-only follow-up, not a reopen.

#### Recommendation and Validation

Add a wiring assertion in the style of `FrameShellWiringTests`. Acceptance: the test fails on the 2026-07-26 revert shape. Test-only, no production risk.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-323, ICW-078, ICW-100 | regression guard |

#### Finding Sources

S16, S20, S28.

---

### F-014 `AddClaimant` Registers the Token Callback Before Adding the Claimant

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Create (ICW-320)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 95% mechanism, low reachability, from source read of `TileWorkCoordinator.cs:783-786`.
**Origin:** C3-006 (S17, S21).

#### Description

`AddClaimant` registers the token callback before adding the claimant entry. A pre-canceled token leaves a ghost claimant. Not reachable in the current serialized render flow, but a permanent trap that reopens if renders ever run concurrently.

#### Rationale

Council-2 confirmed the mechanism and moved the ticket home from ICW-P0-ACTIVECOUNT-residuals to the Wave-F follow-up, agreeing with council-3.

#### Counter-evidence and Deduplication

Same ticket as F-006/F-007; the registration-order fix belongs with the claimant-registration work.

#### Recommendation and Validation

Add the claimant before registering the callback, or skip the add for an already-canceled token. Test: `PreCanceledToken_DoesNotLeaveGhostClaimant`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-320 | atomic change with F-006 |

#### Finding Sources

S17, S21, S28.

---

### F-015 `ComputeMinimumZoom` Divides by `SceneBounds` with No Guard

**Axis:** Standards
**Provenance:** Extension
**Task disposition:** Update (ICW-304)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 90%, from source read of the division site.
**Origin:** C2-009 (S7, S10), C1-028 (S1).

#### Description

`CanvasViewModel.ComputeMinimumZoom` divides by `SceneBounds` dimensions without a guard. Council-3 merged C2-009 with C1-028 and routed both to ICW-304 because both concern the same method; this is a division guard and typed-scale concern, not boundary semantics.

#### Rationale

Source read confirms the unguarded division. Council-3 dissent corrected the earlier proposal to fold it into ICW-308.

#### Counter-evidence and Deduplication

Distinct from F-016 (boundary semantics). Same mechanism as C1-028; one ticket.

#### Recommendation and Validation

Add a typed scale plus a `HasScene`/zero-bounds guard in ICW-304. Cheapest validation: a unit test with degenerate `SceneBounds`.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-304, ICW-301, ICW-308 | related primitive/hardening family |

#### Finding Sources

S1, S7, S10, S28.

---

### F-016 Boundary-Edge Conventions Undocumented and Inconsistent

**Axis:** Standards
**Provenance:** Corroboration
**Task disposition:** Update (ICW-308)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, from source reads of `SpatialBounds.cs:45-49` and `TileGridIndexLookup`.
**Origin:** C1-030 (S1), C3-022 (S19), C2-030 (S6).

#### Description

`SpatialBounds.Intersects` uses inclusive edges while `TileGridIndexLookup` and the pixelometer use the opposite exclusive convention. The contracts are partially documented by `CanvasSceneSourceContractsTests.cs:79-89` but the inconsistency is not resolved or stated.

#### Rationale

Council-3 confirmed ICW-308 is the correct home and that C2-009 must not fold here (see F-015).

#### Counter-evidence and Deduplication

Scope as documentation plus edge/zero-area tests, not a behavior change, to preserve spatial-index parity.

#### Recommendation and Validation

Widen ICW-308 to cover `Intersects`, `TileGridIndexLookup` exclusive edges, and pixelometer half-open reads. Cheapest validation: edge-coordinate unit tests.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-308, ICW-033 | boundary family |

#### Finding Sources

S1, S6, S19, S28.

---

### F-017 `CanvasViewModel.Zoom` Has Zero Callers

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Update (ICW-313)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 95%, from a usage scan at HEAD.
**Origin:** C2-008 (S7, S9).

#### Description

The wheel zoom path bypasses `CanvasViewModel.Zoom`, leaving the wrapper dead and inconsistent with `Pan`.

#### Rationale

Source and usage scan confirm zero callers.

#### Counter-evidence and Deduplication

Folds into ICW-313 (input-handler abstraction), which rewrites the input paths.

#### Recommendation and Validation

Reconcile or delete the wrapper inside ICW-313. Cheapest validation: a usage scan after ICW-313 lands.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-313 | input abstraction |

#### Finding Sources

S7, S9, S28.

---

### F-018 No `Loaded`/`Unloaded` Lifecycle Stops the Anchor-Pan Timer or Cursor

**Axis:** Spec
**Provenance:** Net-new
**Task disposition:** Update (ICW-313)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 90%, from the absence of any lifecycle handler in `CanvasControl.xaml.cs`.
**Origin:** C1-007 (S1, S2).

#### Description

`CanvasControl` owns the anchor-pan interaction but has no `Loaded`/`Unloaded`/dispose path to stop the timer, release capture, or clear `Mouse.OverrideCursor`. A detach can strand the cursor override.

#### Rationale

Grep over `CanvasControl.xaml.cs` finds no `Loaded`/`Unloaded` handler.

#### Counter-evidence and Deduplication

Distinct from F-005 (public surface). Both are boundary hardening but different mechanisms.

#### Recommendation and Validation

Add lifecycle handling in ICW-313 scope. Cheapest validation: an unload-path test that asserts timer/capture/cursor cleanup.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-313, ICW-316A | boundary lifecycle |

#### Finding Sources

S1, S2, S28.

---

### F-019 ICW-102 Premise "Bitmaps Never Disposed" Is Stale

**Axis:** Spec
**Provenance:** Correction
**Task disposition:** Update (ICW-102)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 90%, from source read of the disposal paths.
**Origin:** C3-004, C3-007 (S16, S17).

#### Description

Disposal now exists (`DefectTemplate.Dispose` calls `Bitmap?.Dispose()`; pools dispose at the scene boundary). The remaining question is the fence against concurrent in-flight render, which shrinks further once ICW-321 removes the dead bitmap reads.

#### Rationale

Council-2 traced the disposal paths and the render `Task.Run` at `MainWindow.xaml.cs:692-701`.

#### Counter-evidence and Deduplication

C3-007 (race) is a scope note on the same ticket, not a separate ticket.

#### Recommendation and Validation

Strike the stale premise and re-evaluate the fence after ICW-321 lands. Cheapest validation: the ICW-321 removal plus the existing pool tests.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-102, ICW-321 | sequencing |

#### Finding Sources

S16, S17, S28.

---

### F-020 `BoundedNumeric` Integer Branch Throws for Narrow Fractional Bounds

**Axis:** Standards
**Provenance:** Extension
**Task disposition:** Update (ICW-067)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 95%, from source read of the Integer branch.
**Origin:** C2-013 (S7, S8).

#### Description

The Integer branch of `BoundedNumeric` throws `ArgumentException` for fractional bounds narrower than one integer step instead of returning false.

#### Rationale

Council-3 folded it into ICW-067 (SliderTextBox/BoundedNumeric owner) and flagged the ICW-067 status divergence.

#### Counter-evidence and Deduplication

Same file family as ICW-067 scope; one ticket.

#### Recommendation and Validation

Return false for impossible integer bounds. Cheapest validation: a `BoundedNumeric` unit test with narrow fractional bounds.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-067 | settings input control |

#### Finding Sources

S7, S8, S28.

---

### F-021 Corpus Integrity: Duplicate YAML Keys, Three-File Ticket Family, Status Divergence

**Axis:** Standards
**Provenance:** Net-new
**Task disposition:** Update (ICW-081)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, from direct reads of the ticket files and the validator script.
**Origin:** C2-001, C2-002, C2-003 (S6, S9, S10), ICW-008/062/063 family (S29).

#### Description

`ICW-307` has a literal duplicate `status:` YAML key (lines 5 and 17) and a duplicated validation block. `ICW-306` has a duplicated validation block. `ICW-305`'s summary misstates the eviction mechanism. The publish-hardening tickets `ICW-008`, `ICW-062`, `ICW-063` form a three-file near-duplicate family. `Validate-TaskTracker.ps1` lets the last `status:` win silently and cannot detect these.

#### Rationale

Direct reads confirm each defect. Council-3 verified the canonical ticket is ICW-063 and that ICW-008 and ICW-062 must merge into it.

#### Counter-evidence and Deduplication

These are authoring defects, distinct from the F-010 status divergence (which is a requirement question).

#### Recommendation and Validation

Fix the duplicate keys/blocks, correct ICW-305's summary, merge ICW-008/062 into ICW-063, and extend the validator with duplicate-key, status-cross-check, and key-uniqueness rules. Run `scripts/Validate-TaskTracker.ps1` after each edit.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-081, ICW-307, ICW-306, ICW-305, ICW-063, ICW-084 | corpus reconciliation |

#### Finding Sources

S6, S9, S10, S24, S29.

---

### F-022 ICW-129 Status Divergence

**Axis:** Spec
**Provenance:** Correction
**Task disposition:** Update (ICW-129 via ICW-324)
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 85%, from cross-reading active-tasks, task tracker, and the ticket file.
**Origin:** C3-011 (S19), council-3.

#### Description

`active-tasks.md` marks ICW-129 Done, the ticket file says In Progress, and `task-tracker.md` has no row. The status binds to the un-met "seamless worldspace sampling" acceptance criterion.

#### Rationale

Council-3 explicitly dissented against closing ICW-129 for hygiene reasons; the status decision belongs to the requirement decision in ICW-324 (F-010).

#### Counter-evidence and Deduplication

Not an authoring defect; the status is a real requirement question.

#### Recommendation and Validation

Reconcile inside ICW-324: one status, one task tracker row, and a decision on seamless vs per-tile variance.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-324, ICW-129 | requirement decision |

#### Finding Sources

S19, S23, S24, S29.

---

### F-023 Eviction Can Select an Actively Generating Tile

**Axis:** Spec
**Provenance:** Corroboration
**Task disposition:** Update (ICW-104/ICW-305)
**Verification:** Confirmed
**Severity:** P3
**Confidence:** 85%, from the requirements registry and source read of the eviction fallback.
**Origin:** C2-021 (S8, S9), ICW-064 registry row.

#### Description

Cache eviction can select an actively generating tile. This is the documented intentional fallback in the requirements registry (ICW-064, "Lazy tile cache admission"). The in-flight-candidate question must be an explicit decision in the eviction-policy spec.

#### Rationale

Council-2 classified this as a judgement call, not a defect, and kept it under ICW-104/ICW-305.

#### Counter-evidence and Deduplication

Distinct from F-009 (lock chain) which is structural.

#### Recommendation and Validation

Make the in-flight-candidate decision explicit in the ICW-305/ICW-104 policy spec.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-104, ICW-305 | eviction policy |

#### Finding Sources

S8, S9, S17, S21, S25.

---

### F-024 Delta-6 "Permanent Cancellation Loss" Mechanism Refuted

**Axis:** Spec
**Provenance:** Rejected (refutation recorded)
**Task disposition:** Reject
**Verification:** Confirmed (refutation)
**Severity:** none
**Confidence:** 95%, from council-2's trace of the token-fire remove-then-re-request path.
**Origin:** S8 headline (S13), C2-023 (S13, S17).

#### Description

Delta-6 claimed ICW-204 permanently loses cancellation registration for multi-frame generations. The mechanism is flawed: the frame-token fire removes the claimant entry before the flag reset re-triggers the re-request, so the re-request takes the new-claimant branch and registers a fresh cancellation on the new token. No permanent loss.

#### Rationale

Council-2 traced `TileWorkCoordinator.cs:793-819` and confirmed the remove-then-re-request ordering.

#### Counter-evidence and Deduplication

The valid core (cancel-and-re-request window) is captured by F-006. Record the refutation so delta-6 is not re-filed wholesale.

#### Recommendation and Validation

None. Recorded in the report and the extract-2 seat report.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Audit | S13 | refuted claim |

#### Finding Sources

S13, S17, S28.

---

### F-025 Stale Pre-Refactor Claims

**Axis:** Spec
**Provenance:** Rejected (stale)
**Task disposition:** Reject
**Verification:** Refuted
**Severity:** none
**Confidence:** 95%, from source and tracker checks that each refactor is Done and present.
**Origin:** C2-006, C2-011, C2-014, C2-025, C2-030 (S7, S9, S10); C3-001, C3-003, C3-013, C3-014, C3-016 (S16, S17, S21).

#### Description

These claims reference code that no longer exists at HEAD or defects already fixed:

- `FramePresenter.Child` dual-path bypass (gone; shell lives in `CanvasControl`, ICW-315).
- Dead `InfiniteCanvas.Spatial` reference in the ViewModels project (fixed, ICW-312).
- FIFO tile queue with no priority (delivered, ICW-205).
- ICW-078 guard missing (re-applied, ICW-100).
- Empty background slider handlers (moved to `TileBackgroundNoiseSettingsView`, ICW-067).
- ICW-143 blocked on ICW-078 (dependency cleared).
- `GenerateSet` `Rows is <= 0` null path unreachable (false positive; the pattern handles null correctly).

#### Rationale

Each refuted claim was traced to the current code or the tracker at HEAD.

#### Counter-evidence and Deduplication

None of these has an open requirement gap that survives in a new form. They are recorded so future runs do not re-file them.

#### Recommendation and Validation

None. Recorded.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-315, ICW-312, ICW-205, ICW-100, ICW-067 | done refactors |

#### Finding Sources

S7, S9, S10, S16, S17, S21, S28.

---

### F-026 RowDefinition `17*`/`925*` Ratio Observation

**Axis:** none
**Provenance:** Deferred
**Task disposition:** Defer
**Verification:** Unverified
**Severity:** none
**Confidence:** 40%, from the audit's own low confidence and no defect mechanism.
**Origin:** C2-012 (S8).

#### Description

An observation that a `MainWindow.xaml` RowDefinition uses a `17*`/`925*` ratio that may have been lifted from a designer tool. No defect mechanism.

#### Rationale

The evidence needed (pre-extraction XAML history) is not in the working tree.

#### Counter-evidence and Deduplication

None; deferred by consensus.

#### Recommendation and Validation

Defer. Confirm or dismiss only if a designer-origin audit is wanted.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| None | none | none |

#### Finding Sources

S8.

---

## Assumptions

| ID | Assumption | Effect if false | Evidence needed | Owner |
| --- | --- | --- | --- | --- |
| A-1 | `System.Threading.Lock` supports same-thread reentrancy, making the F-009 chain safe today | F-009 becomes a live deadlock | Verify against the runtime lock primitive | ICW-322 implementer |
| A-2 | `CoalescingAsyncAction` never runs two render bodies concurrently | F-014 ghost becomes reachable | Review render serialization on any parallel-render change | ICW-320 implementer |
| A-3 | `ICanvasSpatialQuerySource` has no consumer at HEAD | F-001 resolution changes shape | Re-check consumers before ICW-314 | ICW-316A implementer |
| A-4 | Seamless noise is a product requirement | ICW-324 outcome changes | User or owner decision | Unassigned |
| A-5 | The render pipeline stays host-side per ADR-0007 | ICW-326 and 316B scope change | ADR review | Unassigned |

## Open Questions

| ID | Question | Why it matters | Cheapest resolution | Owner |
| --- | --- | --- | --- | --- |
| Q-1 | Is seamless noise required, or is per-tile variance acceptable? | Gates ICW-324 and the ICW-129 status | Product decision | Unassigned |
| Q-2 | Should `ICanvasSpatialQuerySource` be deleted or kept as the future hit-test authority? | Shapes the F-001 resolution | Source/design review | Unassigned |
| Q-3 | Does ICW-102 keep a minimal fence after ICW-321 removes the dead bitmap reads? | Determines ICW-102 close vs scope | Decision after ICW-321 | Unassigned |
| Q-4 | Where do the canvas contracts live after extraction (Core vs new assembly)? | Freezes the ICW-316B API surface | Decision in ICW-316B | Unassigned |

## Requests

| Priority | Request | Rationale | Required response |
| --- | --- | --- | --- |
| P1 | Decide the seamless-noise requirement (Q-1) | Blocks ICW-324 and the ICW-129 status | One-line decision: seamless or per-tile |
| P1 | Decide the item-query authority (Q-2) | First gate of ICW-316A | One-line decision: keep one contract vs both |
| P2 | Confirm the priority order of the new backlog | Sequencing of 316A/319/320/321 depends on sprint scope | Accept or reorder the proposed backlog |
| P2 | Decide whether ICW-102 keeps a minimal fence after ICW-321 (Q-3) | Determines a close vs scope action | Decision after ICW-321 lands |

## Source Ledger

| ID | Source | Type | Revision or date | Read directly | Use and limitation |
| --- | --- | --- | --- | --- | --- |
| S1 | docs/audits/infinitecanvaswpf-next-slice-delta-audit-26-08-04-21-18-30.md | audit | 2026-08-04 | yes | Group 1; targets HEAD |
| S2 | docs/audits/infinitecanvaswpf-next-slice-audit-26-08-04-21-03-43.md | audit | 2026-08-04 | yes | Group 1; targets HEAD |
| S3 | docs/audits/infinitecanvaswpf-exhaustive-deep-dive-audit-26-08-04-17-17-42.md | audit | 2026-08-04 | yes | Group 1; targets HEAD |
| S4 | docs/audits/icw-316-next-slice-audit-26-08-04-15-50-00.md | audit | 2026-08-04 | yes | Group 1; targets HEAD |
| S5 | docs/audits/infinitecanvaswpf-audit-26-08-04-15-35-00.md | audit | 2026-08-04 | yes | Group 1; targets HEAD |
| S6 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-04-08-12-10.md | audit | 2026-08-04 | yes | Group 2 |
| S7 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-04-20-12-02.md | audit | 2026-08-04 | yes | Group 2 |
| S8 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-04-20-16-16.md | audit | 2026-08-04 | yes | Group 2 |
| S9 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-04-20-23-32.md | audit | 2026-08-04 | yes | Group 2 |
| S10 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-04-20-32-22.md | audit | 2026-08-04 | yes | Group 2 |
| S11 | docs/audits/infinitecanvaswpf-icw-delta-findings-26-08-05-02-03-31.md | audit | 2026-08-05 | yes | Group 2 |
| S12 | docs/audits/icw-wave-e-audit-delta-5.md | audit | 2026-08-04 | yes | Group 2 |
| S13 | docs/audits/icw-wave-e-audit-delta-6.md | audit | 2026-08-04 | yes | Group 2; headline refuted |
| S14 | docs/audits/icw-wave-e-audit-delta-7.md | audit | 2026-08-04 | yes | Group 2 |
| S15 | docs/audits/canvas-data-source-abstraction-council-review-26-08-04.md | audit | 2026-08-04 | yes | Group 2; prior council output |
| S16 | docs/audits/infinitecanvaswpf-audit-pass4-delta-26-07-26-15-13-15.md | audit | 2026-07-26 | yes | Group 3; oldest |
| S17 | docs/audits/infinitecanvaswpf-audit-pass6-tileworkcoordinator-26-07-27-19-13-16.md | audit | 2026-07-27 | yes | Group 3 |
| S18 | docs/audits/infinitecanvaswpf-audit-pass8-lock-reentrancy-26-07-28-02-40-23.md | audit | 2026-07-28 | yes | Group 3 |
| S19 | docs/audits/infinitecanvaswpf-audit-pass9-noise-seamlessness-26-07-28-06-44-31.md | audit | 2026-07-28 | yes | Group 3 |
| S20 | docs/audits/infinitecanvaswpf-audit-pass10-icw078-dependency-26-07-28-22-54-55.md | audit | 2026-07-28 | yes | Group 3 |
| S21 | docs/audits/infinitecanvaswpf-external-audit-review-and-architecture-feedback-26-07-29-21-24-17.md | audit | 2026-07-29 | yes | Group 3 |
| S22 | docs/audits/infinitecanvaswpf-external-audit-review-addendum-26-07-30-05-30-01.md | audit | 2026-07-30 | yes | Group 3 |
| S23 | docs/tasks/active-tasks.md | task | HEAD | yes | Tracker coverage |
| S24 | docs/tasks/task-tracker.md | task | HEAD | yes | Tracker coverage |
| S25 | docs/requirements/functional-requirements-and-invariants.md | requirement | HEAD | yes | Spec axis |
| S26 | docs/ADR/0003..0007 | ADR | HEAD | yes | Spec axis |
| S27 | docs/handoffs/2026-08-04-icw312-icw315-data-source-boundary.md | handoff | 2026-08-04 | yes | Current state |
| S28 | src/ and tests/ at HEAD 84a0cdb | code/test | 84a0cdb | yes | All code-path claims |
| S29 | docs/tasks/tickets/*.md | task | HEAD | yes | Ticket corpus and keys |

## Task and Sprint Updates

| Finding | Task action | Tracker locations | Sprint impact |
| --- | --- | --- | --- |
| F-001 | Update ICW-312 + ICW-316A gate | ICW-312 ticket, ICW-316A ticket, active-tasks, task tracker | First 316A gate |
| F-002 | Create ICW-316A; rescope ICW-316 | ICW-316A ticket (new), ICW-316 ticket | Restructures the 316 sequence |
| F-003, F-004 | Create (ICW-316A scope) | ICW-316A ticket | Part of 316A |
| F-005 | Create ICW-319 | ICW-319 ticket (new) | Before 316 move |
| F-006, F-007, F-014 | Create ICW-320 | ICW-320 ticket (new) | Before ICW-144 closes |
| F-008 | Create ICW-321 | ICW-321 ticket (new) | Before ICW-102 rescope |
| F-009 | Create ICW-322 | ICW-322 ticket (new) | Trigger before ICW-P0-LEASE-RELEASE |
| F-010 | Create ICW-324 | ICW-324 ticket (new) | Gates ICW-129 status |
| F-011 | Create ICW-325 | ICW-325 ticket (new) | Parallel, ADR-0005 decision |
| F-012 | Create ICW-326 | ICW-326 ticket (new) | Host-side parallel |
| F-013 | Create ICW-323 | ICW-323 ticket (new) | Test-only, batches with 316A |
| F-015 | Update ICW-304 | ICW-304 ticket | Small |
| F-016 | Update ICW-308 | ICW-308 ticket | Small |
| F-017, F-018 | Update ICW-313 | ICW-313 ticket | With ICW-313 |
| F-019 | Update ICW-102 | ICW-102 ticket | After ICW-321 |
| F-020 | Update ICW-067 | ICW-067 ticket | With ICW-067 |
| F-021 | Update ICW-081 | ICW-081 ticket, ICW-307/306/305/063 | First, corpus safety |
| F-022 | Update ICW-129 | ICW-129 ticket, task tracker | Inside ICW-324 |
| F-023 | Update ICW-104/305 | ICW-305 ticket, active-tasks | With eviction policy |
| F-024, F-025, F-026 | Reject/Defer | none | none |

