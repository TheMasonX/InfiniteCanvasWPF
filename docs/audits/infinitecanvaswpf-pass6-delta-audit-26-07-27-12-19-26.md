# InfiniteCanvasWPF — Audit Pass 6 (Delta Only)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` ("docs: add tile-generation stability sprint handoff doc")
**Baseline:** `infinitecanvaswpf-pass5-delta-audit-26-07-27-05-58-30.md` (HEAD `ffe990a9`)
**13 new commits since pass 5**, all one continuous session implementing `ICW-142` (`TileWorkCoordinator`) plus five in-session hotfixes to it, chronicled by the author in `docs/handoffs/2026-07-27-tile-generation-stability-sprint.md` (read in full and verified against code, not taken at face value — see §6).
**Method:** full read of the new `TileWorkCoordinator.cs` (600 lines), the coordinator-integration sections of `SampleImageTile.cs` and `MainWindow.xaml.cs`, `TileCacheBudget`'s new eviction fallback, the 19 new coordinator unit tests (by name, to check what's *not* covered), and cross-check against `ICW-142`/`ICW-143`/`ICW-146` tickets and `ADR-0006`. Confirmed current status of every open item from pass 5.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **Claimant cancellation tokens are hardcoded to `CancellationToken.None` at both production call sites** (`SampleImageTile.cs:428` and `:553`). The coordinator's entire auto-remove-on-token-fire mechanism — the thing `ICW-142`'s own progress notes describe as done ("Per-claimant token registration that auto-removes claimants when their token fires") — is provably unreachable in production today, and only exercised by its own isolated unit test. The *only* claimant-removal path that actually runs is the explicit `RemoveClaimant` call inside `ResetImageCache()`. | **High** | 92% |
| 2 | **`SampleImageTile.DefaultCoordinatorClaimant` is `static readonly`** — one object shared by every tile instance in the process, not per-tile. Harmless today only because nothing in production calls `RemoveAllClaimants` (coordinator-wide claimant removal). `ICW-143` is the very next planned ticket and will need exactly this kind of "remove interest when a tile leaves the viewport" call — if it reaches for `RemoveAllClaimants(GetClaimantId())`, the obvious API given current wiring, it will retract *every* tile's interest across the whole coordinator in one call. | **High** | 85% |
| 3 | Even with a real per-claimant token, the auto-remove callback (`claimantToken.Register(() => RemoveClaimant(...))`, `TileWorkCoordinator.cs:506`) resolves to `TileWorkItem.RemoveClaimant` — an inner instance method that only cancels the work token — **not** the coordinator's own `RemoveClaimant`, which additionally decrements `_activeCount`, removes the item from `_items`, and drains the queue. For a *queued* (not-yet-started) item, this leaves a dead-token item sitting in the queue to be dequeued and started later by `DrainQueue` (which never checks whether the token is already canceled) before failing — burning a concurrency slot instead of freeing one promptly. Currently dormant (blocked by #1); becomes live the moment #1 is fixed by wiring in a real token, which is the natural next step. | Medium | 75% |
| 4 | **Confirmed still open, unchanged:** pass 5's §1 (`InitializeSpatialState()` resetting `MainViewModel`/`DataContext` — and the user's background-noise settings with it — on every `RegenerateSceneAsync`) is untouched by this window; `InitializeSpatialState()` is still called from `RegenerateSceneAsync` (`MainWindow.xaml.cs:173`) with no `ApplySettings` call after it. | High (unchanged) | 95% |
| 5 | **Confirmed still open, partially but not fully mitigated:** pass 5's §2 (defect-template-pool disposal racing an in-flight render's `LockBits`) is unchanged. This window added `_tileCoordinator.CancelAll()` immediately before `DisposeDefectTemplatePools(_tiles)` — a good addition, but it cancels *coordinator tile-generation* work, which was never the actor at risk. The actual risk is a concurrently-running `RenderFrameAsync` background task calling `DrawDefectPatch`→`LockBits` on the same GDI+ bitmaps, and nothing here fences against that. Do not read the new `CancelAll()` call as having closed this. | High (unchanged) | 80% |
| 6 | **Confirmed still open:** `ICW-078`'s `RenderRequestTracker` wiring is absent from `MainWindow.xaml.cs` for the third consecutive pass and 19th commit. Concretely, this now blocks correctly-sequenced work: `ICW-143` lists `ICW-078` as a `dependsOn` and its own Notes say to "preserve `RenderRequestTracker` stale-frame guards... during transitions" — but `active-tasks.md`/`task-tracker.md` still say `ICW-078` is `Done`, so whoever picks up `ICW-143` next has no tracker signal that its stated dependency isn't actually met. | High (unchanged) | 95% |
| 7 | The sprint handoff's own root-cause narrative for its "Known issue #1" (cache thrashing) — *"the evicted tile may have in-flight coordinator work that gets wasted (epoch bump... causes completion discard)"* — undersells what the code actually does: `TileCacheBudget.TryReserve`'s eviction path calls `evictedTile.ResetImageCache()`, which calls `_coordinator.RemoveClaimant` for the evicted tile's own key at every mip level. With the current single-shared-claimant model, that *should* actually cancel the in-flight work via `CancelWorkItem`, not merely let it complete and get discarded. Flagged as an open question, not a contradiction — worth someone confirming with a log trace, since the discrepancy between the doc and the code is itself worth resolving either way. | Low (informational) | 60% |
| 8 | `CancelWorkItem`'s `wasRunning = !item.SetRunning() && _activeCount > 0` (`TileWorkCoordinator.cs:373`) reuses a state-*mutating* method named for a command (`SetRunning`) purely to read prior state — a command/query separation smell. Netted-out behavior is currently correct only because every call to `SetRunning()` happens to already be serialized under the coordinator's own `_lock`, making the `Interlocked.Exchange` inside it redundant. Not a bug today; a maintainability trap for the next person who reads `Interlocked.Exchange` and assumes `_running` is safe to touch without holding `_lock`. | Low | 85% |

