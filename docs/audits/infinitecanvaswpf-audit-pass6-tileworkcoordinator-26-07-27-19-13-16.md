# InfiniteCanvasWPF — Audit Pass 6 (Delta): `TileWorkCoordinator` Concurrency Review

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` ("docs: add tile-generation stability sprint handoff doc")
**Baseline for this pass:** `ffe990a` (my pass-5 report). 9 new commits reviewed, the bulk of which are a "stuck-generation" bug-fixing sprint against a brand-new file, `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs` (599 lines, new), which now owns bounded/coalesced/cancellable background tile generation (ADR-0006, ICW-142/143).
**Method:** Read `TileWorkCoordinator.cs` in full, then walked each of the "stuck generation"/"cache eviction deadlock"/"render pipeline stall" fix commits' diffs individually to see exactly what each one changed, to check whether my findings below were already attempted and missed, or never touched at all.
**Delta-only**, per usual — nothing already covered by prior reports or the ~140 existing tickets is repeated. Also includes verification of two items carried over from pass 5.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **`TileWorkItem.AddClaimant` registers a cancellation callback on the claimant's token *before* adding the claimant to the tracked list.** If the token is already canceled at call time, `CancellationToken.Register` fires the removal callback synchronously and immediately — before there's anything to remove. The claimant is then added anyway, permanently un-removable via the normal cancellation path (a `Register` callback only fires once). This leaves a "ghost" claimant that keeps a tile's work item alive forever, matching the exact "stuck generation" symptom class the last 5+ commits have been fixing — and this specific bug was never touched by any of them. | **High** | 85% |
| 2 | Verified fix: the GDI+ handle leak I reported in pass 2 (§2.1, undisposed defect-template `Bitmap` pool on scene regenerate) **is now correctly fixed** via new `DefectTemplateFactory.DisposePool`/`SampleImageTile.DisposeDefectTemplatePools`, called at the right point in `RegenerateSceneAsync`. | — (verification) | 90% |
| 3 | **That same fix reopens a live race**: `_tileCoordinator.CancelAll()` is cooperative and non-blocking (cancellation tokens fire, but in-flight `Task.Run` factory work isn't awaited to actually stop), and is immediately followed by `DisposeDefectTemplatePools`, with **no synchronization against a concurrently running render frame** — `RenderFrameAsync` does not acquire `_generationGate` (confirmed: that gate has exactly one acquirer, `RegenerateSceneAsync` itself). If a render frame is mid-`LockBits` on a pooled `Bitmap` at the moment it's disposed, this throws — and per my pass-1 report, there's still no global exception handler, so this can crash the app. This is precisely what `ICW-103` describes, and unlike when I checked in pass 5, its premise is now genuinely live (a dispose call exists to race against). | **Medium-High** | 75% |
| 4 | Minor: `TileWorkItem.SetRunning()` uses `Interlocked.Exchange` for atomicity, but every call site (`StartWorkItem`, `CancelWorkItem`) already executes under the coordinator's own `_lock` — the interlocked exchange is fully redundant, and `CancelWorkItem` reuses the same *mutating* "claim" method purely to *query* prior state, which is confusing to read. A plain lock-guarded `bool` would behave identically and be clearer. | **Low** | 80% |
| 5 | Carry-over reinforcement (not new, stronger evidence): pass 5's §1 finding (`DrawDefectPatch`'s `DefectBitmap`/`LockBits` sampling is dead code) still applies unchanged. The new `DefectTemplateFactory.CreateBitmapFromPixels` confirms, from its own construction code, that the `Bitmap` is built via a manual per-pixel unsafe copy loop from the *same* `pixels` array that also becomes `DefectPixels` — i.e., the bitmap is a byte-for-byte redundant copy, constructed and now correctly disposed, entirely to feed a value `DrawDefectPatch` still discards. Deleting it (as recommended in pass 5) would also eliminate finding #3's race and remove the need for `DefectTemplateFactory`'s Windows-only bitmap path altogether. | — (reinforcement) | 90% |

**The throughline:** findings #1 and #3 both look like plausible, still-open contributors to the exact bug class ("stuck generation," "render pipeline stall," "cache eviction deadlock") that the last several commits were fighting — worth checking against whatever specific repro steps motivated those commits before assuming they're fully resolved.

---

## 1. [HIGH] `AddClaimant` register-before-add ordering can create an unremovable "ghost" claimant
**File:** `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs:486-508`
**Confidence: 85%**

```csharp
public void AddClaimant(
    object claimantId,
    CancellationToken claimantToken,
    Action<BackgroundTileCacheKey, byte[]>? onCompleted,
    Action<BackgroundTileCacheKey, Exception>? onFailed)
{
    lock (_claimantLock)
    {
        var existing = _claimants.Find(c => c.Id.Equals(claimantId));
        if (existing is not null) { /* update callbacks */ return; }

        CancellationTokenRegistration? registration = null;
        if (claimantToken.CanBeCanceled)
        {
            registration = claimantToken.Register(() => RemoveClaimant(claimantId));   // ← (A)
        }

        _claimants.Add(new ClaimantEntry(claimantId, onCompleted, onFailed, registration));  // ← (B)
    }
}
```

`CancellationToken.Register` has well-documented .NET semantics: **if the token is already in the canceled state at the moment `Register` is called, the callback executes immediately and synchronously**, on the calling thread, before `Register` returns. Here, that means:

1. If `claimantToken` is already canceled when `AddClaimant` runs, line (A) synchronously invokes `RemoveClaimant(claimantId)` — but the claimant hasn't been added yet (line B hasn't executed). `RemoveClaimant` does `_claimants.FindIndex(c => c.Id.Equals(claimantId))`, finds nothing (`idx < 0`), returns `false`, does nothing.
2. `System.Threading.Lock` (the type backing `_claimantLock` here, per .NET 9's `lock` statement) is reentrant — a thread already holding it can re-enter — so this doesn't deadlock; `RemoveClaimant`'s own `lock (_claimantLock)` just re-enters cleanly and returns.
3. Line (B) then runs anyway, adding the claimant — **whose token has already fired its one-shot cancellation callback**. `CancellationTokenRegistration` callbacks fire exactly once, on the transition to canceled; since the token was *already* canceled at registration time, there is no future transition left to observe. This claimant can now only ever be removed by an explicit `RemoveClaimant`/`RemoveAllClaimants` call from elsewhere in the coordinator — never again via its own token firing.
4. If nothing else ever explicitly removes this specific claimant, `ClaimantCount` never returns to `0` for this work item via the token-driven path, which means the "last claimant removed → cancel the work" logic in `RemoveClaimant`/`RemoveAllClaimants` never triggers for it — the item can sit in `_items` indefinitely once the "real" reason for interest has gone away.

**Is the pre-canceled-token scenario actually reachable?** Yes, plausibly: `Request`/`AddClaimant` coalescing exists specifically to let a second interested party (e.g., a newly superseded frame, or a rapidly re-triggered viewport update) attach to an in-flight item behind the coordinator's `_lock`. Under load — which is exactly when "stuck" symptoms tend to surface — it's entirely possible for a claimant's own cancellation token (e.g., a per-frame or per-render-request token, matching the `RenderRequestTracker`/epoch concept from earlier work) to already be canceled by the time its `AddClaimant` call reaches the front of the lock, especially if the calling code created-and-immediately-superseded the token before the coordinator got to process the request.

**I confirmed this exact code path has never been touched by the recent fix commits**: of the 9 commits reviewed this pass, only one (`3736a72`, "remove per-frame claimant advance") mentions `AddClaimant` at all, and that change was a pure logging addition (`Log.Debug(...)`) with no reordering. The register-before-add sequence is unchanged from whenever this file was first introduced.

**Recommendation:** Add the claimant to `_claimants` first, *then* register the cancellation callback:
```csharp
var entry = new ClaimantEntry(claimantId, onCompleted, onFailed, null);
_claimants.Add(entry);
if (claimantToken.CanBeCanceled)
{
    var registration = claimantToken.Register(() => RemoveClaimant(claimantId));
    _claimants[_claimants.Count - 1] = entry with { Registration = registration };
}
```
This way, if the token is already canceled, `Register` fires immediately but `RemoveClaimant` now finds the just-added entry and removes it correctly — restoring the intended "already-canceled tokens are removed right away" behavior instead of creating a permanent ghost. Given how central this coordinator is to the "stuck generation" investigation already underway, this is worth checking against whatever specific repro scenario motivated those commits before concluding they're independent of this bug.

---

## 2–3. Verified fix + reopened race around defect-template bitmap disposal

**Confirmed fixed (pass-2 §2.1):** `DefectTemplateFactory.DisposePool` (new file) and `SampleImageTile.DisposeDefectTemplatePools` (new static helper, `SampleImageTile.cs:79-91`) correctly dispose the shared defect-template `Bitmap` pool, deduplicated by reference (`var disposed = new HashSet<object>();`) so a pool shared across many tiles is only disposed once. `MainWindow.RegenerateSceneAsync` calls it at the right point — after `_tileCoordinator.CancelAll()`, before `_tiles` is reassigned (`MainWindow.xaml.cs:173-185`). Good, verified resolution of a real leak.

**But this introduces a live race (§3), because `CancelAll()` doesn't wait for anything:**

```csharp
public void CancelAll()
{
    lock (_lock)
    {
        ...
        foreach (var key in keys) { CancelWorkItem(key, item); }   // signals _workCts.Cancel() — cooperative only
        _queue.Clear();
    }
}
```
`CancelWorkItem`'s in-flight branch calls `item.CancelWork()` → `_workCts.Cancel()` — this *requests* cancellation of the token passed to the running factory (`await item.Factory(item.WorkToken)`), but does not wait for that `Task.Run` continuation to actually observe the token and unwind. `CancelAll()` returns as soon as the signal is sent, not when the work has stopped.

Immediately after, `RegenerateSceneAsync` calls `SampleImageTile.DisposeDefectTemplatePools(_tiles)` on the *same* thread, disposing the outgoing scene's `Bitmap` pool right away. Meanwhile:
- The just-canceled background factory task may still be executing for some (short but nonzero) window before it observes cancellation.
- **More importantly**, `RenderFrameAsync` — which calls `DrawDefectPatch`'s `bitmap.LockBits(...)` on these same pooled bitmaps — is **not gated by `_generationGate` at all** (confirmed: `grep -n "_generationGate\."` across `MainWindow.xaml.cs` shows exactly one `WaitAsync` call, in `RegenerateSceneAsync`, and no corresponding wait in `RenderFrameAsync`). There is nothing stopping a render frame from being in-flight, mid-`LockBits`, on a `Bitmap` at the exact moment `RegenerateSceneAsync` disposes it on another thread.
- GDI+ `Bitmap` operations on a disposed bitmap throw (typically `ArgumentException`, GDI+'s generic "parameter is not valid" symptom for a disposed/invalid `Image`). Per my pass-1 report, there's still no `DispatcherUnhandledException` handler, so if this fires inside the render pipeline's `async void` chain, it can crash the app rather than being caught anywhere.

This is exactly the scenario `ICW-103` ("Protect `DefectBitmap` GDI+ usage from concurrent mutation/dispose") describes — worth noting that when I checked this in pass 5, no dispose call existed yet at all, so the ticket's premise wasn't actually live. It is now, as a direct side effect of the (correct, needed) leak fix.

**Recommendation, in order of preference:**
1. **Best:** implement pass-5's §1 recommendation (delete `DrawDefectPatch`'s dead `DefectBitmap`/`LockBits` sampling and the `DefectBitmap` field entirely, since the real rendered value already comes from `DefectPixels`). This removes the race by removing the shared mutable native resource that needs protecting in the first place — no locking needed if there's nothing left to lock.
2. **If `DefectBitmap` must stay for some reason not visible from this codebase:** either have `RegenerateSceneAsync` await actual completion/quiescence of `_tileCoordinator`'s canceled work before disposing (e.g., a `Task WaitForQuiescenceAsync()` on the coordinator), or gate `RenderFrameAsync` on the same `_generationGate` so a regenerate-in-progress can't overlap with a render — the latter is a bigger behavioral change (would briefly stall rendering during regenerate) so option 1 is clearly preferable given it's already independently recommended.

---

## 4. [LOW] `SetRunning()`'s atomic exchange is redundant given the surrounding lock discipline
**File:** `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs:373, 542-545`
**Confidence: 80%**

```csharp
public bool SetRunning() => Interlocked.Exchange(ref _running, 1) == 0;
```
Called from two places: `StartWorkItem` (`item.SetRunning();`, return value discarded — used purely for its mutating side effect) and `CancelWorkItem` (`var wasRunning = !item.SetRunning() && _activeCount > 0;` — used purely to *read* whether it was already running, discarding the fact that this call *also* sets it). Both call sites execute exclusively under `_lock` (`StartWorkItem` is only reachable from `Request` and `DrainQueue`, both of which hold `_lock`; `CancelWorkItem` is only reachable from `RemoveClaimant`/`RemoveAllClaimants`/`CancelAll`, all of which hold `_lock`). Since every access to `_running` is already serialized by `_lock`, the `Interlocked.Exchange` provides no additional safety over a plain `bool` field — it just makes the code harder to read, because `CancelWorkItem` has to invoke a method named "Set" purely to inspect state, discarding its own write.

**Recommendation:** Replace with a plain `private bool _running;` read/written directly under the existing lock, and give `CancelWorkItem` a non-mutating way to check prior state (e.g., check `State == TileWorkItemState.Running` instead, which is already tracked and already lock-guarded) rather than reusing `SetRunning()` for a query. Low priority — this isn't a bug, just avoidable complexity that makes the actual concurrency model harder to audit (which is exactly the risk category the ongoing "stuck generation" investigation is fighting).

---

## Suggested Priority (this pass only)

1. **§1** — highest-value: a concrete, previously-untouched mechanism that could independently explain "stuck" tile generation. Cheap to fix (reorder two statements), worth testing against whatever repro case motivated the recent fix commits.
2. **§3** — implement pass-5 §1's `DefectBitmap` removal (already recommended, now doubly motivated: removes dead CPU work *and* closes this race in one move) rather than trying to add synchronization around code that shouldn't need to exist.
3. **§4** — bundle into whichever session next touches this file; purely a readability/auditability improvement, no behavior change.
