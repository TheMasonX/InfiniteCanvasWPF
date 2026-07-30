# InfiniteCanvasWPF — Deep-Dive Code Audit & ICW Implementation Guidance

**Repo:** `TheMasonX/InfiniteCanvasWPF` · **Commit audited:** `afa8b5b8244424c253177943ad766ceef3bb1819` (`main`, "External audits and more tasks")
**Method:** Full retrieval via `codeload.github.com` tarball at the pinned SHA (not GitHub UI/web_fetch, to avoid truncation). Every file under `src/`, `tests/`, `benchmarks/` (66 `.cs` files, 8,440 LOC) read in full. Tracker docs (`active-tasks.md`, `JIRA.md`, ADRs, external-audit synthesis docs) read for context but **not trusted at face value** — every load-bearing claim below was independently re-derived from source and given its own confidence score.
**Scope note:** `docs/tasks/tickets/` contains **~155 ticket files for ~100 unique IDs** with duplicate/conflicting IDs (e.g., `ICW-004`, `ICW-021`, `ICW-052`–`056`, `ICW-061`–`065`, `ICW-098`–`100` each have 2+ non-identical files). This is itself a tracked defect (`ICW-081`, `ICW-100-reconcile-duplicate-ticket-ids.md`). This report treats `docs/tasks/active-tasks.md` as the closest thing to ground truth and flags every place the tracker's status disagrees with what the code actually does.

---

## 1. Executive Summary

