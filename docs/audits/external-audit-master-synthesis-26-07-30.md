# External Audit Master Synthesis — Validated Findings and Master Task List

**HEAD:** 139a8b62fa2d6363615eb6a819d07a76aa8c55c2
**Date:** 2026-07-30
**Method:** 4 parallel subagents each reviewed a subset of 12 audit files against current code, cross-referenced against task tracker, ADRs, and requirements registry. Results merged below.

## Source Documents Reviewed

| File | Agent | Key Topics |
|---|---|---|
| `infinitecanvaswpf-pass6-delta-audit-26-07-27-12-19-26.md` | 1 | Coordinator defects, claimant tokens, shared claimant, GDI+ concurrency, ICW-078 regression |
| `infinitecanvaswpf-pass12-audit-26-07-28-22-55-21.md` | 1 | GDI+ concurrency in tile generation, SaveSettings fallback, dictionary access in tooltip |
| `infinitecanvaswpf-external-audit-validation-26-07-29-06-45-13.md` | 1, 4 | Cross-validation with external audits, active-count timing, pixelometer bypass, PixelCost mip gap, buffer reuse race, plan sequencing risk |
| `infinitecanvaswpf-plan-refinement-26-07-29-07-07-57.md` | 1, 4 | Plan refinements: Phase 0/1 sequencing, GDI+ touchpoint, settings consistency, PixelCost test, migration note |
| `infinitecanvaswpf-pass5-delta-audit-26-07-27-05-58-30.md` | 2 | Background noise reset, defect pool dispose race, GeneratorOptions defaults, ICW-078 regression |
| `infinitecanvaswpf-pass9-audit-26-07-27-19-33-49.md` | 2 | ObjectsPerTile validation mismatch, ICW-015 claims incomplete, NoiseOctaves default split |
| `infinitecanvaswpf-audit-pass7-supplementary-26-07-27-19-20-24.md` | 2 | Global exception handler swallows, GeneratorOptions.ImageCount placeholder |
| `infinitecanvaswpf-pass11-audit-26-07-28-06-43-17.md` | 2 | 19/21 async void handlers lack try, global handler silence, ICW-014 EventLog fallback confirmed |
| `infinitecanvaswpf-pass8-audit-26-07-27-19-19-38.md` | 3 | SpatialBounds zero-size risk, TileGridIndexLookup overflow, person-directed comments, LiveSpatialIndexService clean |
| `infinitecanvaswpf-pass10-audit-26-07-28-02-41-11.md` | 3 | Unused interfaces IRenderer/IBackgroundTileSource, ViewportScrollbarPolicy zero-guard clean, LinearSpatialIndexBuilder test-double |
| `infinitecanvaswpf-pass7-audit-26-07-27-19-13-25.md` | 3 | Defect-pool dispose fix pattern exists in OnClosed, BackgroundTileContracts unused types, CoalescingAsyncAction clean |

## Cross-Cutting Patterns

### Pattern A: Coordinator Concurrency Accounting Is Wrong (High, 3 findings, 0 tracked)

The coordinator's `_activeCount` is decremented before physical work stops (`CancelWorkItem`), not in the worker's terminal `finally`. During a cancellation burst (fast scrolling), `DefaultMaxConcurrency = 4` is not a real ceiling — simultaneous GDI+ operations are bounded only by stale delegate accumulation. Combined with claimant tokens hardcoded to `CancellationToken.None`, the auto-remove mechanism is dead code. DrainQueue never checks claimant-token liveness, so queued items with stale tokens can block usable ones.

**Source findings:** Pass6-1 (High), Pass6-2 (High), Pass6-3 (Medium), External-2 (High), External-1 (corroboration), PlanRef-1 (sequencing)
**Existing coverage:** None. ICW-142 (Done) built the coordinator but did not fix these integration defects.
**Proposed tasks:** ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN, ICW-P1-CLAIMANT-TOKENS
**Priority:** Before ICW-143 starts.

### Pattern B: Pixelometer Bypasses Cache Budget (Medium, 2 findings, 0 tracked)

`TryReadPixelValue` calls `TryGetPixelsNonBlocking` with `tryReserveCacheEntry` defaulting to `null`, completely bypassing `TileCacheBudget` accounting. Generated tiles from mouse hover are untracked and unrecoverable by the eviction system. Two independent audits confirm this mechanism at HEAD.

**Source findings:** External-3 (Medium), PlanRef-3 (settings pattern)
**Existing coverage:** None.
**Proposed tasks:** ICW-P0-PIXELOMETER-READOUT
**Priority:** Medium — interim fix (pass tryReserve through) is cheap.