**Bottom line:** the coordinator itself (concurrency bound, coalescing, counters, disposal) is solid and well-tested in isolation — 19 focused unit tests is real coverage. The gap is entirely at the **integration seam**: the specific tokens and IDs `SampleImageTile` hands the coordinator don't yet match what the coordinator's own design assumes, and that seam is exactly where `ICW-143` is about to build next. Findings #1–#3 are cheapest to fix *before* `ICW-143` starts, not after.

---

## 1. [HIGH] Claimant tokens are `CancellationToken.None` — auto-removal path is dead in production
**Confidence: 92%**

```csharp
// SampleImageTile.cs:428 (mip 0) and :553 (mip N) — identical pattern at both sites
var admitted = _coordinator.Request(
    key,
    async token => { ... },
    GetClaimantId(),
    CancellationToken.None,               // <-- claimantToken
    onCompleted: OnCoordinatorPixelsGenerated,
    onFailed: OnCoordinatorPixelsGenerationFailed,
    tryReserve: tryReserveCacheEntry);
```
`TileWorkItem.AddClaimant` only registers the auto-remove callback when `claimantToken.CanBeCanceled`:
```csharp
if (claimantToken.CanBeCanceled)
{
    registration = claimantToken.Register(() => RemoveClaimant(claimantId));
}
```
`CancellationToken.None.CanBeCanceled` is always `false`, so this branch never runs at either call site. The *only* code path that ever calls `RemoveClaimant`/`RemoveAllClaimants` in production is the explicit loop inside `ResetImageCache()` (fired on eviction and scene regeneration). This matches the sprint's own "Known issue #2: No viewport culling" — but that framing describes it as a missing *feature*; the actual state is that the mechanism designed to support that feature (per-claimant tokens) is already built, tested in isolation (`PerClaimantToken_RemovesClaimantWhenTokenFires`), and wired to a value that guarantees it never fires. `ICW-142`'s own progress notes list "auto-removes claimants when their token fires" as done — true of the coordinator class in isolation, not true of the integration.

**Recommendation:** before starting `ICW-143`, either (a) wire a real per-viewport-generation token through `EnsurePixelsGenerationStarted`/`EnsureMipPixelsGenerationStarted` now — cheap, and gives `ICW-143` something to hang off of — or (b) explicitly note in `ICW-143`'s scope that claimant lifetime will be entirely non-token-based (explicit `RemoveClaimant` calls only) and drop the token parameter's auto-remove behavior from the design going forward so the next reader isn't misled by the XML doc comment's promise ("When this token fires, the claimant is considered removed").

---

## 2. [HIGH] Shared static claimant object — safe today, a footgun for `ICW-143`
**Confidence: 85%**

```csharp
// SampleImageTile.cs:27
private static readonly object DefaultCoordinatorClaimant = new();
...
private object GetClaimantId() => ClaimantIdProvider?.Invoke() ?? DefaultCoordinatorClaimant;
```
`static` — one instance for the whole process, not one per tile. `MainWindow.xaml.cs:214` explicitly sets `_tiles[i].ClaimantIdProvider = null;` for every tile with the comment *"Use default stable claimant"*, so in the running app, literally every tile's every generation request currently uses this same object as its claimant identity.

