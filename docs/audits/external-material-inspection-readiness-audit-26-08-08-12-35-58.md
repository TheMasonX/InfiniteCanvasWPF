# External Material Inspection Viewport Readiness Audit

**Description:** Net-new readiness review after the 2026-08-07 delta. The review checks current source paths, working-tree changes, existing tasks, and focused tests.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `main` at `e0b0669a0445be30356e04f2e6f479044feb3851`
**Latest commit:** `e0b0669a0445be30356e04f2e6f479044feb3851` - `refactor: ship retained annotation overlay only`
**ID Hash:** `external-material-inspection-readiness-2026-08-08`
**Author:** GitHub Copilot, InfiniteCanvas Agent
**Timestamp:** 2026-08-08 12:35 US Central
**Review mode:** Net-new only, with corrections and extensions to existing tasks
**Scope:** Materializer integration, tile identity, frame ownership, frame publication, external-host evidence, and related task records

## Executive Summary

This audit reviews the current working tree against the external material inspection viewport requirement.
The repository remains not ready to replace an external material inspection viewport.

The review records one Standards finding and three Spec findings.
All findings extend existing work. No new ICW key is required.

The highest-risk residual is full tile identity loss at the raster boundary.
The materializer and cache use source, tile, revision, and mip identity, but the active raster path reduces that identity to `tile.Id`.
An external host with colliding tile IDs can display the wrong payload.

The working tree now snapshots the frame item sequence and copies public payload input bytes.
The frame still accepts a mutable raster and mutable item implementations without an ownership check.
The materializer also lacks a direct same-epoch duplicate-worker completion test.

The review did not modify source code or run source tests.
The task tracker validator is the required documentation validation step for this change.

## Review Method and Coverage

The review read the 2026-08-07 readiness delta before inspecting current source.
It compared current source behavior with the requirements registry, ADR-0005, existing ICW tickets, and focused tests.

The working tree contains pre-existing uncommitted source, test, and task changes.
The audit treats those changes as current evidence and does not attribute them to this audit.

The materializer race challenge compared `BackgroundTileMaterializer.Request` and `Complete` with `TileWorkCoordinator` cancel-and-re-request behavior.
The source guards scene replacement with a scene epoch.
The normal claimant cancellation path removes callbacks before a canceled worker completes.
The remaining same-epoch ordering case needs a direct regression test.

The review did not run the Core test suite, Windows test suite, App build, benchmark run, WPF runtime stress loop, or external source parity host.

## Table of Findings

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task | Sources |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| S-001 | Dual materialization ownership remains | Standards | Update | Confirmed | P1 | 95% | ICW-076 | S6, S11, S12, S17, S18, S19 |
| F-001 | Full tile identity is lost before raster composition | Spec | Update | Confirmed | P0 | 95% | ICW-339, ICW-340, ICW-076 | S2, S6, S11, S12, S14, S18, S19 |
| F-002 | Frame raster and item ownership remain unenforced | Spec | Update | Confirmed | P1 | 95% | ICW-338 | S2, S7, S15, S16 |
| F-003 | Same-epoch duplicate completion lacks direct proof | Spec | Defer | Unverified | P2 | 72% | ICW-076, ICW-341 | S6, S11, S12, S13, S21 |

## Findings

### S-001 Dual Materialization Ownership Remains

**Axis:** Standards
**Provenance:** Extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 95%, because the active materializer path and legacy tile-owned path are both visible in current source.
**Origin:** Independent current-source review after the 2026-08-07 readiness delta

#### Description

The current migration has two materialization ownership paths.
`MainWindow` requests source-neutral payloads through `BackgroundTileMaterializer`.
`SampleImageTile` still owns direct pixel factories, legacy cache state, and coordinator callbacks.
`ZeroCopyBitmapFactory` still exposes both the direct tile-generation path and the resident-payload path.

This creates duplicated ownership and divergent-change risk.
A future external adapter can update one path while the active raster path still uses another path.

#### Rationale

`MainWindow.RenderFrameAsync` requests the materializer for visible tiles at [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L597-L613).
The same method builds resident payloads for composition at [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L625-L639).

