# InfiniteCanvasWPF — Delta Report: Wave E Verification + New Exhaustive-Review Findings

**Previous reports:** `infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md` (commit `afa8b5b8`), `infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md` (commit `596fea64`) — both now committed into `docs/audits/` by the project itself.
**This report's commit:** `main` tip at time of review (GitHub API was rate-limited for exact-SHA lookup; retrieved via `codeload.github.com/.../tar.gz/main`, which always resolves to the current tip regardless of API rate limits — content cross-checked against the new handoff/council docs' own stated HEAD references, which agree). Message trail confirms this is the state after **Sprint 1 Wave E**.
**Scope of this report:** per instructions, this contains **only new findings and corrections** — it does not repeat anything already reported. Findings already identified and already fixed by the project's own Wave D "council review" (a real bug in `PublishInterestSet` dropping failure callbacks, a missing null guard on `ViewportInterestSet`, a missing `_disposed` check) are **not** re-reported as new; §1 briefly confirms they're genuinely fixed, since "review the changes" was explicitly asked, but no new guidance is attached to them.

---

## 1. Verification of Changes Since the Last Report (Wave E)

Diffed `TileWorkCoordinator.cs` and `BackgroundTileContracts.cs` (the only two source files touched) against the exact copies read in the previous session. Confirmed genuine, correct fixes, matching `docs/handoffs/2026-07-30-sprint1-wave-d-supplementary-review.md`'s claims:
- `PublishInterestSet` now calls `CancelWorkItem` directly instead of removing claimants first — this closes a real bug where `DispatchFailed`'s claimant snapshot would already be empty by the time it ran, permanently stranding `_generationQueued = 1` on culled tiles. Diff-confirmed correct. **95% confidence.**
- `DrainQueueWithLivenessCheck` gained a `_disposed` check at its top, bringing it in line with the class's other lock-holding methods. Diff-confirmed. **95% confidence.**
- `ViewportInterestSet` converted from primary-constructor syntax to an explicit constructor with `ArgumentNullException.ThrowIfNull` guards — confirmed in `BackgroundTileContracts.cs`. **95% confidence.**

One thing worth flagging that the project's own review didn't mention: the council review's remaining-known-issues table already lists several O(n)/allocation-related performance smells in this same code (`GetClaimantIds()` LINQ allocation, O(n) `RemoveFromQueue` rebuild, O(n) scan-ahead under lock). Those are already tracked; §2.6 below adds one **the council review's list didn't catch**, in the same family.

---

## 2. New Findings

### 2.1 `CancelWorkItem`'s "caller must hold `_lock`" contract is undocumented and inconsistent with sibling methods — a live hazard given how this class is being edited