### Pattern C: Background Noise Settings Silently Reset on Regenerate (High, 1 finding, 0 tracked)

`InitializeSpatialState()` creates a new `MainViewModel` with defaults on every `RegenerateSceneAsync`, silently discarding the user's persisted background-noise settings. The startup path loads settings correctly, then immediately discards them when the auto-regenerate fires. This breaks a just-shipped feature (ICW-066/ICW-067) on its primary user action.

**Source findings:** Pass5-1 (High), Pass9-3 (NoiseOctaves default split)
**Existing coverage:** None.
**Proposed tasks:** Background noise settings survival fix (fold into ICW-067 or new task)
**Priority:** High — single-method fix with high user impact.

### Pattern D: Settings Validation Is Not Unified Across Entry Points (Medium, 2 findings, partially tracked)

`CanvasUserSettings.IsValid` has no upper bound on `ObjectsPerTile`, while the generator enforces `MaxObjectsPerTile = 256` with a hard throw. Separately, `MinimumSparseTilePixelSize` is validated and stored but never passed to `GenerateFrozenBitmap`. Two known instances of the same pattern: settings validation differs across entry points.

**Source findings:** Pass9-1 (Medium), Pass12-1 (confirms), External-4 (PixelCost mip correction), PlanRef-3 (pattern)
**Existing coverage:** ICW-030 (Done, partial), ICW-099 (To Do, partial)
**Proposed tasks:** ICW-P1-SETTINGS-VALIDATION
**Priority:** Medium — one-line IsValid fix + structural pattern fix.

### Pattern E: Unused Rendering Abstractions (Medium, 2 findings, partially tracked)

2 of 5 interfaces in the codebase (IRenderer, IBackgroundTileSource) have zero implementers. Both are in `InfiniteCanvas.Rendering`. Combined with 4 supporting records in `BackgroundTileContracts.cs` (Descriptor, Request, Payload) that are unused in production, this is 40% of abstraction seams in dead code.

**Source findings:** Pass10-1 (Medium), Pass7-2 (Medium)
**Existing coverage:** ICW-018 (To Do, partial — only IRenderer/ViewportRenderRequest)
**Proposed tasks:** Expand ICW-018 to cover IBackgroundTileSource
**Priority:** Low — nothing broken, but cleanup before more abstractions accumulate.

## Complete Validated Findings Inventory

### High Severity

| # | Finding | Source | Tracked? | Priority |
|---|---|---|---|---|
| H1 | Claimant tokens hardcoded to CancellationToken.None (auto-removal path dead) | Pass6 §1 | No — ICW-142 (Done) built infra but didn't wire tokens | Before ICW-143 |
| H2 | Shared static DefaultCoordinatorClaimant (RemoveAllClaimants would cancel all tiles) | Pass6 §2 | No | Before ICW-143 |
| H3 | _activeCount decremented before physical work stops (concurrency cap not real during cancellation) | External §2, Pass12 §2 | No — ICW-142 doesn't fix this | Before ICW-143 |
| H4 | Defect-template-pool disposal races in-flight render (CancelAll() targets wrong actor) | Pass5 §2, Pass7 §1 | ICW-102 (To Do) | Before next regenerate |
| H5 | Background noise settings reset to defaults on every Regenerate | Pass5 §1 | No | Immediate UX fix |
| H6 | ICW-078/RenderRequestTracker wiring absent for 19+ commits (blocks ICW-143) | Pass5 §4, Pass6 §6 | ICW-100 (Proposed) — update status | Before ICW-143 |
| H7 | 19/21 async void handlers lack try blocks — global handler silently swallows all exceptions with no user signal | Pass11 §1 | ICW-014 (In Progress) — widen scope | Before next regression |
| H8 | Pixelometer-triggered generation bypasses TileCacheBudget (untracked, unrecoverable memory) | External §3 | No | Medium-high |
| H9 | DrainQueue does not check claimant-token liveness (stranded stale items block usable ones) | Pass6 §3, External §1 | No | Before ICW-143 |
| H10 | ReleaseReservation is a no-op counter (does not release budget bytes) | External §4 | ICW-134 (To Do — partial) | Before ICW-134 |

### Medium Severity