`SampleImageTile` still resolves requests through tile-owned factories at [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs#L127-L151).
It also retains direct cache keys and generation state at [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs#L424), [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs#L584), and [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs#L792).

`ZeroCopyBitmapFactory` still contains the direct tile-generation overload at [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs#L114-L165) and the resident payload overload at [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs#L178-L220).

This is a Standards finding because the partial migration duplicates the materialization boundary and increases divergent-change risk.
The external replacement requirement also remains unmet, but that Spec consequence belongs to ICW-076 and the earlier readiness findings.

#### Counter-evidence and Deduplication

The materializer is now active in `MainWindow`, so the prior claim that it was entirely inactive is no longer current.
The source-neutral contracts and focused materializer tests are real progress.
This finding does not reopen ICW-142, ICW-143, ICW-205, ICW-327, or ICW-330.

#### Recommendation and Validation

Complete ICW-076 by making one path own request admission, payload residency, and raster consumption.
Keep the sample source as an adapter, but remove or isolate legacy direct generation from the active external path.

Add a source-level ownership test and an integration test that prove the active raster path never invokes the legacy tile-owned generation path.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | [ICW-076](../tasks/tickets/ICW-076-background-tile-mip-levels.md) | Owns materializer migration |
| ADR | [ADR-0005](../ADR/0005-source-agnostic-background-tile-mips.md) | Defines source-neutral tile ownership |
| Test | [BackgroundTileMaterializerTests.cs](../../tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs) | Covers materializer behavior only |

#### Finding Sources

S6, S11, S12, S17, S18, and S19.

### F-001 Full Tile Identity Is Lost Before Raster Composition

**Axis:** Spec
**Provenance:** Extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P0
**Confidence:** 95%, because current source directly converts full cache-key results into a tile-ID-only dictionary and performs tile-ID-only lookup.
**Origin:** Independent current-source review against the source-qualified tile requirement

#### Description

The materializer stores and resolves full `BackgroundTileCacheKey` values.
The active render path stores resident payloads in `Dictionary<string, BackgroundTilePayload>`.
The rasterizer then retrieves payloads by `tile.Id`.

This loses source identity, content revision, mip level, and any future layer identity before composition.
Two external sources or layers can use the same tile ID.
The later payload can replace the earlier payload, or the rasterizer can use a payload from a different revision or mip.

#### Rationale

`BackgroundTileCacheKey` contains source ID, tile ID, content revision, and mip level at [BackgroundTileContracts.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs#L55-L64).
`BackgroundTileMaterializer` indexes resident payloads by that complete key at [BackgroundTileMaterializer.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L16-L20).

`MainWindow` creates a tile-ID-only dictionary at [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs#L625-L631).
`ZeroCopyBitmapFactory` accepts that string-keyed dictionary at [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs#L178-L192).
`DrawResidentTile` checks and retrieves the payload by `tile.Id` at [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs#L249-L255).

The source adapter also indexes its current sample set by tile ID at [SampleImageTileSource.cs](../../src/InfiniteCanvas.Rendering/SampleImageTileSource.cs#L8-L17).
That sample-only choice does not satisfy the external source-qualified contract.

The requirements registry requires source, tile, content revision, and mip identity for cache keys and active-frame pins at [functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md#L43).

#### Counter-evidence and Deduplication

The current sample scene uses unique tile IDs, so the existing single-source tests do not reproduce the collision.
The materializer and coordinator already preserve complete keys internally.
This finding is therefore a boundary extension, not a new cache-key implementation task.

ICW-339 owns semantic source and layer identity.
ICW-340 owns the atomic layer plan.
ICW-076 owns the tile materializer and cache migration.
No new ICW key is justified.

#### Recommendation and Validation

Carry `BackgroundTileCacheKey` or an equivalent source-qualified value through the resident payload map and layer plan.
Do not convert a payload map to `Dictionary<string, ...>` before raster composition.

Add a Windows regression test with two payloads that share a tile ID but differ in source or revision.
Compose both tiles and assert that each tile receives its own payload bytes.
Add a mip collision case so a requested mip cannot consume a different resident variant by ID alone.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | [ICW-339](../tasks/tickets/ICW-339-semantic-material-viewport-identity.md) | Owns semantic identity |
| Task | [ICW-340](../tasks/tickets/ICW-340-atomic-material-layer-plan-publication.md) | Owns the frame and layer boundary |
| Task | [ICW-076](../tasks/tickets/ICW-076-background-tile-mip-levels.md) | Owns source-qualified tile materialization |
| Requirement | [Functional requirements](../requirements/functional-requirements-and-invariants.md#L43) | Requires complete tile identity |
| Test | [ZeroCopyBitmapFactoryTests.cs](../../tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs#L244-L267) | Current test uses one tile-ID key only |

#### Finding Sources

S2, S6, S11, S12, S14, S18, and S19.

### F-002 Frame Raster And Item Ownership Remain Unenforced

**Axis:** Spec
**Provenance:** Extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 95%, because the current constructor snapshots the list but does not enforce raster freezing or element-level stability.
**Origin:** Residual ownership review after the working-tree changes

#### Description

The current working tree now copies the `CanvasFrame` item sequence.
It also copies public `BackgroundTilePayload` input bytes.
The frame still accepts any `BitmapSource`, even though the contract describes a frozen raster.
The item sequence remains a shallow copy, so mutable item implementations can change after frame acceptance.

The external viewport cannot rely on a stable accepted snapshot unless the boundary enforces or clearly owns raster and item state.

#### Rationale

`CanvasFrame` accepts `BitmapSource` without checking `IsFrozen` at [CanvasFrame.cs](../../src/InfiniteCanvas.Controls/CanvasFrame.cs#L18-L20).
It stores the raster directly at [CanvasFrame.cs](../../src/InfiniteCanvas.Controls/CanvasFrame.cs#L76-L86).
The current working tree does snapshot the item sequence with `items.ToArray()` at [CanvasFrame.cs](../../src/InfiniteCanvas.Controls/CanvasFrame.cs#L76).

`ICanvasItem` exposes getter-only members, but the contract does not require immutable implementations or value snapshots at [ICanvasItem.cs](../../src/InfiniteCanvas.Core/ICanvasItem.cs#L7-L13).
Getter-only access does not prevent a host-owned object from changing its returned bounds after publication.

The payload constructor now copies public input bytes and exposes read-only memory at [BackgroundTileContracts.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs#L105-L151).
That change closes the earlier byte-array aliasing case, but it does not close raster or element ownership.

The published payload requirement requires stable read-only frame and cache input at [functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md#L45).

#### Counter-evidence and Deduplication

The item-list mutation case from the 2026-08-07 audit is corrected in the working tree.
The payload source-array mutation case also has focused test coverage in [BackgroundTileMaterializerTests.cs](../../tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs#L65-L79).
This finding records only the residual raster and element-level ownership gap.

The issue remains under ICW-338 and does not reopen ICW-316A.

#### Recommendation and Validation

Choose one explicit boundary policy for rasters.
Reject non-frozen rasters, or create and freeze an owned copy before publication.

Choose one explicit policy for item state.
Require immutable item implementations, or snapshot the identity and bounds needed by the frame.

Add tests for non-frozen raster input and post-publication item mutation.
Run a concurrent read test that publishes and consumes the accepted frame while the rejected mutable input changes.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | [ICW-338](../tasks/tickets/ICW-338-immutable-frame-and-payload-ownership.md) | Owns remaining frame and payload ownership |
| Task | [ICW-316A](../tasks/tickets/ICW-316A-harden-canvas-contracts.md) | Earlier contract hardening task |
| Requirement | [Functional requirements](../requirements/functional-requirements-and-invariants.md#L45) | Requires stable published inputs |
| Test | [CanvasControlConsumerHostTests.cs](../../tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs#L103-L128) | Covers item-list sequence ownership only |

#### Finding Sources

S2, S7, S15, and S16.

### F-003 Same-Epoch Duplicate Completion Lacks Direct Proof

**Axis:** Spec
**Provenance:** Extension
**Task disposition:** Defer
**Verification:** Unverified
**Severity:** P2
**Confidence:** 72%, because the coordinator permits duplicate physical workers, while normal claimant removal and scene epochs provide partial protection.
**Origin:** Focused control-flow challenge of materializer completion ordering

#### Description

`TileWorkCoordinator` admits a new worker when an older worker for the same key is already canceled.
`BackgroundTileMaterializer` tracks one in-flight scene epoch per key, not a per-operation generation.

The normal frame-token cancellation path removes the canceled worker's claimant callback.
Scene replacement advances the materializer epoch and rejects the old completion.
The remaining same-epoch duplicate-worker case lacks a direct materializer regression test that proves reservation release and callback ordering.

#### Rationale

The coordinator excludes canceled items from coalescing at [TileWorkCoordinator.cs](../../src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs#L176-L199).
Its comments explicitly accept overlapping workers during cancel-and-re-request.

The materializer captures only `requestEpoch` and uses one `_inFlightEpochs` entry per key at [BackgroundTileMaterializer.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L158-L208).
Completion checks the scene epoch at [BackgroundTileMaterializer.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L258-L280).
The release guard compares the current key epoch at [BackgroundTileMaterializer.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs#L290-L300).

These guards reject old scene results and avoid ordinary claimant callbacks after frame-token cancellation.
They do not directly prove the same-epoch duplicate completion contract.

#### Counter-evidence and Deduplication

The source does not currently demonstrate a stale payload overwrite under the normal claimant lifecycle.
The existing coordinator tests cover cancel-and-re-request ownership, and the materializer tests cover scene advancement.
This audit therefore does not create a new correctness task or claim a reproduced race.

#### Recommendation and Validation

Extend ICW-076 with a deterministic source that blocks two same-key workers in one scene epoch.
Cancel the first claimant, re-request the key, release the workers in both orders, and assert one resident payload, one reservation release, and the correct callback.
Include the `CancelAll` path because it preserves coordinator claimant entries until worker completion.

Use ICW-341 as the host-level evidence gate after the focused test passes.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | [ICW-076](../tasks/tickets/ICW-076-background-tile-mip-levels.md) | Owns materializer completion and reservation behavior |
| Task | [ICW-341](../tasks/tickets/ICW-341-external-host-parity-and-runtime-stress.md) | Owns runtime and host evidence |
| Test | [BackgroundTileMaterializerTests.cs](../../tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs#L7-L79) | Current materializer coverage lacks this ordering case |
| Test | [TileWorkCoordinatorTests.cs](../../tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs) | Existing coordinator cancel-and-re-request coverage |

#### Finding Sources

S6, S11, S12, S13, and S20.

## Corrections and Extensions to Existing Tasks

- ICW-076 remains In Progress. The materializer is now active in the request path, but the raster boundary still uses tile-ID-only payload lookup and legacy tile-owned generation remains present. Add the full-key boundary and same-epoch completion test to the acceptance criteria.
- ICW-338 remains Proposed. The working tree now snapshots the item sequence and copies public payload input bytes. Keep the task open for frozen raster enforcement, element-level item stability, concurrent reads, and validation of the current uncommitted changes.
- ICW-339 remains Proposed. Extend semantic identity acceptance so source-qualified tile and layer identity survives from the source contract through the accepted frame and raster payload map.
- ICW-340 remains Proposed. Require the immutable layer plan to carry complete tile identity and add collision tests for equal tile IDs with different source, revision, or mip values.
- ICW-341 remains Proposed. Add external-host parity evidence for identity collisions, same-epoch completion ordering, and WPF lifecycle stress.
- ICW-337 remains Proposed. It continues to coordinate the five existing child tasks. This audit adds no new ICW key.
- ICW-316A and ICW-328 remain complete for their original count, lifecycle, and integer ordering gates. This audit does not reopen those tasks.

## Priority Order

1. P0, ICW-339 and ICW-340, define semantic identity and publish identity-preserving layers atomically.
2. P0 readiness gate, ICW-076, complete the active materializer migration and preserve full tile identity through raster composition.
3. P1, ICW-338, enforce raster and item ownership at the frame boundary.
4. P1, ICW-341, prove external host parity and WPF lifecycle behavior.
5. P2, ICW-076 and ICW-341, close the same-epoch duplicate-worker evidence gap.

## Open Questions and Validation Gaps

| ID | Question | Why it matters | Cheapest resolution | Owner |
| --- | --- | --- | --- | --- |
| Q-001 | Does the external host allow equal tile IDs across sources or layers? | The current raster map can select the wrong payload when IDs collide. | Add the collision test with two source-qualified requests. | ICW-339, ICW-340 |
| Q-002 | Does the frame contract require immutable item values or only stable identity and bounds? | A shallow item copy does not freeze host-owned item state. | Confirm the external host item lifetime and choose value snapshot or immutable implementation. | ICW-338 |
| Q-003 | Can `CancelAll` occur without a scene advance while a same-key replacement request remains active? | The materializer has a scene epoch but no per-operation generation. | Run the deterministic two-worker ordering test. | ICW-076 |
| Q-004 | What external layer order and visibility rules define parity? | ICW-340 cannot prove deterministic ordering without a fixed layer source of truth. | Approve the neutral layer plan contract before implementation. | ICW-340 |

| ID | Assumption | Effect if false | Evidence needed | Owner |
| --- | --- | --- | --- | --- |
| A-001 | The current uncommitted source changes represent the intended working-tree state. | Ownership findings can change after the changes are committed or revised. | Re-run source review at the implementation commit. | Repository owner |
| A-002 | External material sources can produce colliding tile IDs across layers or revisions. | F-001 severity can decrease for a single-source host, but the reusable contract still lacks the required identity boundary. | External host contract or collision test. | ICW-339 |

## Requests

| Priority | Request | Rationale | Required response |
| --- | --- | --- | --- |
| P1 | Confirm the external source and layer identity contract. | This decision controls the value type carried by the layer plan and payload map. | Approve source, layer, tile, revision, and mip identity fields. |
| P1 | Run the focused ownership and materializer ordering tests after the current working-tree changes settle. | The audit uses uncommitted source evidence and cannot claim implementation validation. | Provide Core and Windows test results plus the App build result. |

## Source Ledger

| ID | Source | Type | Revision or date | Read directly | Use and limitation |
| --- | --- | --- | --- | --- | --- |
| S1 | [viewport-material-inspection-readiness-delta-2026-08-07.md](viewport-material-inspection-readiness-delta-2026-08-07.md) | Prior audit | 2026-08-07 | Yes | Baseline for net-new classification |
| S2 | [functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md) | Requirement registry | Working tree | Yes | Defines identity, atomic publication, and ownership requirements |
| S3 | [active-tasks.md](../tasks/active-tasks.md) | Tracker | Working tree | Yes | Existing task status and evidence |
| S4 | [task-tracker.md](../tasks/task-tracker.md) | Tracker | Working tree | Yes | Existing task log and activity |
| S5 | [ICW-337 ticket](../tasks/tickets/ICW-337-external-material-inspection-readiness.md) | Task | 2026-08-07 | Yes | Readiness epic and child-task ownership |
| S6 | [ICW-076 ticket](../tasks/tickets/ICW-076-background-tile-mip-levels.md) | Task | Working tree | Yes | Materializer migration and completion requirements |
| S7 | [ICW-338 ticket](../tasks/tickets/ICW-338-immutable-frame-and-payload-ownership.md) | Task | Working tree | Yes | Frame and payload ownership requirements |
| S8 | [ICW-339 ticket](../tasks/tickets/ICW-339-semantic-material-viewport-identity.md) | Task | Working tree | Yes | Semantic source and frame identity |
| S9 | [ICW-340 ticket](../tasks/tickets/ICW-340-atomic-material-layer-plan-publication.md) | Task | Working tree | Yes | Atomic layer publication |
| S10 | [ICW-341 ticket](../tasks/tickets/ICW-341-external-host-parity-and-runtime-stress.md) | Task | Working tree | Yes | External host and runtime evidence |
| S11 | [MainWindow.xaml.cs](../../src/InfiniteCanvas.App/MainWindow.xaml.cs) | Source | Working tree | Yes | Active request and raster payload assembly |
| S12 | [BackgroundTileMaterializer.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileMaterializer.cs) | Source | Working tree | Yes | Cache identity, epochs, and completion acceptance |
| S13 | [TileWorkCoordinator.cs](../../src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs) | Source | Working tree | Yes | Cancel-and-re-request behavior |
| S14 | [BackgroundTileContracts.cs](../../src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs) | Source | Working tree | Yes | Full tile cache-key contract and payload ownership |
| S15 | [CanvasFrame.cs](../../src/InfiniteCanvas.Controls/CanvasFrame.cs) | Source | Working tree | Yes | Frame raster and item boundary |
| S16 | [ICanvasSceneSource.cs](../../src/InfiniteCanvas.Core/ICanvasSceneSource.cs) | Source | Working tree | Yes | Unqualified scene change contract |
| S17 | [SampleImageTile.cs](../../src/InfiniteCanvas.Rendering/SampleImageTile.cs) | Source | Working tree | Yes | Legacy tile-owned materialization paths |
| S18 | [SampleImageTileSource.cs](../../src/InfiniteCanvas.Rendering/SampleImageTileSource.cs) | Source | Working tree | Yes | Sample adapter identity boundary |
| S19 | [ZeroCopyBitmapFactory.Windows.cs](../../src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs) | Source | Working tree | Yes | Resident payload lookup and raster composition |
| S20 | [BackgroundTileMaterializerTests.cs](../../tests/InfiniteCanvas.Tests/BackgroundTileMaterializerTests.cs) | Test | Working tree | Yes | Materializer and payload ownership coverage |
| S21 | [CanvasControlConsumerHostTests.cs](../../tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs) | Test | Working tree | Yes | Generic host and frame sequence coverage |
| S22 | [ZeroCopyBitmapFactoryTests.cs](../../tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs) | Test | Working tree | Yes | Single tile-ID resident payload coverage |
| S23 | [ADR-0005-source-agnostic-background-tile-mips.md](../ADR/0005-source-agnostic-background-tile-mips.md) | ADR | Repository | Yes | Defines full-key materializer ownership, resident payload boundaries, and migration sequence |

## Task and Sprint Updates

| Finding | Task action | Tracker locations | Sprint impact |
| --- | --- | --- | --- |
| S-001 | Update ICW-076 with the dual ownership and active-path migration evidence. | [ICW-076 ticket](../tasks/tickets/ICW-076-background-tile-mip-levels.md), [active-tasks.md](../tasks/active-tasks.md), [task-tracker.md](../tasks/task-tracker.md) | Keep materializer migration ahead of external host evidence. |
| F-001 | Extend ICW-339, ICW-340, and ICW-076 with full-key preservation and collision tests. | [ICW-339 ticket](../tasks/tickets/ICW-339-semantic-material-viewport-identity.md), [ICW-340 ticket](../tasks/tickets/ICW-340-atomic-material-layer-plan-publication.md), [ICW-076 ticket](../tasks/tickets/ICW-076-background-tile-mip-levels.md) | Preserve P0 identity ordering. |
| F-002 | Update ICW-338 with frozen raster and element-level ownership acceptance. | [ICW-338 ticket](../tasks/tickets/ICW-338-immutable-frame-and-payload-ownership.md), [active-tasks.md](../tasks/active-tasks.md), [task-tracker.md](../tasks/task-tracker.md) | Keep ownership validation before external host proof. |
| F-003 | Add the same-epoch duplicate-worker ordering test to ICW-076 and host evidence to ICW-341. | [ICW-076 ticket](../tasks/tickets/ICW-076-background-tile-mip-levels.md), [ICW-341 ticket](../tasks/tickets/ICW-341-external-host-parity-and-runtime-stress.md) | Defer until the focused materializer contract is testable. |

Finding count: Standards 1, Spec 3. Worst Standards issue: dual materialization ownership during the active migration. Worst Spec issue: full tile identity is lost before raster composition.