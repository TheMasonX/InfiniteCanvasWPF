# InfiniteCanvasWPF — Audit Delta #6 (post priority-queue rewrite / MVVM extraction / ICW-204)

**Scope of this pass:** Re-fetched `origin/main` (16 new commits since the last audited state, including a `PriorityQueue<T,TPriority>` rewrite of `TileWorkCoordinator`'s queue — matches the fix I recommended in Delta #1/`ICW-144` — a large MVVM extraction into `InfiniteCanvas.ViewModels`/`CanvasControl`, triple-buffer frame rotation fixes, and `ICW-204`, "tile generation permanently lost on scroll"). Read every changed line of `TileWorkCoordinator.cs` (now 902 lines) end-to-end against the pre-rewrite version I already knew well, then traced `ICW-204`'s fix in `SampleImageTile.cs` against `TileWorkCoordinator.AddClaimant`'s current (unrelated-file) implementation to check the two changes are actually compatible with each other. They are not.
**This document is deltas only.** The `PriorityQueue`/tombstone rewrite (`_removedKeys`, `RebuildQueue`, `ComputePriority`) is a clean, correct implementation of exactly what I recommended for the old `Queue<T>` problem — read in full, no issues found, nothing to report there.

---

## New Finding — `ICW-204`'s fix causes every multi-frame tile generation to permanently lose its coordinator-level cancellation registration after one frame boundary, silently defeating claimant-token cancellation for exactly the generations where it matters most — **High confidence, fully traced, root-caused to a specific 5-line gap**

### The mechanism, traced end to end

1. `MainWindow.RenderFrameAsync` replaces and cancels `_frameTileCts` **every single frame**, unconditionally (verified at the call site: `Interlocked.Exchange(ref _frameTileCts, new CancellationTokenSource()); previousCts?.Cancel();`, at the top of every frame, not gated on any visibility change).
2. Each tile's claimant token, captured once via `GetClaimantToken()` at `Request()`-call time, is therefore guaranteed to fire on the very next frame after the request is admitted — regardless of whether the tile is still visible, and regardless of whether its generation has finished.
3. `ICW-204` (this session's newest fix) adds `RegisterClaimantReset(claimantToken)`, which registers a **second**, tile-local callback directly on that same claimant token: `claimantToken.Register(() => Interlocked.Exchange(ref _generationQueued, 0))`. This runs unconditionally, independent of whether the coordinator decided the work should actually be cancelled (the no-flash rule in `TileWorkCoordinator` can decide the opposite — keep the work alive because the tile is still in the interest set — and this reset fires anyway, because it's driven by token-fire, not by the coordinator's cancellation decision).
4. Since `_generationQueued` resets to 0 on the next frame, `EnsurePixelsGenerationStarted`'s guard (`CompareExchange(ref _generationQueued, 1, 0)`) passes again, and — for any generation that hasn't produced pixels yet — the tile calls `_coordinator.Request(...)` **again**, for the same cache key, on every subsequent frame until the generation actually finishes.
5. That second `Request()` call finds the key already in `_items` (the original item survived, per the no-flash rule) and coalesces: `TileWorkCoordinator.Request` → `existing.AddClaimant(claimantId, claimantToken, onCompleted, onFailed)`.
6. `TileWorkItem.AddClaimant`, read in full at current HEAD:
   ```csharp
   var existing = _claimants.Find(c => c.Id.Equals(claimantId));
   if (existing is not null)
   {
       _claimants[_claimants.IndexOf(existing)] = existing with
       {
           OnCompleted = onCompleted,
           OnFailed = onFailed
       };
       return;   // <-- claimantToken (the new one) is never touched
   }
   ```
   When the claimant ID already exists (true here — the tile's claimant ID, `_perTileClaimant`, is stable across the whole app per earlier audit passes), the method updates the callback delegates and returns **without ever looking at the newly-passed `claimantToken`**. The `ClaimantEntry.Registration` field — the `CancellationTokenRegistration` tied to the *original* token from frame N — is preserved unchanged by the `with` expression. No new registration is created for the frame-N+1 token that was just passed in.

### The consequence

The original registration was already consumed the moment frame N's token fired (that's what triggered this whole re-request in the first place) — a `CancellationTokenRegistration` on an already-cancelled token doesn't fire again. So after exactly one re-coalesce cycle, the claimant has **no live registration on any token that will ever fire in the future**. The claimant is now permanently un-removable via the token-cancellation path.

Once that happens, nothing else in the current design can cancel this claimant's interest in the tile either: `PublishInterestSet` only cancels `Queued` items directly (confirmed by reading the current method — it explicitly skips `Running` items by design), and `DrainQueueWithLivenessCheck`'s liveness check (`IsItemAlive`) only runs when a *queued* entry is dequeued, never against already-`Running` items. The only way a `Running` item now gets cancelled is via `RemoveClaimant` firing from a live token registration — which this exact sequence has just permanently disabled for this claimant.

**Net effect:** any tile generation that survives even one frame boundary (i.e., any generation that doesn't complete within a single frame — the case `ICW-204` exists to fix in the first place) permanently loses the ability to be cancelled by scrolling the tile away mid-generation. It will now run to completion and hold its `TileCacheBudget` reservation for its full natural duration no matter how far off-screen the tile scrolls, silently reintroducing the class of problem this ticket chain (`ICW-142` → `ICW-P1-CLAIMANT-TOKENS` → `ICW-204`) has been iterating on, just one mechanism further downstream. `ICW-204`'s own root-cause section is worth quoting because this finding is the direct continuation of the pattern it already names: *"ICW-142 removed per-frame `RemoveAllClaimants` because it caused cancel thrashing. `ICW-P1-CLAIMANT-TOKENS` re-introduced the same per-frame cancellation through the claimant token."* This fix re-introduces a variant of the *original* problem (uncancellable long-running work) one layer deeper, via the coalescing path `ICW-204` itself now exercises on every frame for any multi-frame generation.

### Why this wasn't caught by `ICW-204`'s own tests

`ICW-204`'s regression tests (per its own ticket, "Add regression tests for both paths") verify that the tile *does* regenerate after a token fire — they check the dedup-flag reset works, i.e., that re-`Request()` happens. They would not have caught this, because the *symptom* here isn't "generation doesn't restart" (it does, correctly) — it's "the coordinator loses the ability to cancel the restarted claim early," which only manifests as excess background CPU/reservation-holding during fast scroll-away, not as a stuck/blank tile. It's the opposite-shaped bug from the one being fixed, hiding behind the same code path.

**Recommendation:** Fix at the source — `AddClaimant`'s re-coalesce branch should dispose the stale `Registration` and create a fresh one against the newly-supplied `claimantToken` before returning, e.g.:
```csharp
if (existing is not null)
{
    existing.Registration?.Dispose();
    var registration = claimantToken.CanBeCanceled
        ? claimantToken.Register(() => RemoveClaimant(claimantId))
        : (CancellationTokenRegistration?)null;
    _claimants[_claimants.IndexOf(existing)] = existing with
    {
        OnCompleted = onCompleted,
        OnFailed = onFailed,
        Registration = registration
    };
    return;
}
```
This is a small, local, low-risk change and directly closes the gap without touching `ICW-204`'s own fix or the frame-cancellation model. Given the ticket-chain history here, it's worth adding a regression test that specifically holds a generation open across 3+ simulated frame boundaries (via re-`Request()` calls with fresh tokens on the same claimant ID) and then asserts that cancelling the *latest* token actually results in `RemoveClaimant`/cancellation firing — the existing tests only exercise a single request-then-cancel cycle, never a coalesce-then-cancel cycle, which is exactly the gap here.

---

*This delta report should be read alongside `icw-wave-e-audit.md` and `icw-wave-e-audit-delta-2.md` through `-5.md`. It does not repeat their content. The `PriorityQueue` rewrite and MVVM/`CanvasControl` extraction were both scanned this pass and are out of scope for this report only because nothing new was found in them yet — a follow-up pass should still look at `CanvasViewModel.cs`/`CanvasViewportViewModel.cs`/`CanvasControl.xaml.cs` for design-level findings, which this pass did not have room to cover.*