| # | Finding | Source | Tracked? |
|---|---|---|---|
| M1 | SpatialBounds permits zero Width/Height; DrawTile divides by zero with no guard | Pass8 §1 | No |
| M2 | BackgroundTileContracts unused abstraction scaffolding (IBackgroundTileSource, Descriptor, Request, Payload) | Pass7 §2, Pass10 §1 | ICW-018 (To Do — partial) |
| M3 | GeneratorOptions.ImageCount default is borrowed pixel-dimension constant (8192) | Pass5 §3, Pass7Supp §2 | ICW-088/090 (scope) |
| M4 | ObjectsPerTile settings-file validation doesn't match generator's hard limit (MaxObjectsPerTile = 256) | Pass9 §1 | No |
| M5 | Global exception handler swallows unconditionally with no user-visible signal | Pass7Supp §1 | ICW-014 (In Progress) |
| M6 | Concurrent GDI+ usage in tile generation (4+ simultaneous Bitmap/Graphics/FillEllipse/LockBits) | Pass12 §2, External §2 | No |
| M7 | Interaction between GDI+ concurrency and _activeCount timing (unbounded during cancellation) | External §2 | No |
| M8 | PixelCost undercounts mip memory by up to ~33% (mip-0-only computation) | External §4 | ICW-134 (To Do) |
| M9 | Front/back buffer InteropBitmap reuse races WPF compositor | External §5 | ICW-021 (To Do — template only) |
| M10 | Plan sequencing risk: Phase 6 defects should be Phase 0/1 | External §6, PlanRef §1 | No |
| M11 | Plan has settings/ViewModel blind spot | External §6 | No |
| M12 | GDI+ ApplyDetailsWithGdiPlus is a second touchpoint unaddressed by plan R-004 | PlanRef §2 | No |
| M13 | Migration sequencing strategy missing for active codebase iteration | PlanRef §5 | No |

### Low / Informational

| # | Finding | Source | Tracked? |
|---|---|---|---|
| L1 | TileGridIndexLookup unchecked int overflow (low likelihood at current scale) | Pass8 §2 | ICW-023 |
| L2 | Person-directed comments in shared source | Pass8 §3 | None |
| L3 | LiveSpatialIndexService CAS state machine confirmed clean | Pass8 §4 | None (record) |
| L4 | CoalescingAsyncAction lost-wakeup gap confirmed clean | Pass7 §3 | None (record) |
| L5 | ViewportScrollbarPolicy zero-guard pattern confirmed clean | Pass10 §2 | None (reference) |
| L6 | LinearSpatialIndexBuilder is test-double (not dead scaffolding) | Pass10 §3 | None (record) |
| L7 | BitmapConversionDuration null-deref verified fixed | Pass5 §5 | None (verified fixed) |
| L8 | Dead diagnostic properties + Gray8 0.0 ms status text | Pass5 §6 | ICW-023 |
| L9 | ICW-015 closure claims incomplete (missed third validation site) | Pass9 §2 | ICW-015 (note) |
| L10 | NoiseOctaves default split map (5 vs 3 across 4 sites) | Pass9 §3 | None |
| L11 | ICW-014 EventLog fallback verified accurate | Pass11 §2 | ICW-014 (confirmed) |
| L12 | Dictionary-indexer tooltip creation is concrete instance of tracked string-keyed pattern | Pass12 §3 | ICW-031/111 |
| L13 | Redundant parameter in ApplyScaleWithUniformFirst | Pass12 §4 | ICW-023 |
| L14 | BackgroundTileReadoutInfo is actually used (Pass 7 audit error: said zero references) | Pass7 §2 correction | Correct audit file |
| L15 | Handoff doc vs code discrepancy on eviction root cause | Pass6 §7 | None |

## Proposed New Tasks

### Phase 0: Safety Harness (must precede architectural refactoring)

| ID | Summary | Dependencies | Priority |
|---|---|---|---|
| ICW-P0-STALE-PUB | Add explicit stale-generation publication guard at tile completion callback, verified by injection test | ICW-078 epoch infra exists | Before ICW-143 |
| ICW-P0-ACTIVECOUNT | Move `_activeCount` decrement to worker terminal `finally` so concurrency cap represents physical execution only | ICW-142 coordinator exists | Before ICW-143 |
| ICW-P0-QUEUE-DRAIN | Add claimant-token liveness check in `DrainQueue` before advancing past non-running items | ICW-P0-ACTIVECOUNT | Before ICW-143 |
| ICW-P0-PIXELOMETER-READOUT | Convert pixelometer to published-frame snapshot; interim: pass `tryReserveCacheEntry` | None | Medium |
| ICW-P0-TRANSACTIONAL-REGEN | Wrap `RegenerateSceneAsync` in transactional guard with fallback to previous scene on failure | None | Medium |
| ICW-P0-BUFFER-REUSE-SYNC | Add synchronization or triple-buffering for `InteropBitmap` compositor handoff | ICW-021 (expand) | Medium |
| ICW-P0-SPATIAL-INDEX-SAFETY | Implement immutability copy-on-query for spatial index; add replace/move/delete publish tests | ICW-060, ADR-0003 | Medium |
| ICW-P0-LEASE-RELEASE | Replace `ReleaseReservation` counter with `IDisposable` lease pattern for exactly-once release | None | Before ICW-134 |
| ICW-P0-SEQUENCING | Restructure ICW-141 epic to include explicit Phase 0 and Phase 1 milestones | None | Immediate |
| ICW-P0-MIGRATION-GUARD | Add concurrent-development policy for the migration (freeze or strangler-fig) | None | Before Phase 1 |