**Top-line finding:** the tracker is optimistic in two directions at once — it under-reports how broken the concurrency layer is (three independent external audits already agree on this) and it over-reports how broken some settled subsystems are (spatial indexing and Serilog startup hardening both already have fixes in the code that the ticket text doesn't acknowledge). Neither can be assumed from the ticket description alone; both required reading the code.

**Confirmed, still-open, high-severity defects** (see §3 for full plans):
1. **`TileWorkCoordinator._activeCount` is decremented at cancellation *request* time, not at worker exit** (`ICW-P0-ACTIVECOUNT`) — `DefaultMaxConcurrency = 4` is not a real ceiling during a cancel storm. **95% confidence**, root-caused to exact lines.
2. **Claimant cancellation tokens are hardcoded to `CancellationToken.None`** at both coordinator call sites (`ICW-P1-CLAIMANT-TOKENS`) — stale frame work cannot be canceled at all today; only orphaning (zero-claimant) cancellation works. **97% confidence.**
3. **`TileWorkCoordinator.ReleaseReservation` is a no-op counter**, not a real budget release (`ICW-P0-LEASE-RELEASE`) — cache byte-budget accounting silently drifts on every cancellation/failure. **98% confidence.**
4. **`SampleImageTile._pixelCost` is computed once from mip‑0 dimensions and never revised** (`ICW-P1-PIXELCOST-MIPS`) — `TileCacheBudget` undercounts real resident memory once mip levels accumulate. **95% confidence.**
5. **`PublishFrame` recycles the retiring front buffer as the next back buffer with no compositor synchronization** (`ICW-021` / `ICW-P0-BUFFER-REUSE-SYNC`) — confirmed exact mechanism at `MainWindow.xaml.cs:465-483`. **85% confidence** (mechanism confirmed; actual visible tearing not reproduced in this static review).
6. **`RenderRequestTracker` exists as a class but is wired into nothing** — zero references anywhere outside its own file (`ICW-100`). **99% confidence**, trivially grep-verified.
7. **`RegenerateSceneAsync` has no rollback on mid-flight failure** (`ICW-P0-TRANSACTIONAL-REGEN`) — confirmed no try/catch around the body beyond the outer `finally` that just re-enables UI. **85% confidence.**
8. **`OnClosed` disposes the tile coordinator and bitmap buffers without waiting for an in-flight `RegenerateSceneAsync`** (`ICW-029`) — the generation gate is never awaited at shutdown. **80% confidence.**
9. **`CanvasUserSettings.IsValid` never upper-bounds `ObjectsPerTile`**, while `SampleImageGenerator.GenerateSet` throws above 256 (`ICW-P1-SETTINGS-VALIDATION`) — a hand-edited settings file with `ObjectsPerTile: 500` loads successfully and crashes on first regenerate. **95% confidence.**
10. **`MinimumSparseTilePixelSize` is persisted and validated but never consumed** — and worse than the ticket states: `ZeroCopyBitmapFactory.DrawTile` already has a `minimumSparseTilePixelSize` **parameter that is entirely unused inside the method body**, i.e. the plumbing exists and is already dead, not merely missing (`ICW-099`). **95% confidence.**

**Findings that contradict the tracker's status (claims that do *not* hold up):**
- **`ICW-060` / `ICW-P0-SPATIAL-INDEX-SAFETY`** ("spatial index unstarted, mutable lists exposed from STRtree") — **false at HEAD.** `StrTreeSpatialIndexService.Query` already copies NTS's mutable `IList<T>` to an array with an explicit comment naming this exact concern. `LiveSpatialIndexService` already uses an immutable, lock-free CAS state machine. **80% confidence this ticket is stale and should be closed or rescoped**, not treated as unstarted Phase-0 work.
- **`ICW-099-serilog-eventlog-startup-fallback`** ("no guard around EventLog sink construction") — **false at HEAD.** `SerilogHost.CreateLogger()` already wraps the `WriteTo.EventLog(...)` call in try/catch with a file-only fallback. **75% confidence this ticket is stale** (residual 25% reflects that I did not trace whether `Serilog.Sinks.EventLog`'s admin-rights failure can occur outside this try block, e.g. lazily on first write).
- **`ICW-P0-STALE-PUB`** ("tile-level stale-publication guard needs verification, currently proposed") — **partially true, partially already done.** `SampleImageTile.OnCoordinatorPixelsGenerated`/`OnCoordinatorMipGenerated` already compare `key.ContentRevision` against the live `_generationEpoch` and discard stale results — the *tile-level* guard exists today. What's genuinely missing is sharing this epoch with a *frame-level* tracker (`ICW-100`), which is the part the ticket's "Depends on" note correctly identifies. **85% confidence** the ticket description overstates how much is missing.

**New defects found that have no corresponding ticket:**
- `MipOptions` record (`src/InfiniteCanvas.Rendering/MipOptions.cs`) is **100% dead code** — zero references anywhere in the solution. **99% confidence.**
- `SampleImageGenerator` has a **private, unreachable duplicate** of `AnnotationGenerator.GenerateAnnotations` (lines 574–622) — the real call site at line 190 calls the public `AnnotationGenerator.GenerateAnnotations` instead, leaving ~50 lines of dead, byte-for-byte-duplicated logic in `SampleImageGenerator.cs`. **95% confidence.**
- `TileGridIndexLookup.TryGetTileIndex`'s `(row * columns) + column` is unchecked, inconsistent with the `checked{}` convention used everywhere else in the same file family (already partially captured under `ICW-023`'s expanded scope, but worth calling out explicitly). **90% confidence.**

**Recommended execution order** (unchanged in spirit from `ICW-P0-SEQUENCING`, refined by evidence above):
1. Phase 0 safety: `ICW-P0-ACTIVECOUNT` → `ICW-P0-LEASE-RELEASE`/`ICW-P1-PIXELCOST-MIPS` (do together, same class) → `ICW-100` (RenderRequestTracker re-apply) → `ICW-P0-TRANSACTIONAL-REGEN` → `ICW-029`.
2. Phase 1 correctness: `ICW-P1-CLAIMANT-TOKENS` → `ICW-P1-COOPERATIVE-CANCEL` → `ICW-P1-GDI-CONCURRENCY` → `ICW-P1-SETTINGS-VALIDATION` (+ `ICW-099`).
3. Cleanup, cheap and independent of the above: `ICW-101` (tooltip presenter restore), close/rescope `ICW-060`/`ICW-P0-SPATIAL-INDEX-SAFETY` and `ICW-099-serilog`, delete `MipOptions`/dead `GenerateAnnotations`, `ICW-018` disposition.
4. Tracker hygiene (`ICW-081`/duplicate `ICW-100`/`ICW-084`) should happen in parallel, not after — every week it's deferred adds more duplicate IDs.
5. Only after 1–2 land: `ICW-143` viewport culling, as the tracker itself already states.

---

## 2. Evidence Ledger

Every claim below cites the exact file/line read in this session. Confidence rubric: **95–99%** = exact line quoted, unambiguous; **80–94%** = mechanism confirmed by reading the code path, minor interpretive gap; **60–79%** = plausible from partial evidence, not fully traced end-to-end; **<60%** = inference from ticket/tracker text only, not independently verified this session.

| # | Claim | File:Line | Verdict | Confidence |
|---|---|---|---|---|
| E1 | `_activeCount` decremented in `CancelWorkItem` before worker thread observes cancellation | `TileWorkCoordinator.cs:373-385` | Confirmed | 95% |
| E2 | `DrainQueue` has no claimant-liveness check before restarting queued items | `TileWorkCoordinator.cs:399-412` | Confirmed as designed-but-shallow (not a "bug" in the strict sense; see §3.2) | 75% |
| E3 | Claimant token hardcoded `CancellationToken.None` (native) | `SampleImageTile.cs:428` | Confirmed | 97% |
| E4 | Claimant token hardcoded `CancellationToken.None` (mip) | `SampleImageTile.cs:553` | Confirmed | 97% |
| E5 | `ReleaseReservation` is `Interlocked.Increment(ref _reservationReleases)` only | `TileWorkCoordinator.cs:431-434` | Confirmed | 98% |
| E6 | `_pixelCost = checked(pixelWidth * pixelHeight)` set once in constructor, never revised | `SampleImageTile.cs:16,67,105` | Confirmed | 95% |
| E7 | `TileCacheBudget.TryReserve`/`Release` key off `tile.PixelCost` (mip-0 only) | `SampleImageTile.cs:789,838` | Confirmed | 95% |
| E8 | `PublishFrame` reassigns `previousFront` → `_backBitmapFactory` with no fence/wait | `MainWindow.xaml.cs:465-483` | Confirmed mechanism; compositor-level tearing not reproduced | 85% |
| E9 | `RenderRequestTracker` has zero call sites outside its own definition | repo-wide grep | Confirmed | 99% |
| E10 | `RegenerateSceneAsync` has no try/catch for partial-failure rollback | `MainWindow.xaml.cs:163-244` | Confirmed | 85% |
| E11 | `OnClosed` disposes coordinator/buffers without awaiting `_generationGate` | `MainWindow.xaml.cs:1413-1429` | Confirmed | 80% |
| E12 | `CanvasUserSettings.IsValid` has no upper bound on `ObjectsPerTile` | `CanvasUserSettings.cs:60` | Confirmed | 95% |
| E13 | `SampleImageGenerator.GenerateSet` throws for `ObjectsPerTile > 256` | `SampleImageGenerator.cs:94-97,146-149` | Confirmed (this is the mismatch partner to E12) | 95% |
| E14 | `MinimumSparseTilePixelSize` validated/persisted but never referenced outside `CanvasUserSettings.cs`/`MainViewModel` wiring | repo-wide grep | Confirmed | 95% |
| E15 | `ZeroCopyBitmapFactory.DrawTile`'s `minimumSparseTilePixelSize` parameter is unused in the method body | `ZeroCopyBitmapFactory.Windows.cs:175-228` | Confirmed | 95% |
| E16 | `StrTreeSpatialIndexService.Query` already copies to array, comment explicitly names the mutable-list concern | `StrTreeSpatialIndexService.cs:31-37` | Confirmed — contradicts `ICW-060` | 90% |
| E17 | `LiveSpatialIndexService` already uses immutable `ImmutableArray<T>` + CAS state, no mutable list exposure | `LiveSpatialIndexService.cs` (full file) | Confirmed | 90% |
| E18 | `SerilogHost.CreateLogger()` already wraps `WriteTo.EventLog(...)` in try/catch with file-only fallback | `SerilogHost.cs:34-41` | Confirmed — contradicts `ICW-099-serilog` premise | 75% |
| E19 | `OnCoordinatorPixelsGenerated`/`OnCoordinatorMipGenerated` already discard stale (epoch-mismatched) results | `SampleImageTile.cs:486-520,618-644` | Confirmed | 90% |
| E20 | `IRenderer<TScene,TOutput>` and `ViewportRenderRequest` have zero references anywhere (prod or tests) | repo-wide grep | Confirmed | 99% |
| E21 | `IBackgroundTileSource` has zero implementers/references | repo-wide grep | Confirmed | 99% |
| E22 | `BackgroundTileDescriptor`/`Request`/`Payload` are exercised **only by tests**, not by production code | repo-wide grep | Confirmed (nuance the ticket doesn't state) | 90% |
| E23 | `MipOptions` record has zero references anywhere | repo-wide grep | Confirmed, no existing ticket names this | 99% |
| E24 | `SampleImageGenerator` has a private, unreachable duplicate of `AnnotationGenerator.GenerateAnnotations` | `SampleImageGenerator.cs:574-622` vs `:190` | Confirmed, no existing ticket names this | 95% |
| E25 | `CreateAnnotationToolTip` uses raw `Features["Confidence"]`/`["Severity"]` indexers (no `TryGetValue`) despite a safe `AnnotationFeaturePresenter.BuildTooltipContent` existing and going unused for this call site | `MainWindow.xaml.cs:724-732` vs `AnnotationFeaturePresenter.cs:17-29` | Confirmed | 95% |
| E26 | `SpatialBounds.Intersects` uses closed-interval (`<=`/`>=`) semantics while pixel/tile lookups elsewhere use half-open `[X, Right)` | `SpatialBounds.cs:45-51` vs `SampleImageTile.cs:384`, `TileGridIndexLookup.cs:27-33` | Confirmed mismatch exists; behavioral impact (edge-pixel annotation bias) not independently reproduced | 75% |
| E27 | `TileGridIndexLookup`'s `(row*columns)+column` is not `checked` | `TileGridIndexLookup.cs:47` | Confirmed | 90% |
| E28 | 21 `async void` handlers exist in `MainWindow.xaml.cs`, none with local try/catch beyond the global dispatcher handler | repo-wide grep + spot read | Confirmed count; "none has try" spot-checked on ~8 of 21, not all 21 individually | 80% |
| E29 | `SampleImageGenerator`/`AnnotationGenerator` generation paths have no `CancellationToken` parameter or check anywhere in the hot loop | `SampleImageGenerator.cs` (full file) | Confirmed | 90% |
| E30 | `ApplyDetailsWithGdiPlus` constructs `System.Drawing.Bitmap`/`Graphics` inside coordinator worker tasks with no explicit serialization | `SampleImageGenerator.cs:333-396`, `TileWorkCoordinator.cs:294-346` | Confirmed code shape; GDI+ thread-safety failure mode itself not reproduced (would require a live repro) | 65% |
| E31 | No existing test exercises actual concurrent-factory-body overlap during a cancellation storm (only quiescent-state `ActiveCount` assertions) | `tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs` (grep) | Confirmed | 85% |
| E32 | Existing `docs/tasks/tickets/` has ≥17 duplicated ID pairs across non-identical files | tarball file listing | Confirmed by listing; not all pairs diffed for content | 90% |

---

## 3. Implementation Plans — Priority Tier

### 3.1 `ICW-P0-ACTIVECOUNT` — Fix concurrency ceiling violation
**Confidence root cause is real: 95%.**

**Mechanism:** `StartWorkItem` increments `_activeCount` and calls `item.SetRunning()` (flips `_running` 0→1). On cancel of a running item, `CancelWorkItem` calls `item.SetRunning()` **again** — it's already 1, so the exchange returns 1 (not 0), making `!item.SetRunning()` evaluate `true` → `wasRunning = true` → **`_activeCount` is decremented immediately**, before the `Task.Run` delegate's `await item.Factory(item.WorkToken)` has actually unwound. If the factory doesn't observe `WorkToken` promptly (it currently never does — see 3.5), the real OS thread keeps running GDI+ work while the coordinator believes a concurrency slot is free and lets `DrainQueue` start a new item. Under a fast-scroll cancellation burst this can pile up far beyond `DefaultMaxConcurrency = 4`.

**Fix:**
1. Move the `_activeCount` decrement for the "was running" branch out of `CancelWorkItem` and into the worker's `finally` (the `Task.Run` lambda in `StartWorkItem`) — the single place that actually knows the factory has returned/thrown/been canceled.
2. `CancelWorkItem`'s job for a running item becomes: mark `State = Canceled`, call `item.CancelWork()` to signal the token, and **not** touch `_activeCount`. Let the `Task.Run` completion path (already has three `catch` branches) do the accounting exactly once via `HandleWorkStopped`.
3. Guard against the existing double-decrement risk: `HandleWorkStopped` already early-returns when `item.State == TileWorkItemState.Canceled` (line 353) — after removing the eager decrement from `CancelWorkItem`, this early return needs to instead **still decrement** (it's now the only place that does), so drop the `|| item.State == TileWorkItemState.Canceled` short-circuit on the `_activeCount` line specifically while keeping it for the counters (`_canceledCount` etc.) to avoid double-counting those.
4. Add a stress test: submit N > maxConcurrency items, cancel the first `maxConcurrency` immediately without observing the token in the factory (simulate the "doesn't observe cancellation promptly" case with a `ManualResetEventSlim` gate), assert peak concurrently-executing factory bodies (instrument via `Interlocked.Increment`/`Decrement` around the factory itself in the test) never exceeds `maxConcurrency`.

**Assumptions:** the intent of "active" is "currently executing factory body," not "currently claimed." **Open question:** should `CancelWorkItem` still eagerly free a *queue* slot logically (so `DrainQueue` can start a *replacement* item) even though the physical thread is still finishing? If yes, that requires a distinct "reserved-but-not-yet-freed" counter, which is closer to what `ICW-P0-QUEUE-DRAIN`'s claimant-liveness check is reaching for — recommend implementing both in the same PR since they're causally linked.

---

### 3.2 `ICW-P0-QUEUE-DRAIN` — Claimant-liveness check in `DrainQueue`
**Confidence real gap exists: 75%** (lower than E1 because the current claimant-removal path already proactively calls `RemoveFromQueue` when the last claimant leaves — see `RemoveClaimant`/`RemoveAllClaimants` → `CancelWorkItem` → `RemoveFromQueue`. The residual risk is a **race window**, not an always-reproducible bug: a claimant can be removed concurrently with `DrainQueue` dequeuing the same key between the `_queue.Dequeue()` and the `_items.TryGetValue` check at lines 405-408, both under `_lock`, so actually the window looks closed for the *removal* path — but there is no check that a *newly dequeued* item's `ClaimantCount > 0` before starting it, which matters once `ICW-P1-CLAIMANT-TOKENS` introduces per-frame tokens that fire without going through `RemoveAllClaimants` explicitly.

**Recommendation:** treat this ticket's own Phase 0/Phase 1 split as correct — implement the skeleton now:
```csharp
private void DrainQueue()
{
    lock (_lock)
    {
        while (_activeCount < _maxConcurrency && _queue.Count > 0)
        {
            var key = _queue.Dequeue();
            if (_items.TryGetValue(key, out var item)
                && item.State == TileWorkItemState.Queued
                && item.ClaimantCount > 0) // Phase 0: skeleton check, always true today
            {
                StartWorkItem(item);
            }
        }
    }
}
```
Phase 1 (after `ICW-P1-CLAIMANT-TOKENS` lands) becomes meaningful once tokens can fire independently of explicit `RemoveClaimant` calls. Add a regression test asserting a work item whose only claimant's token fires *while queued* never transitions to `Running`.

---

### 3.3 `ICW-P0-LEASE-RELEASE` + `ICW-P1-PIXELCOST-MIPS` + `ICW-134` — Cache accounting correctness
**Confidence: 95–98%, do as one PR — they are three symptoms of the same underlying design gap (no real lease object).**

**Current state:**
- `TileWorkCoordinator.ReleaseReservation` (line 431-434) is `Interlocked.Increment(ref _reservationReleases)` — a diagnostic counter with **no connection** to `TileCacheBudget.UsedBytes`.
- `TileCacheBudget.Release(tile)` (line 827-840) *does* correctly decrement `_usedBytes` by `tile.PixelCost` — but this is only ever called from `OnTilePixelsGenerationFailed` (`MainWindow.xaml.cs:282-287`), i.e. only on the UI-thread failure event, not from the coordinator's own cancellation/rejection paths. Cancellations that never surface a UI event (e.g., orphaned claimant on tile eviction) leak budget bytes forever, compounding with the mip-undercounting bug below.
- `SampleImageTile.PixelCost` is fixed at construction to `pixelWidth * pixelHeight` (native, mip‑0) and is what `TryReserve`/`Release` charge — so once a tile also holds 2–3 resident mip payloads (each smaller but nonzero), the budget is undercounting actual heap usage by up to ~33%+ once several mips accumulate, exactly as `ICW-P1-PIXELCOST-MIPS` states.

**Fix, in order:**
1. Introduce `ICacheReservation : IDisposable` in `TileWorkCoordinator`/`TileCacheBudget`, returned from a successful `tryReserve` closure, with `Dispose()` performing the actual `TileCacheBudget.Release`-equivalent exactly once (guard with an `Interlocked.CompareExchange` disposed-flag to make double-dispose a no-op, and add a leak-detection test that asserts `UsedBytes` returns to baseline after N reserve/cancel cycles).
2. Change `TileCacheBudget`'s unit of accounting from `tile.PixelCost` to `ResourceKey`-scoped accounting (source+tile+mip), i.e. reserve/release per resident payload rather than per tile. This directly serves `ICW-134`'s "variant-aware" requirement — don't build it twice.
3. Replace `SampleImageTile.PixelCost` (single int) with a method/property that sums all currently-resident mip payload byte counts (`_pixels?.Length ?? 0` plus `_mipPixels.Values.Sum(p => p.Length)`), read under `_cacheGate`.
4. Route every `TileWorkCoordinator` cancellation/failure/rejection path (`CancelWorkItem`, `HandleWorkStopped`, rejected admission in `Request`) through the new lease's `Dispose()` instead of the counter-only `ReleaseReservation`.
5. Regression tests: `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` (per ticket's own naming), plus a leak-detection test that runs many reserve→cancel cycles and asserts `UsedBytes == 0` at the end.

**Open question:** should eviction of a tile with multiple resident mips release all of them atomically, or per-mip? Recommend atomic (whole-tile) release to avoid partial-state windows, since `ResetImageCache` already clears `_mipPixels` wholesale.

---

### 3.4 `ICW-100` — Re-apply `RenderRequestTracker` wiring
**Confidence the wiring is absent: 99%. Confidence this is the right fix shape: 85%.**

`RenderRequestTracker` (`RenderRequestTracker.cs`) is a complete, correct, 23-line epoch counter (`BeginRequest`/`IsCurrent`/`Advance`, all `Interlocked`/`Volatile`-safe) with a dedicated test file (`RenderRequestTrackerTests.cs`) — but it is instantiated and called **nowhere** in `MainWindow.xaml.cs`. The class was evidently wired once (per the tracker's own note about commit `9247bff` reverting it) and the revert was never re-applied.

**Fix:**
1. Add a `RenderRequestTracker _renderRequestTracker` field to `MainWindow`.
2. At the top of `RenderFrameAsync`, call `var requestVersion = _renderRequestTracker.BeginRequest();` before the `await Task.Run(...)` that does the actual spatial query + bitmap generation.
3. After the `await`, before calling `PublishFrame`, guard: `if (!_renderRequestTracker.IsCurrent(requestVersion)) return;` — discard a frame that's no longer the latest requested (superseded by a newer pan/zoom/regenerate).
4. Call `_renderRequestTracker.Advance()` from `RegenerateSceneAsync` (a full scene swap should always invalidate any in-flight frame render) in addition to natural advancement via new `BeginRequest()` calls.
5. Add a **regression test that would have caught the silent revert**: not just "IsCurrent works" (already covered) but an integration-style test in `MainWindow`'s test surface (or a thin wrapper) asserting that two overlapping `RenderFrameAsync` calls result in exactly one `PublishFrame` call for the second (superseding) request. Without this, the class can be silently unwired a third time.
6. Wire the same epoch value into `ICW-P0-STALE-PUB`'s tile-level check (§Evidence E19) so both guards share one counter, per the ticket's own note — pass `requestVersion` (or a shared `SceneGeneration` id) into `SampleImageTile`'s claimant registration so tile-level and frame-level staleness agree.

---

### 3.5 `ICW-P1-CLAIMANT-TOKENS` — Wire real cancellation tokens
**Confidence: 97% root cause confirmed, 80% on recommended fix shape.**

Both coordinator call sites (`SampleImageTile.cs:428` for native, `:553` for mip) pass `CancellationToken.None` as `claimantToken`. This means `TileWorkItem.AddClaimant`'s `claimantToken.Register(...)` never fires (line 504: `if (claimantToken.CanBeCanceled)` — `CancellationToken.None.CanBeCanceled` is `false`), so the *only* way a work item is ever canceled today is via explicit `RemoveClaimant`/`RemoveAllClaimants`/`CancelAll()` calls (e.g., scene regeneration). A stale claimant from a superseded viewport frame is never automatically dropped by token expiry — it just sits there as a claimant forever (or until `ResetImageCache` explicitly calls `RemoveClaimant` for the old-revision key, which only happens on cache eviction, not on ordinary frame supersession).

**Fix:**
1. Introduce a per-frame (or per-viewport-generation) `CancellationTokenSource` in `MainWindow`, refreshed each time `RenderFrameAsync` begins a new frame (tie its lifetime to the same `RenderRequestTracker` epoch from §3.4 for consistency).
2. Thread that token through `SampleImageTile.EnsurePixelsGenerationStarted`/`EnsureMipPixelsGenerationStarted` in place of `CancellationToken.None`. This requires adding a `CancellationToken` parameter (or a `Func<CancellationToken>` provider mirroring the existing `ClaimantIdProvider` pattern) to `TryGetPixelsNonBlocking`'s call chain, since that's ultimately where generation is triggered from `ZeroCopyBitmapFactory.DrawTile`.
3. Use **token-source replacement**, not `RemoveAllClaimants` — the ticket is explicit that removing all claimants each frame caused cancel-thrashing when `ICW-142` was implemented (worth taking this stated prior failure at face value; re-deriving it would require reproducing the thrashing, which this static review can't do — **60% confidence** on this specific historical claim, flagged as unverified).
4. Add an integration test: start a tile generation under frame-1's token, advance to frame-2 (cancel frame-1's token source), assert the coordinator's `CanceledCount` increments and the tile's `_generationQueued` flag resets so a future request can retry.

---

### 3.6 `ICW-P1-COOPERATIVE-CANCEL` + `ICW-P1-GDI-CONCURRENCY`
**Confidence gap exists: 90% (no cancellation checks found anywhere in generation code). Confidence GDI+ concurrency is a live risk: 65%** (mechanism is real — `ApplyDetailsWithGdiPlus` constructs `System.Drawing.Bitmap`/`Graphics.FromImage` per call, invoked concurrently from up to `maxConcurrency` `Task.Run` workers, and .NET's GDI+ wrapper has known historical thread-affinity/token issues — but I did not reproduce a crash or corrupted-output failure in this review; treat as a real risk worth mitigating, not a proven live bug).

**Fix for cooperative cancellation:**
- Add `token.ThrowIfCancellationRequested()` at the start of `GenerateMonochromeMipPixels`, before/after `GenerateNoisePixelsCore`, and before/after `ApplyMipDetails`/`ApplyDetailsWithGdiPlus` — the factory delegates in `SampleImageTile.cs:420-431` and `:546-551` already receive a `token` parameter that is currently only used for the mip path's post-hoc `token.ThrowIfCancellationRequested()` (line 549) and **not at all** on the native path (line 420-425 doesn't touch `token`). Fix the asymmetry first (cheap, isolated), then add the finer-grained checks inside the generator itself once the token is real (depends on §3.5 landing first, since today's `CancellationToken.None` makes any checks here inert).

**Fix for GDI+ concurrency:**
- Cheapest mitigation: serialize `ApplyDetailsWithGdiPlus` calls behind a dedicated `SemaphoreSlim(1,1)` (accept the throughput hit; circle rasterization is a small fraction of total generation time per the `ICW-097`/`ICW-131` profiling notes already in the tracker) rather than building a dedicated worker-thread queue, which is a larger structural change for uncertain marginal benefit given `DefaultMaxConcurrency` is only 4.
- Add a stress test that runs `maxConcurrency` concurrent `ApplyDetailsWithGdiPlus` calls in a tight loop under a debug build with GDI+ debug assertions enabled (or simply run it under high concurrency in CI many times) — this is the only way to get real evidence of whether the current unbounded-during-cancel-storm concurrency (§3.1) has ever caused a real GDI+ fault; I could not determine this from static review alone. **Open question, not resolved by this audit.**

---

### 3.7 `ICW-021` / `ICW-P0-BUFFER-REUSE-SYNC` — InteropBitmap compositor handoff
**Confidence mechanism confirmed: 85%. Confidence this causes visible tearing in practice: unknown — not reproducible via static review.**

`PublishFrame` (`MainWindow.xaml.cs:465-483`): the just-retired `_frontBitmapFactory` becomes the *next* `_backBitmapFactory` immediately if dimensions match, with **no wait** for WPF's composition thread to finish consuming the `InteropBitmap` that still points at that same native memory section. The next `RenderFrameAsync` call can start writing new pixels into that shared file-mapping view (`NativeMemory.Clear` + `DrawTile`/`DrawDefectPatch` writes directly into `_view`) while the compositor may still be reading it for the previous frame's `Image.Source`.

**Fix options (pick one, both are legitimate):**
- **Option A — triple buffering:** keep 3 `ZeroCopyBitmapFactory` instances in rotation instead of 2, giving the compositor a full frame of slack before a buffer is reused. Simple, small memory cost (one extra native section at viewport resolution, bounded by the existing 4096×4096 clamp).
- **Option B — explicit fence:** after `FramePresenter.Child = frameVisual`, use `CompositionTarget.Rendering` (or `Dispatcher.Invoke` at `DispatcherPriority.Render`) to defer marking the old front buffer reusable until *after* the next composed frame has been presented — more precise, more code.

Recommend **Option A** first (lower risk, directly addresses the race without depending on WPF composition internals), with a regression test asserting no two live `Image.Source` references ever point at the same `ZeroCopyBitmapFactory` simultaneously (achievable by instrumenting a reference-count wrapper in test builds). Mark `ADR-0004`'s acceptance criteria contingent on this landing, as the tracker already proposes.

---

### 3.8 `ICW-P0-TRANSACTIONAL-REGEN`
**Confidence gap is real: 85%.**

`RegenerateSceneAsync` (`MainWindow.xaml.cs:163-244`) mutates `_spatialIndex`, `_camera`, `_tileCacheBudget`, `_tiles`, `_annotations`, and calls `_tileCoordinator.CancelAll()` — all before the only `try`/`finally` in the method, which merely re-enables the UI and releases the semaphore on any exit path. If `GenerateSet` throws (e.g., `ObjectsPerTile` out of range from a corrupted settings file — see §3.10), or the `_lifetime.Token` fires mid-await, the method exits with `_tiles` unassigned relative to `_spatialIndex`/`_sceneBounds`, or with an already-cleared spatial index and no annotations published.

**Fix:**
1. Snapshot the previous scene's key fields (`_tiles`, `_annotations`, `_sceneBounds`, and construct a way to re-attach the old `_spatialIndex` — note `InitializeSpatialState()` currently creates a *new* index eagerly at the top of the method, which is itself part of the problem) before mutating anything.
2. Wrap the generation + publish steps in `try`/`catch`; on any exception other than a clean shutdown-driven `OperationCanceledException`, restore the snapshot and surface a status message (`StatusText.Text = "Regeneration failed: ..."`) instead of leaving the UI in a half-initialized state.
3. This depends on `ICW-102`'s `DisposeDefectTemplatePools` fencing (see §3.11) being correct first, since the rollback path also needs the old defect template pool to not have been disposed prematurely.
4. Add an integration test: inject a `GenerateSet` that throws on the second call, call `RegenerateSceneAsync` twice, assert the second call leaves `_tiles`/`_annotations` at the *first* call's values (not empty, not null, not partially applied).

---

### 3.9 `ICW-029` — Shutdown lifecycle race
**Confidence: 80%.**

`OnClosed` (`MainWindow.xaml.cs:1413-1429`) cancels `_lifetime`, awaits `_renderAction.DisposeAsync()` (which does correctly drain in-flight render work per `CoalescingAsyncAction.DisposeAsync`), then immediately disposes `_frontBitmapFactory`/`_backBitmapFactory`/`_tileCoordinator` — **without ever acquiring or waiting on `_generationGate`**. If a user closes the window while `RegenerateSceneAsync` is mid-flight (holding the gate, inside `Task.Run(() => SampleImageGenerator.GenerateSet(...))` or awaiting `_spatialIndex.PublishSnapshotAsync`), that background task can throw `ObjectDisposedException` against the just-disposed coordinator, or race against buffer disposal.

**Fix:**
1. In `OnClosed`, before disposing shared resources, do `await _generationGate.WaitAsync();` (with a short timeout guard, since `_lifetime.Cancel()` should make any well-behaved in-flight generation observe cancellation and release the gate promptly — but only once `ICW-P0-TRANSACTIONAL-REGEN`'s exception handling is in place; today an unhandled exception path could leave the gate held).
2. Order matters: cancel `_lifetime` first (already done), *then* wait for the gate, *then* dispose the coordinator/buffers — this sequencing is what prevents the coordinator from being disposed while `GenerateSet` is still running under it.
3. Add a close-stress test (per the ticket's existing "Next Step") that triggers `RegenerateSceneAsync` and immediately calls `OnClosed`, repeated N times, asserting no unhandled exception is logged.

---

### 3.10 `ICW-P1-SETTINGS-VALIDATION` (covers `ICW-099`)
**Confidence: 95% on both sub-defects.**

Two independently-confirmed instances of the same pattern:
1. `CanvasUserSettings.IsValid` (line 55-71) checks `ObjectsPerTile >= 0` only — no upper bound — while `SampleImageGenerator.GenerateSet`/`GenerateSet(GeneratorOptions)` both throw `ArgumentOutOfRangeException` above `MaxObjectsPerTile = 256`. A hand-edited or corrupted `settings.json` with `ObjectsPerTile: 500` round-trips through `CanvasUserSettingsStore.Load` successfully (passes `IsValid`) and crashes the very first `RegenerateSceneAsync` call at startup — a startup crash loop, since the bad settings file persists.
2. `MinimumSparseTilePixelSize` is validated (line 71: `>= 0 and <= 4096`) and persisted, but grep confirms it is referenced **only** inside `CanvasUserSettings.cs` itself — never read by `MainWindow`, `ZeroCopyBitmapFactory`, or anywhere in the render path. Worse: `ZeroCopyBitmapFactory.DrawTile` already has a same-named parameter (`minimumSparseTilePixelSize`, default `0`) that is **passed through from `GenerateFrozenBitmap` but never read inside `DrawTile`'s body** — meaning even if `MainWindow` is fixed to pass the setting through, `DrawTile` itself needs a real implementation (presumably: skip/placeholder-render tiles whose projected screen size is below the threshold), not just plumbing.

**Fix:**
1. Add `&& ObjectsPerTile <= SampleImageGenerator.MaxObjectsPerTile` to `CanvasUserSettings.IsValid`. Note this creates a dependency from `InfiniteCanvas.Core` → `InfiniteCanvas.Rendering` that doesn't currently exist (check project references); if that's architecturally undesirable, duplicate the constant as `CanvasUserSettings.MaxObjectsPerTile = 256` with a comment cross-referencing `SampleImageGenerator.MaxObjectsPerTile` and a test asserting they stay equal.
2. Implement the actual skip-below-threshold logic inside `DrawTile` using the existing (currently-ignored) parameter — compare the tile's projected screen-space size (already computed similarly in `SampleImageTile.ShouldGenerateForPixelSize`, which is the right logic to reuse/share) against `minimumSparseTilePixelSize` and render only the placeholder value when below it, skipping the (currently unconditional) generation trigger.
3. Wire `MainWindow.RenderFrameAsync`'s call to `GenerateFrozenBitmap` (line 357-363) to pass `_mainViewModel`'s persisted `MinimumSparseTilePixelSize` (needs a UI control too, per the ticket, or can ship headless first with just the persisted value).
4. Add a single shared validation-function pattern per the ticket's broader ask: e.g. `CanvasUserSettings.ValidateObjectsPerTile(int)` called from both `IsValid` and `TryReadGenerationOptions` (`MainWindow.xaml.cs:1370-1411`, which already independently re-implements the same `0..MaxObjectsPerTile` check — a third copy of this validation logic that would benefit from consolidation).
5. Regression test: round-trip a settings file with `ObjectsPerTile = 500`, assert `IsValid == false` and `Load` falls back to defaults instead of crashing on first generate.

---

### 3.11 `ICW-102` — Defect template pool disposal fencing
**Confidence gap exists: 80%.**

`CoalescingAsyncAction` (full file read, §Evidence) has no non-destructive "wait for idle" — only `DisposeAsync`, which sets `_disposed = true` permanently. `RegenerateSceneAsync` calls `SampleImageTile.DisposeDefectTemplatePools(_tiles)` (line 185) right after `_tileCoordinator.CancelAll()`, with no guarantee the in-flight `RenderFrameAsync`'s background `Task.Run` (which reads `annotation.DefectBitmap` inside `DrawDefectPatch`) has actually finished — `CancelAll()` cancels *coordinator* work, not the render pipeline's own `Task.Run` in `RenderFrameAsync` line 352.

**Fix:** add a non-destructive `WaitForIdleAsync()` to `CoalescingAsyncAction` that captures and awaits `_processingTask` the same way `DisposeAsync` does, but without setting `_disposed`:
```csharp
public async Task WaitForIdleAsync()
{
    Task? processingTask;
    lock (_gate) { processingTask = _processingTask; }
    if (processingTask is not null)
    {
        try { await processingTask.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
}
```
Call `await _renderAction.WaitForIdleAsync();` in `RegenerateSceneAsync` before `SampleImageTile.DisposeDefectTemplatePools(_tiles)`. Add a concurrency test that starts a render, immediately triggers regenerate, and asserts no `ObjectDisposedException`/`AccessViolationException`-class failure occurs (the managed `Bitmap.Dispose()` will throw `ArgumentException`/`InvalidOperationException` on use-after-dispose, which is the realistic failure signature to assert against in a test).

---

### 3.12 `ICW-101` — Restore tooltip presenter usage
**Confidence: 95%. This is the cheapest fix in this report — do it immediately, independent of everything else.**

`AnnotationFeaturePresenter.BuildTooltipContent` (lines 17-29) already exists, already uses `TryGetValue` (safe), and is **already correct**. `MainWindow.CreateAnnotationToolTip` (line 724-732) does not call it — it duplicates similar formatting using raw indexers (`annotation.Features["Confidence"]`), which throws `KeyNotFoundException` if either key is ever absent (currently always present because `AnnotationGenerator`/`SampleImageGenerator`'s duplicate always populate both — see E24 — but this is an accident of the current data path, not a contract guarantee, since `Features` is typed as `IReadOnlyDictionary<string,double>` with no schema enforcement).

**Fix:** replace lines 724-732 with a call to `AnnotationFeaturePresenter.BuildTooltipContent(annotation)`, delete the now-redundant local formatting. Add a behavioral test asserting `CreateAnnotationToolTip`'s output matches `BuildTooltipContent`'s output for a given annotation (or better: delete `CreateAnnotationToolTip` entirely and call the presenter method directly at the one call site, `MainWindow.xaml.cs:552`).

---

### 3.13 `ICW-018` — Dormant rendering abstractions
**Confidence on facts: 99%. Confidence on recommended disposition: subjective, stated as such.**

Precise inventory (correcting the ticket's count):
- `IRenderer<TScene,TOutput>` and `ViewportRenderRequest`: **zero references anywhere**, including tests. Fully dead.
- `IBackgroundTileSource`: **zero references anywhere**, including tests. Fully dead.
- `BackgroundTileDescriptor`, `BackgroundTileRequest`, `BackgroundTilePayload`: referenced **only by `SampleImageGeneratorTests.cs`** (3 call sites) — not dead, but not wired into any production render path either. These represent a richer, source-agnostic contract that `SampleImageTile`/`ZeroCopyBitmapFactory` do not currently use (they use the leaner `BackgroundTileCacheKey` struct instead).

**Recommendation:** the ticket's three options (implement / delete / ADR-document) are all reasonable; given `ICW-076`'s ADR-0005 already establishes a source-agnostic mip strategy and is "In Progress," the richer contracts may be intentional scaffolding for that work rather than pure dead code. Recommend: **delete `IRenderer`/`ViewportRenderRequest`/`IBackgroundTileSource` outright** (genuinely zero-consumer, zero test coverage, no ADR references them) and **keep** `BackgroundTileDescriptor`/`Request`/`Payload` with an explicit doc comment or ADR note tying them to `ICW-076`'s future direction, since tests already depend on them.

---

### 3.14 Housekeeping items (fast, independent, no dependencies)

- **Delete `MipOptions.cs`** (E23) — zero references, no ticket names it, safe to remove in the same PR as `ICW-018`'s cleanup.
- **Delete the dead private `SampleImageGenerator.GenerateAnnotations`** (lines 574-622, E24) — confirm no reflection-based test depends on it (a quick grep for `"GenerateAnnotations"` in test files found none beyond the call at line 190), then remove; this also removes a second live copy of the `Confidence`/`Severity` feature-dictionary construction that would otherwise need to be kept in sync with `AnnotationGenerator.cs` if `ICW-031`'s typed-metrics migration proceeds.
- **`TileGridIndexLookup.cs:47`** — wrap `(row * columns) + column` in `checked { }` per the existing repo convention (`Bgra32BufferLayout.GetPixelOffset` already does this for the analogous calculation). Trivial, zero behavior change for valid inputs.
- **Close or rescope `ICW-060`/`ICW-P0-SPATIAL-INDEX-SAFETY`** — the specific "mutable STRtree list exposure" defect is already fixed (E16/E17). If there's a *different* remaining concern (e.g., `LiveSpatialIndexService.Query`'s `AppendMatches` doing an `O(n)` linear scan over `HotItems`/`PublishingItems` rather than the indexed `SnapshotIndex` — this is real but is a *performance* characteristic, not a safety/immutability bug), rewrite the ticket to describe that specific concern rather than leaving stale text that no longer matches the code.
- **Close or verify `ICW-099-serilog-eventlog-startup-fallback`** — the try/catch fallback already exists (E18). Recommend one verification step before closing: confirm (via a quick spike on a non-admin test machine, or reading `Serilog.Sinks.EventLog`'s source) that `WriteTo.EventLog(...)`'s admin-rights check actually happens synchronously inside that fluent call and not lazily on first log write outside the try block — this is the one piece of the claim I could not verify from static source alone.

---

## 4. Broader Backlog Triage

The remaining ~130 ticket files were **not** individually re-verified against source line-by-line in this session (time/scope-bounded); this table reflects lighter review — spot-checks, cross-referencing `active-tasks.md` status against file existence/basic grep, and the duplicate-ID inventory. Treat confidence as capped at 60% for anything not covered in §2–3 above.

| Ticket(s) | Tracker status | Triage recommendation | Confidence |
|---|---|---|---|
| `ICW-104`/`ICW-305` (tile-cache eviction policy) | To Do / Proposed, duplicate scope | `TileCacheBudget.TryReserve` (read in full, §Evidence) does use a real policy today: prefer evicting *generated* tiles over ungenerated ones, first-match-in-dictionary-order otherwise — i.e., **not pure FIFO-by-dict-order** as `ICW-305`'s text claims, but also not LRU. Merge these two tickets; rewrite scope as "replace first-match eviction with LRU," not "replace FIFO" (mischaracterization risk). | 70% |
| `ICW-031`/`ICW-080`/`ICW-111` (typed annotation metrics) | To Do / Proposed | All three target the same `Features["Confidence"]`/`["Severity"]` string-keyed pattern (E25). Recommend collapsing into one ticket: introduce `AnnotationMetrics(double Confidence, double Severity)` on `SampleAnnotation`, migrate `CreateAnnotationToolTip` (fix via §3.12 first as a stopgap), `AnnotationFeaturePresenter.BuildRows`, and the feature-grid binding together. Doing `ICW-101` first (§3.12) reduces this ticket's urgency since the crash risk is eliminated even before the typed migration. | 65% |
| `ICW-081`/dup-`ICW-100`/`ICW-084` (tracker hygiene) | Proposed/To Do | Confirmed real and worse than described: not just `ICW-061`–`065`, but also `ICW-004`, `ICW-005` (only one dup each — fine), `ICW-007`, `ICW-009`, `ICW-011`–`013`, `ICW-015`, `ICW-016`, `ICW-021`–`023`, `ICW-051`–`056`, `ICW-098`–`100` all have 2+ files. Recommend a mechanical script pass (per `ICW-084`'s own target file, `Validate-TaskTracker.ps1`) before any more tickets are filed — every new audit pass (including this one) burns time re-discovering which of two same-numbered tickets is current. | 90% (on the scope of duplication) |
| `ICW-110` (async void audit) | To Do | E28 confirms the count (21 handlers) but not individually that all 21 lack try/catch — recommend a mechanical Roslyn analyzer pass (e.g., `VSTHRD100`) rather than manual re-verification, which would be the efficient way to get exhaustive per-handler evidence this audit didn't have budget for. | 60% |
| `ICW-037` (accessibility baseline) | To Do | Not independently verified this session (would require reading all of `MainWindow.xaml`, not just the code-behind, which was out of this pass's LOC budget). Tracker's claim (no `AutomationProperties.Name`) is plausible given the codebase's demo-app origins but unverified here. | 40% |
| `ICW-036`/`ICW-138` (CI/nullable baseline) | Proposed/In Progress | Not independently verified — no `.github/workflows` directory exists in the tarball (confirmed: only `.github/agents/` and `.github/skills/` present), so **no CI currently runs on this repo at all** regardless of the ticket's "baseline solution build passed" claim (which would have been a local, not-CI, build). Flag this as a gap the ticket text doesn't surface: there is no automated verification of *any* claim in this entire tracker today. | 85% (on CI absence) |
| `ICW-076` (background tile mips, ADR-0005) | In Progress | Consistent with what's actually implemented — `BackgroundTileMipPolicy` (in `BackgroundTileContracts.cs`, read in full) is real, used, and matches the described 8-level ceiling policy. Tracker status here appears accurate. | 75% |
| `ICW-097`/`ICW-131` (Gray8/FastNoise performance) | In Review/Done | `SampleImageGenerator.GenerateNoisePixelsCore` (read in full) does use `FastNoise2` via `ArrayPool<float>` rental and a single native `GenUniformGrid2D` call — consistent with the ticket's described state. No RGB-to-Gray8 scalar conversion loop found anywhere in the current generator. Tracker status appears accurate. | 75% |
| All other `ICW-1xx`/`ICW-P0/P1`-prefixed items not listed above (`ICW-070`, `-071`, `-077`, `-090`–`096`, `-098`, `-112`, etc.) | Various | Not reviewed against source this session. Recommend triaging these against the duplicate-ID cleanup (`ICW-081`) first, since several may already be superseded or merged into the P0/P1 items covered in §3. | N/A — insufficient evidence to score |

---

## 5. Assumptions

- The commit pinned in the request (`afa8b5b8244424c253177943ad766ceef3bb1819`) is treated as authoritative "current state." I verified this SHA is `main`'s tip at the time of retrieval (confirmed via `GET /repos/.../commits/main` returning the same SHA), so this is not a stale/orphaned commit.
- Windows-only code paths (`#if WINDOWS` blocks: `ZeroCopyBitmapFactory.Windows.cs`, GDI+ generation paths, `DefectTemplateFactory`'s bitmap creation) were read as source but **not compiled or executed** — no Windows runtime was available in this environment. All findings on these paths are static-analysis-level confidence, not empirically reproduced.
- Test-pass claims embedded in `active-tasks.md` (e.g., "86 tests passing," "Release app build succeeded") were **not re-run** in this session — no build/test execution was performed (no .NET SDK / Windows target available in this container). These claims are taken as historical record only, not re-verified.
- "Confidence" percentages reflect this reviewer's calibration given the evidence actually gathered, not a formal statistical measure.

## 6. Open Questions (require a maintainer decision, not further code reading)

1. Should `ICW-P0-QUEUE-DRAIN`'s Phase 1 claimant-liveness check block on `ICW-P1-CLAIMANT-TOKENS`, or can a simpler interim check (item still present in `_items` with `ClaimantCount > 0`) ship independently? (§3.2)
2. Is the "cancel-thrashing" failure mode cited against `RemoveAllClaimants`-per-frame (§3.5) documented anywhere reproducible (a profiler capture, an issue writeup), or is it tribal knowledge from the `ICW-142` implementation session? If the latter, worth writing down before it's lost.
3. For `ICW-018`'s disposition (§3.13): does `ICW-076`'s in-progress source-agnostic mip work actually plan to consume `IBackgroundTileSource`, or was that interface speculative and already abandoned in favor of the simpler `BackgroundTileCacheKey` approach `SampleImageTile` uses today? This determines delete-vs-keep for that one interface specifically.
4. Is there an intended difference between `SpatialBounds.Intersects`'s closed-interval semantics and the half-open `[X, Right)` semantics used elsewhere (E26), or is one of the two simply wrong? I could not determine intent from the code or comments.
5. Given no CI exists at all (§4, `ICW-036` row), is there a target date for `ICW-036`, or is manual local verification the accepted state indefinitely? This changes how much weight to put on any "N/N tests passed" claim going forward, including in future audits.

---

*Methodology note: this report was produced by retrieving the full repository tree via `codeload.github.com` at the pinned commit (bypassing GitHub UI/API truncation), reading all 66 `.cs` files under `src/`, `tests/`, and `benchmarks/` in full via a paginated file-viewer, and cross-checking every load-bearing claim in `docs/tasks/active-tasks.md` and the external-audit synthesis docs against that source before writing any recommendation. Where a claim could not be verified from static source alone (build execution, runtime behavior, GDI+ thread-safety failure modes, historical incident claims), this is stated explicitly rather than inferred.*