This is safe *only* because the coordinator's `RemoveAllClaimants(claimantId)` — which removes a claimant from *every* work item across the *entire* coordinator, by design (see `TileWorkCoordinator.cs:196-224`) — is never called with it in production (confirmed: its only caller anywhere in the repo is a unit test). `RemoveClaimant(key, claimantId)`, which *is* used (from `ResetImageCache`), is scoped to one key, so the sharing is inert there.

`ICW-143`'s stated acceptance criteria include *"A tile outside the current interest set is not started if it is still queued, and running work loses its claim promptly"* — the natural, minimal-diff way to implement that against the current API is exactly `RemoveAllClaimants(someClaimant)` when a tile scrolls off-screen, and `GetClaimantId()` is the obvious source for `someClaimant` since it's already there. If that's how it gets implemented, the first time it fires it will cancel every other tile's in-flight generation too — a wide, silent, hard-to-reproduce-from-a-bug-report regression (symptoms would look like "everything keeps re-generating" or "generation frequently cancels for no reason").

**Recommendation:** make the claimant identity per-tile (or per-viewport-frame, per `ADR-0006`'s design) *before* `ICW-143` lands — e.g. `ClaimantIdProvider = () => this` (the tile itself), or a small per-frame token object if `ICW-143` wants frame-scoped claimants instead. Either removes the shared-instance hazard outright. Cheap, and directly unblocks `ICW-143`'s acceptance criteria rather than working around this.

---

## 3. [MEDIUM] Token-fired auto-remove bypasses coordinator-level bookkeeping for queued items
**Confidence: 75%** — mechanism confirmed by reading; currently unreachable (gated by #1), so severity is speculative pending #1 being fixed.

```csharp
// TileWorkItem.AddClaimant, TileWorkCoordinator.cs:502-507
if (claimantToken.CanBeCanceled)
{
    registration = claimantToken.Register(() => RemoveClaimant(claimantId));
}
```
Inside `TileWorkItem`, unqualified `RemoveClaimant` resolves to `TileWorkItem.RemoveClaimant(object)` — the instance method — not `TileWorkCoordinator.RemoveClaimant(key, claimantId)`. `TileWorkItem.RemoveClaimant` (line 517) only removes the claimant from its own list and, if that was the last one, calls `CancelWork()` — which just does `_workCts.Cancel()`. It does **not** touch the coordinator's `_activeCount`, `_items` dictionary, or `_queue`, and does not call `DrainQueue()`.

For a **running** item this self-heals: the in-flight `Task.Run` (`TileWorkCoordinator.cs:294-346`) will observe `OperationCanceledException` from the now-canceled token and run `HandleWorkStopped`, which does the full cleanup. But for a **queued** item (not yet started), nothing removes it from `_queue`/`_items`. `DrainQueue()` (line 399-412) only checks `item.State == TileWorkItemState.Queued` before calling `StartWorkItem` — it never checks whether the work token was already canceled by a departed claimant. So a queued item whose only claimant left via token cancellation will sit in the queue, eventually get dequeued and started anyway, occupy one of the (default 4) concurrency slots, and then fail almost immediately once the factory or the `token.ThrowIfCancellationRequested()` check (mip path, `SampleImageTile.cs:549`) observes the stale cancellation — wasting a slot that a genuinely-useful queued request could have used instead.

**Recommendation:** when wiring in real tokens for #1, either route the auto-remove callback through the *coordinator's* `RemoveClaimant(key, claimantId)` instead of the item's own method (requires capturing the coordinator/key in the closure, which `AddClaimant` doesn't currently have access to — a small signature change), or have `DrainQueue`/`StartWorkItem` skip and evict any item whose `WorkToken.IsCancellationRequested` is already true before starting it. Either is a small, contained fix — worth doing in the same change as #1, since #1's fix is what makes this reachable.

---

## 4–6. Confirmed still-open items from pass 5 (no new evidence needed — status check only)

| Pass 5 finding | Status at `139a8b6` | Note |
|---|---|---|
| §1 — `InitializeSpatialState()` resets `MainViewModel`/background-noise settings on every regenerate | **Unchanged, still open** | `InitializeSpatialState()` still called from `RegenerateSceneAsync` (`MainWindow.xaml.cs:173`); no `ApplySettings` call added after it in this window. |
| §2 — defect-template-pool dispose races an in-flight render's `LockBits` | **Unchanged, still open** — see note below | `_tileCoordinator.CancelAll()` was added right before the dispose call this window, but it cancels the coordinator's tile-*generation* work, not the render pipeline. The actual race is against `RenderFrameAsync`'s background `Task.Run`, which is untouched by this change. Don't mistake the new `CancelAll()` line for a fix to this specific finding. |
| `ICW-078` — `RenderRequestTracker` wiring reverted, never re-applied | **Unchanged, still open, now blocking** | Still absent from `MainWindow.xaml.cs` (`grep` count: 0). `ICW-143` lists it as a `dependsOn` while tracker docs still say `Done` — see §6 above for why this now matters concretely rather than abstractly. |

---

## 7. [Open question] Handoff doc's account of "Known issue #1" may not match the code
**Confidence: 60% —** flagging the discrepancy, not asserting which side is wrong; would take a log trace or a targeted test to settle.

Handoff doc: *"the evicted tile may have in-flight coordinator work that gets wasted... epoch bump on ResetImageCache causes completion discard."* Code (`SampleImageTile.cs:296-313`): `ResetImageCache()` bumps the epoch **and** loops over every mip level calling `_coordinator.RemoveClaimant(oldKey, claimant)`. Given the single-shared-claimant setup (see §2), that removal should make `ClaimantCount == 0` for the evicted tile's own in-flight work and trigger `CancelWorkItem` — actual cancellation, not merely a wasted completion that gets discarded by epoch check. Both could be true simultaneously (e.g. cancellation races the in-flight `await item.Factory(...)` and the factory doesn't check `WorkToken` often enough to actually stop early — plausible, since the mip-path factory only checks cancellation *after* the factory call returns, not during it), but that's a different, more specific claim than the handoff makes. Worth a quick trace of `Coord CANCEL` vs `TileGen DISCARD` log lines under the same fast-scroll repro that motivated this sprint, to know which is actually happening before tuning `DefaultMaxConcurrency`/`DefaultMaxBytes` (the handoff's own suggested next steps) based on the wrong mental model of where the waste comes from.