### Phase 1: Correctness

| ID | Summary | Dependencies | Priority |
|---|---|---|---|
| ICW-P1-CLAIMANT-TOKENS | Wire per-frame/viewport `CancellationToken` into coordinator claimant API so frame-level cancellation fires | ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN | Before ICW-143 |
| ICW-P1-COOPERATIVE-CANCEL | Add cancellation token checks in tile generation factories around each expensive sub-phase | ICW-P1-CLAIMANT-TOKENS | After ICW-P0 |
| ICW-P1-GDI-CONCURRENCY | Add explicit GDI+ concurrency management for tile generation factories (dedicated worker or serialization) | ICW-P0-ACTIVECOUNT | After ICW-P0 |
| ICW-P1-SETTINGS-VALIDATION | Create single validation function per option field; fix `ObjectsPerTile` `IsValid` gap and `MinimumSparseTilePixelSize` consumption | None | Medium |
| ICW-P1-PIXELCOST-MIPS | Replace `_pixelCost` with sum of all resident mip payload bytes; add `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` test | ICW-134 scope expansion | After ICW-P0-LEASE-RELEASE |
| ICW-P1-SETTINGS-SCOPE | Add explicit acceptance criteria to ICW-022 for settings-bug fixes in Phase 1 of compatibility plan | ICW-022 | When ICW-022 starts |

### Existing Tasks Needing Status/Scope Updates

| ID | Current Status | Needed Change |
|---|---|---|
| ICW-078 | Done | Change to In Progress — wiring was reverted and never re-applied |
| ICW-100 | Proposed | Promote to To Do — tracks re-application of ICW-078 wiring |
| ICW-102 | Proposed/To Do | Update to require fencing `DisposeDefectTemplatePools` against in-flight render |
| ICW-103 | Done | Reopen? Or note that ICW-102 now covers the concern it warned about |
| ICW-134 | To Do | Expand scope to explicitly name `_pixelCost` mip-0-only defect; include `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` test |
| ICW-018 | To Do | Expand scope to explicitly include `IBackgroundTileSource` alongside `IRenderer`/`ViewportRenderRequest` |
| ICW-014 | In Progress | Add acceptance criterion: non-blocking user-visible signal on dispatcher exceptions |
| ICW-143 | To Do | Add dependency notes: requires ICW-P0-ACTIVECOUNT, ICW-P0-QUEUE-DRAIN, ICW-P1-CLAIMANT-TOKENS before viewport culling |
| ICW-141 | Proposed | Restructure to include Phase 0 and Phase 1 milestones before current ICW-142/143/144 scope |
| ICW-021 | To Do | Add concrete acceptance criteria for `InteropBitmap` compositor-safe reuse |
| ICW-023 | To Do | Add: TileGridIndexLookup overflow guard, dead diagnostic properties, redundant parameter cleanup |
| ICW-031/111 | To Do | Ensure `CreateAnnotationToolTip` (MainWindow.xaml.cs:724) is named as migration target |
| ICW-099 | To Do | Ensure it links to the settings-validation unification pattern (ICW-P1-SETTINGS-VALIDATION) |

## Required Documentation Updates

### ADR-0006 (viewport-aware tile work scheduling)
- Restructure §Implementation Sequence to Phase 0 → Phase 1 → Phase 2 (current ICW-142/143/144)
- Add mandates: `_activeCount` decremented in worker `finally` only
- Add mandates: queue drain checks claimant-token liveness before advancing past non-running items
- Add mandates: cache reservations use `IDisposable` lease pattern for exactly-once release
- Add mandates: claimant tokens originate from frame/viewport lifecycle, not static `CancellationToken.None`
- Add mandates: platform-interop drawing inside generation factories has explicit concurrency bounds
- Add note: active development migration sequencing policy
- Reference external audit validation as decision record