**Finding:** `TileWorkCoordinator.CancelWorkItem` (the same method both of my previous reports' `ICW-P0-ACTIVECOUNT` analysis and the new `ICW-P0-ACTIVECOUNT-residuals` ticket focus on) takes **no lock of its own** — it is only safe to call from within a block that already holds `_lock` (its callers — `RemoveClaimant`, `RemoveAllClaimants`, `PublishInterestSet`, `DrainQueueWithLivenessCheck` — all wrap their calls to it in their own `lock (_lock) { ... }`). Nothing in the code — no comment, no `<remarks>` doc, no naming convention (e.g., a `_NoLock`/`_Locked` suffix) — states this requirement. Meanwhile sibling methods in the *same class* (`HandleWorkStopped`, `DrainQueueWithLivenessCheck`, `PublishInterestSet`) **do** take their own lock. This inconsistency (some private/public methods self-lock, others assume the caller already holds the lock, with no way to tell which is which except by reading the body) is exactly the kind of implicit contract that's easy to violate under exactly the conditions this codebase is currently operating in: **five sequential "waves," each adding new methods to this same class, evidently by separate sessions/agents** (per the handoff docs' own account). A future addition that calls `CancelWorkItem` without already holding `_lock` would compile cleanly and could pass every existing test, while introducing a genuine data race (e.g., two threads both passing the `item.State is Completed/Failed/Canceled` early-return check before either commits `item.State = Canceled`).

**Why this matters more than a generic style nit:** the project has already created `ICW-P0-MIGRATION-GUARD`, a *process*-level ticket about exactly this risk category ("two agents could independently modify the same subsystem... with conflicting changes"). This finding is the concrete, code-level instance of that abstract concern, in the exact file the guard ticket names as an example (`TileWorkCoordinator`). It also directly touches `ICW-P0-ACTIVECOUNT-residuals`' proposed restructuring of `CancelWorkItem`'s tail — that ticket's fix should add the missing contract documentation while it's already editing this method, rather than leaving the ambiguity for the next wave.

**Recommendation:** add an XML `<remarks>` or a plain comment directly above `CancelWorkItem`'s signature: `// Caller must hold _lock. Not safe to call independently.` Apply the same audit to `RemoveFromQueue` (same pattern, not independently re-verified line-by-line this pass, but its call sites all appear to be under lock already based on a grep of callers). Consider, as a longer-term structural fix, renaming lock-dependent private helpers with a consistent suffix (e.g., `CancelWorkItemLocked`) so the convention is visible at every call site without needing to open the method body — a low-cost, high-leverage readability change for a class under this much concurrent editing pressure.

**Confidence:** 90% (directly traced: confirmed no lock inside `CancelWorkItem`, confirmed all current call sites hold the lock, confirmed no documentation exists). **New ticket recommended** (not a clean fit inside any existing one): `ICW-P0-LOCK-CONTRACT-DOCS`, `related: [ICW-P0-ACTIVECOUNT-residuals, ICW-P0-MIGRATION-GUARD]`.

### 2.2 `BackgroundTileCacheKey.SourceId = "synthetic"` is a magic-string literal duplicated across 6 call sites in 3 files

**Finding:** grep confirms the literal string `"synthetic"` is hand-typed at 6 separate call sites: `MainWindow.xaml.cs:388,393`, `SampleImageTile.cs:327,436,570`, and `SampleImageGeneratorTests.cs:311`. `BackgroundTileCacheKey` is a `record struct` used as the coordinator's dictionary/queue key — its equality is fully structural, so `SourceId` must match **character-for-character** across every call site for cache-key coalescing (and hence the entire concurrency-safety story built across Sprint 1 — active-count accounting, claimant tracking, interest-set culling) to work at all. There is no shared constant; a typo at any one of the 6 sites would silently produce phantom, never-matching cache entries with zero compiler error.

**Why now, not before:** `SourceId` exists as a `string` specifically to support multiple tile sources (this is the same abstraction `IBackgroundTileSource`/`BackgroundTileDescriptor` — flagged as dead/underused in my first report's `ICW-018` findings — was evidently meant to eventually provide). Today there is exactly one source, and it's spelled out by hand 6 times instead of referencing one constant. This is the same "unfinished abstraction" theme as `ICW-018` and the `GeneratorOptions`/`MipOptions` findings below — worth grouping together conceptually even though they're different tickets.

**Recommendation:** add `public const string SyntheticSourceId = "synthetic";` next to `BackgroundTileCacheKey`'s definition in `BackgroundTileContracts.cs`; update all 6 call sites (5 production + 1 test) to reference it.

**Confidence:** 95% (fully grep-confirmed). **Recommend as an extension to `ICW-018`** (dormant rendering abstractions) rather than a new ticket — same root cause (the multi-source abstraction was never finished), same file.

### 2.3 `ISpatialIndexService<T>` is a shallow interface, forcing a concrete-type downcast in `CanvasViewportViewModel<T>`

**Finding:** `ISpatialIndexService<T>` exposes exactly two members: `Count` and `Query(SpatialBounds)`. `LiveSpatialIndexService<T>` (one of at least three implementations, alongside `ImmutableSpatialIndexService<T>` and whatever wraps `StrTreeSpatialIndexService`) has substantially more capability — snapshot publishing (`PublishSnapshotAsync`), a `LastPublishedAtUtc` timestamp, and presumably the mutation API used to add/update entities — none of which is part of the interface contract. `CanvasViewportViewModel<T>.ApplyFrame` needs `LastPublishedAtUtc` for its `LastSnapshotPublishedAtUtc` observable property, and the only way to get it is:
```csharp
if (_spatialIndexService is LiveSpatialIndexService<T> liveSpatialIndexService)
{
    LastSnapshotPublishedAtUtc = liveSpatialIndexService.LastPublishedAtUtc;
}
```
This is a textbook "shallow module" symptom (Ousterhout's terminology, which the request specifically asked about): the interface's surface is too thin relative to what real consumers need, so the consumer reaches around it via a concrete-type check. Two concrete consequences: (1) if `CanvasViewportViewModel<T>` is ever constructed over `ImmutableSpatialIndexService<T>` or a test double, `LastSnapshotPublishedAtUtc` silently stays `null` forever with no error or warning — a silent capability gap, not a loud failure; (2) any future `ISpatialIndexService<T>` implementation that *does* have meaningful publish-timestamp semantics would need its own `if (x is ConcreteType)` branch added here, an ever-growing type-check chain instead of one interface member.

**Recommendation:** either (a) add `DateTimeOffset? LastPublishedAtUtc { get; }` directly to `ISpatialIndexService<T>` (nullable, so non-snapshotting implementations can return `null` honestly instead of the ViewModel silently guessing), or (b) introduce a narrow, optional interface (`ISpatialIndexSnapshotInfo { DateTimeOffset? LastPublishedAtUtc { get; } }`) that `LiveSpatialIndexService<T>` implements and the ViewModel checks against instead of the concrete class — preferable if not every implementation should be forced to carry this member. Either removes the concrete-type coupling.

**Confidence:** 95% (interface and consumer both read in full). **New ticket recommended**: `ICW-SPATIAL-INDEX-INTERFACE-SHAPE` (no existing ticket found covering this — searched `docs/tasks/` for `ISpatialIndexService`/`LiveSpatialIndexService` interface-shape concerns; only found tickets about the *immutability* of `Query`'s return value, `ICW-060`/`ICW-P0-SPATIAL-INDEX-SAFETY`, both already resolved — this is a distinct concern about interface completeness, not query-result mutability).

### 2.4 `ICW-188`, `ICW-189`, and most of `ICW-088` are substantially already implemented, but still tracked as "To Do" — a **correction**, not a duplicate

This is the most consequential finding in this report: it's a second, independent instance of the exact tracker-rot pattern flagged in my previous follow-up report (§1, "the tracker was not updated"), but this time in a completely different ticket family that I had not previously examined, discovered purely by reading `SampleImageGenerator.cs`/`GeneratorOptions.cs` against their own stated requirements.

**`ICW-188`** ("Introduce `GeneratorOptions` and `MipOptions` records... wire the primary `GenerateSet` and `GenerateMipPixels` paths to accept these option types") — status: **To Do**. Reality:
- `GeneratorOptions` record: **exists**, fully implemented (`GeneratorOptions.cs`), and is the real parameter carrier for `GenerateSet(GeneratorOptions)`.
- `MipOptions` record: **exists** (`MipOptions.cs`) but has **zero references anywhere in the solution** (confirmed by repo-wide grep — this is the same dead-code finding as my first report's E23, now given its correct ticket context: it isn't orphaned scaffolding with no purpose, it's **half of `ICW-188`, built but never wired in**). The methods `ICW-088` specifically named as needing this treatment — `GenerateMonochromeMipPixels`, `ApplyMipDetails`, `ApplyDetailsWithGdiPlus` — still take long, flat parameter lists today, confirmed by re-reading `SampleImageGenerator.cs` in this session.
- **Correction: status should be "In Progress," not "To Do."**

**`ICW-189`** ("single canonical public entry point `GenerateSet(GeneratorOptions)`... XML docs to mark old overloads as forwarded/deprecated... parity unit tests") — status: **To Do**. Reality:
- The canonical entry point and forwarding overload **exist** (`SampleImageGenerator.cs:71-127`) — the legacy 16-parameter overload has an inline comment ("Forwarding overload preserved for backward compatibility") and forwards to `GenerateSet(GeneratorOptions)`, exactly as specified.
- "XML docs to mark old overloads as forwarded/deprecated": **not done** — the comment is a plain `//` line, not an `[Obsolete]` attribute or XML `<summary>` deprecation note. No IDE tooling will ever surface this to a caller.
- "parity unit tests" comparing the two call forms: **not done** — repo-wide grep confirms **every single call site in the entire solution** (production `MainWindow.xaml.cs`, all of `SampleImageGeneratorTests.cs`, `ZeroCopyBitmapFactoryTests.cs`, and the new `TileMaterializationBenchmarks.Windows.cs`) uses the legacy positional-parameter overload — **not one caller anywhere constructs `GeneratorOptions` directly.** This means the "new" canonical API this ticket set out to establish is reachable only indirectly and is exercised by zero direct tests.
- **Correction: status should be "In Progress" (roughly half-done — the mechanical plumbing exists, the migration and deprecation signaling do not), not "To Do."**

**`ICW-088`** ("Reduce parameter count... consolidate into `GeneratorOptions`... extract `AnnotationGenerator`... extract `DefectTemplateFactory`") — status: **To Do**. Reality, checked against its own 6-step "Proposed PR work" list:
1. Add `GeneratorOptions`/`MipOptions` — done / half-done (see `ICW-188` above).
2. Adapter overload + forwarding — done (see `ICW-189` above).
3. Extract `GenerateAnnotations` into `AnnotationGenerator` — **done**, `AnnotationGenerator.cs` exists and is called from `SampleImageGenerator.cs:190` — **but the extraction left a dead duplicate behind**: `SampleImageGenerator.cs` still has a private, unreachable copy of the old `GenerateAnnotations` method at lines 574-622 (byte-for-byte the same logic, confirmed in my first report's E24, now correctly attributable to this ticket's incomplete cleanup rather than an unexplained orphan).
4. Extract `DefectTemplateFactory` — **done**, `DefectTemplateFactory.cs` exists and is wired in.
5. "Replace the long-parameter private helpers with option structs... remove redundant casts" — **not done**, confirmed by re-reading the file this session; this is the same gap as `MipOptions` never being wired in.
6. "Remove deprecated forwarding overloads in a follow-up PR" — not reached, correctly blocked on step 2's deprecation marking never having happened.
- **Correction: status should be "In Progress" (~60% complete: 2 of 3 extractions done and correct, the options-record consolidation half-done, the deprecation/parity-test/cleanup work not started), not "To Do."** Recommend splitting this ticket's remaining scope explicitly into: (a) wire `MipOptions` into the mip-generation methods it was built for, (b) delete the dead duplicate `GenerateAnnotations`, (c) add `[Obsolete]` + parity tests per `ICW-189`, (d) only then consider removing the legacy overload.

**Secondary observation:** neither `ICW-188` nor `ICW-189` appears in `docs/tasks/active-tasks.md` at all (only as standalone files under `docs/tasks/tickets/`) — a smaller-scale version of the same "two sources of truth, one goes stale" problem `ICW-081` was created to fix for duplicate IDs; here the failure mode is "exists in one tracker file but not the other" rather than "duplicated ID," but the remedy (a single canonical tracker, or automated consistency checking per `scripts/Validate-TaskTracker.ps1`) is the same one already proposed.

**Confidence:** 90% overall (every specific sub-claim above is grep/read-confirmed; the only soft part is "60%/half-done" being a qualitative estimate rather than a formal metric).

### 2.5 Background-noise tuning values are duplicated across four independent locations, and one has already drifted

**Finding:** the same eight noise/rendering tuning values (`TargetValue`, `Noise`, `CircleCount`, `NoiseScale`, `NoiseOctaves`, `NoiseLacunarity`, `NoiseGain`, `NoiseAmplitude`) have their defaults declared independently in **four** places:
1. `CanvasUserSettings` property initializers (`= 128`, `= 8`, `= 3`, `= 1`, `= 5`, `= 2.5`, `= 0.6`, `= 1`).
2. `TileBackgroundNoiseSettingsViewModel`'s `[ObservableProperty]` field initializers (matches #1 exactly: `128, 8, 3, 1, 5, 2.5, 0.6, 1`).
3. `SampleImageGenerator.GenerateSet`'s legacy-overload parameter defaults (`targetValue = 128, noise = 8, noiseScale = 1.0, noiseOctaves = 3, noiseLacunarity = 2.5, noiseGain = 0.6, noiseAmplitude = 1.0`, `circleCount = 3`).
4. `GeneratorOptions`'s own record-parameter defaults (identical set again).

**#3 and #4 have already drifted from #1 and #2**: `NoiseOctaves` defaults to **3** in the generator's two copies but **5** in the settings/ViewModel's two copies. Because every real call site (see §2.4) always passes `noiseOctaves` explicitly from `backgroundNoiseSettings.NoiseOctaves` (sourced from #1/#2's live values), this drift is **currently inert** — but it is exactly the kind of landmine that goes live the instant someone (a) constructs `GeneratorOptions` directly without specifying `NoiseOctaves`, finally completing the `ICW-189` migration described above, or (b) calls the legacy overload without that one named argument in a new code path (e.g., a test, a CLI tool, a script). Separately: `CanvasUserSettings.IsValid` never checks an upper bound tighter than `byte`'s natural 0–255 range for `BackgroundNoise`, even though every consumer (`MainViewModel.CreateBackgroundNoiseSnapshot`) treats its real valid range as **0–24** — a hand-edited settings file with `BackgroundNoise: 200` passes `IsValid` today and then gets silently clamped to 24 the first time a snapshot is taken, with no error and no record that the persisted and effective values now disagree.

**Recommendation:** this is the same underlying problem `ICW-P1-SETTINGS-VALIDATION` already exists to fix (unified validation), just with a wider blast radius than that ticket currently scopes (it's framed around `ObjectsPerTile`/`MinimumSparseTilePixelSize`). Recommend extending its scope to include: (a) a single canonical source of truth for these 8 noise-parameter bounds and defaults — e.g., named constants or a small `NoiseParameterRanges` type referenced by `CanvasUserSettings.IsValid`, `MainViewModel.CreateBackgroundNoiseSnapshot`'s clamp logic, and (once `ICW-189` completes) `GeneratorOptions`'s defaults — rather than four independent literal sets; (b) add the missing `BackgroundNoise <= 24` check to `IsValid`; (c) fix the `NoiseOctaves` default drift (decide whether 3 or 5 is actually correct and make both copies agree) before anyone completes the `GeneratorOptions` migration and inherits the wrong one.

**Confidence:** 90% (all four locations read directly this session and in the original audit; the drift is a simple side-by-side comparison, not an inference).

### 2.6 One more performance/allocation smell in the same family the council review already flagged, that its list didn't include

**Finding:** the council review's "Remaining Known Issues (Deferred)" table already lists several O(n)/allocation concerns in `TileWorkCoordinator` (`GetClaimantIds()` LINQ allocation, O(n) `RemoveFromQueue`, O(n) scan-ahead under lock). Worth adding to that same list: `DrainQueueWithLivenessCheck`'s scan-ahead branch allocates **two new `List<BackgroundTileCacheKey>`** (`deferred` and `remaining`) **on every single drain call that encounters a non-visible head-of-queue item**, even though `deferred` only ever holds exactly one element (the originally-dequeued key) — it's declared as a `List<T>` and iterated with `foreach` for what is always a 1-element collection. Under `ICW-144`'s own benchmark scenarios (`PublishInterestSet_MixedVisibility`, `FastScrollStress_ThreeCycles`), this allocation happens once per queue-drain iteration during exactly the fast-scroll conditions those benchmarks are designed to stress — worth having the benchmark's forthcoming allocation counters (per `ICW-132`) specifically watch this one, since it's cheap to fix (replace `deferred` with a single local variable) once it's visible in a profile.

**Confidence:** 85% (code read directly; not benchmarked/measured, so "worth watching" rather than "confirmed hot path").

---

## 3. Corrections Summary Table

| Ticket | Current status | Recommended correction | Basis |
|---|---|---|---|
| `ICW-188` | To Do | **In Progress** — `GeneratorOptions` done, `MipOptions` built-but-unwired | §2.4 |
| `ICW-189` | To Do | **In Progress** — entry point + forwarding done; deprecation marking and parity tests not started | §2.4 |
| `ICW-088` | To Do | **In Progress (~60%)** — 2 of 3 extractions done cleanly, 1 done with leftover dead code, options-consolidation half-done | §2.4 |
| `ICW-018` | (existing, resolution TBD per original report) | **Extend scope** to include the `SourceId = "synthetic"` magic-string consolidation | §2.2 |
| `ICW-P1-SETTINGS-VALIDATION` | Proposed | **Extend scope** beyond `ObjectsPerTile`/`MinimumSparseTilePixelSize` to cover the 4-location noise-parameter default duplication and the missing `BackgroundNoise` upper-bound check | §2.5 |
| `ICW-P0-ACTIVECOUNT-residuals` | Proposed | **Extend scope** to also document `CancelWorkItem`'s caller-must-hold-lock contract while its tail is already being restructured | §2.1 |
| *(new)* `ICW-P0-LOCK-CONTRACT-DOCS` | — | **New ticket recommended** | §2.1 |
| *(new)* `ICW-SPATIAL-INDEX-INTERFACE-SHAPE` | — | **New ticket recommended** | §2.3 |

---

## 4. Assumptions & Open Questions

- GitHub's REST API was rate-limited for this session (`api.github.com/repos/.../commits/main` returned a 403); the exact HEAD SHA was not independently re-confirmed the way the previous two reports did. The `codeload.github.com` tarball fetch of `main` is not subject to the same rate limit and reflects the true current tip, and its contents (new handoff docs referencing "Wave E," the exact diff shape matching the Wave E handoff's own description) corroborate that this is the intended state to review. Recommend re-confirming the exact SHA in a future session once API rate limits reset, for the record.
- §2.4's "~60% complete" figure for `ICW-088` is a qualitative estimate based on its own explicit 6-step checklist, not a formal completion metric.
- I did not re-verify every one of the council review's own "Remaining Known Issues" performance claims (`GetClaimantIds()` allocation, O(n) `RemoveFromQueue`, etc.) — they were read and appear consistent with the code, but weren't independently re-derived line-by-line since they're already correctly tracked and outside this report's "new findings only" scope.
- Open question carried into this report: given `ICW-P0-MIGRATION-GUARD` (added this session) proposes a feature-freeze-during-P0 policy, should `ICW-088`/`ICW-188`/`ICW-189`'s remaining work (a pure refactor, not a safety fix) be explicitly classified as "deferred" work under that policy, or does it qualify as safety-adjacent given it touches the same file family (`SampleImageGenerator.cs`) that `ICW-P1-COOPERATIVE-CANCEL`/`ICW-P1-GDI-CONCURRENCY` also need to edit? Recommend the migration-guard ticket's author make this classification explicit once it's adopted, to avoid two more agents editing `SampleImageGenerator.cs` concurrently for unrelated reasons.

---

*Methodology note: this report was produced by diffing the two most-recently-changed source files against the exact previous-session copies, then conducting fresh line-by-line review of previously-unread or only-lightly-read files (`MainViewModel.cs`, `CanvasViewportViewModel.cs`, `AboutDialog.cs`, `TileBackgroundNoiseSettingsView.xaml.cs`, `GeneratorOptions.cs`, `ISpatialIndexService.cs`, `ScreenPoint.cs`, `SpatialRecord.cs`), a repo-wide grep sweep for code-smell markers (`TODO`/`HACK`/`placeholder`/etc.) and for the `"synthetic"` magic-string pattern, and a targeted read of three previously-unexamined ticket files (`ICW-088`, `ICW-188`, `ICW-189`) cross-referenced against their own stated acceptance criteria. All findings above are grounded in this session's direct reads; nothing here is inferred from ticket text alone.*