---

## 8. [LOW] `SetRunning()` used as a disguised query in `CancelWorkItem`
**Confidence: 85%**

```csharp
var wasRunning = !item.SetRunning() && _activeCount > 0;
```
`SetRunning()` is `Interlocked.Exchange(ref _running, 1) == 0` — a command ("transition to running, tell me if that was a fresh transition") being called here purely to learn whether the item was *already* running, with the mutation as an accepted (and here, harmless-because-idempotent) side effect. Every call site of `SetRunning()` — `StartWorkItem` and `CancelWorkItem` — already runs under the coordinator's own `_lock`, so the `Interlocked` semantics add nothing; a plain bool field read under the existing lock would do. Not a functional bug, but exactly the kind of "atomic primitive layered on top of a lock that already protects the same field" pattern that misleads future readers into thinking `_running` is safe to touch lock-free.

**Recommendation:** replace with a plain `bool _isRunning` field guarded by `_lock`, and give `CancelWorkItem` a real query (e.g. `item.State == TileWorkItemState.Running`, which is already tracked and already lock-protected) instead of re-deriving the same fact through a second, differently-named mechanism.

---

## Suggested Priority

1. **§1 + §2 together** — fix before `ICW-143` starts. Both are cheap (swap a claimant-ID source, wire a real token) and both directly unblock `ICW-143`'s stated acceptance criteria rather than requiring `ICW-143` to work around them.
2. **§3** — bundle into the same change as #1, since fixing #1 is what makes #3 reachable.
3. **Pass 5 §4/§1 (`InitializeSpatialState`)** — still the cheapest fully-open High-severity item in the backlog; unrelated to this sprint's scope, hasn't regressed, but hasn't been picked up either.
4. **Pass 5 §5/§2 (defect-pool dispose race)** and **`ICW-078`** — unchanged priorities from pass 5; `ICW-078` now has a concrete downstream consequence (§6) worth citing when re-prioritizing it.
5. **§7** — cheap to resolve (read the logs from the existing repro), do it before tuning concurrency/budget defaults on the strength of a possibly-imprecise root cause.
6. **§8** — trivial, bundle with any other low-priority cleanup pass.

## Assumptions & Open Questions

- All findings are from static reading of the tarball at `139a8b6`; no build or test run was performed as part of this pass (no Windows execution environment available), consistent with every prior pass's stated methodology.
- §3's severity is contingent on §1 being fixed the "obvious" way (passing a real token). If the eventual `ICW-143` fix instead relies entirely on explicit `RemoveClaimant` calls and never wires a cancelable token through `Request`, §3 stays permanently unreachable and can be closed as not-applicable rather than fixed.
- §7 is explicitly not a claim that the handoff doc is wrong — only that the code supports a more specific and more favorable story than the doc states, and the two should be reconciled with evidence (a log trace) rather than left as an assumption in either direction.