### ADR-0005 (background tile mips)
- Add explicit requirement: mip memory accounted separately from native tile memory using `ResourceKey` including mip level
- Add acceptance criterion: `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative`

### functional-requirements-and-invariants.md
- Add: Pixelometer readout must never initiate tile generation. Must consume published-frame snapshot only.
- Add: Regeneration must be atomic. Failure or cancellation restores the previous scene.
- Add: Retired frame surface must not be modified until WPF compositor confirms done reading.
- Add: Cache reservations use exactly-once release semantics enforced by `IDisposable`.
- Add: Tile generation factories must be cooperatively cancellable (check token per expensive sub-phase).
- Add: Every option field has exactly one canonical validation function shared by all entry paths.
- Add: Settings-file `ObjectsPerTile` must be validated against `MaxObjectsPerTile` before use.

### DesignDoc.md
- Add concurrency invariant: coordinator `_activeCount` represents physically executing factories only.
- Add reference to Phase 0 safety harness and external audit mandatory requirements.

### Audit file corrections
- `infinitecanvaswpf-pass7-audit-26-07-27-19-13-25.md` §2: correct `BackgroundTileReadoutInfo` reference count (it IS used in `MainWindow.xaml.cs` lines 1516, 1564, 1572 and in tests). The overall finding about unused scaffolding remains valid for the other 4 types.

## Prioritized Action Plan

### Immediate (before next ICW-143 work)
1. **Status corrections**: ICW-078 → In Progress, ICW-100 → To Do, ICW-078 dependency in ICW-143 → ICW-100
2. **ICW-P0-SEQUENCING**: Restructure ICW-141 epic with Phase 0/1 milestones
3. **Fix H5**: Stop `InitializeSpatialState()` from resetting `MainViewModel`/background-noise settings

### Before ICW-143 (viewport culling)
4. **ICW-P0-ACTIVECOUNT**: Fix `_activeCount` timing (move decrement to worker `finally`)
5. **ICW-P0-QUEUE-DRAIN**: Add claimant-token liveness check in `DrainQueue`
6. **ICW-P1-CLAIMANT-TOKENS**: Wire per-frame/viewport `CancellationToken` into coordinator
7. **Fix H1/H2**: Make claimant identity per-tile, wire real cancellation tokens
8. **Fix H6**: Re-apply ICW-078 `RenderRequestTracker` wiring (ICW-100)

### Medium-term
9. **ICW-P0-PIXELOMETER-READOUT**: Fix pixelometer cache budget bypass
10. **ICW-P0-TRANSACTIONAL-REGEN**: Add transactional regeneration guard
11. **ICW-P1-SETTINGS-VALIDATION**: Fix ObjectsPerTile IsValid gap and MinimumSparseTilePixelSize consumption
12. **ICW-P0-LEASE-RELEASE**: Replace ReleaseReservation with IDisposable lease
13. **ICW-P1-PIXELCOST-MIPS**: Fix PixelCost to account for all mip levels (expand ICW-134 scope)

### Architecture cleanup
14. **Expand ICW-018**: Decide fate of IRenderer, IBackgroundTileSource (implement, delete, or ADR)
15. **Fix M1**: Tighten SpatialBounds constructor or add DrawTile zero-guard
16. **ICW-P0-BUFFER-REUSE-SYNC**: Fix InteropBitmap compositor race (expand ICW-021 scope)
17. **ICW-P0-SPATIAL-INDEX-SAFETY**: Spatial index publish safety (start ICW-060)
18. **ICW-P1-GDI-CONCURRENCY**: GDI+ concurrency management for tile generation
19. **ICW-P1-COOPERATIVE-CANCEL**: Make tile factories cooperatively cancellable

### Low-priority cleanup
20. **ICW-023 expansion**: TileGridIndexLookup overflow, dead diagnostic properties, redundant parameter
21. **Audit file corrections**: Fix BackgroundTileReadoutInfo reference count in pass 7
22. **ICW-015 notes addendum**: Acknowledge third validation site
23. **Resolve NoiseOctaves default split**: Centralize at single value

## Summary Statistics

| Metric | Count |
|---|---|
| Total findings reviewed | 53 |
| High severity | 10 |
| Medium severity | 13 |
| Low / Informational | 15 |
| Verified fixed | 1 |
| Already adequately tracked | 9 |
| Partially tracked (needs scope expansion) | 8 |
| Not tracked (needs new task) | 12 |
| Audit file corrections needed | 1 |
| Proposed new Phase 0 tasks | 10 |
| Proposed new Phase 1 tasks | 6 |
