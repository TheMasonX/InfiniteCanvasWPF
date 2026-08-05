# InfiniteCanvasWPF — Audit Delta #5 (post-council, Wave F review)

**Context:** The prior four reports were extracted into a master findings ledger (C1–C48), independently reconciled by a three-seat council against `b5e1e8b`, and a separate Wave F commit (`4467593`, "cooperative viewport cancellation for tile generation") has since landed on top of that. This pass (1) audits Wave F's actual code changes — genuinely new, unreviewed work — and (2) reconciles a few loose ends against the council's disposition.

**Headline: Wave F's own fix reopens the exact race it was written to speed up.** Details below.

---

## New Finding — `Request()`'s coalescing check can re-attach a fresh claimant to an already-canceled, still-physically-running work item, silently discarding the re-request — **High confidence, directly enabled by Wave F's own design change**

Wave F deliberately changed `CancelWorkItem` so a **running** item is no longer removed from `_items` when canceled — it now stays there until `HandleWorkStopped` runs on physical worker termination (the commit's own comment: *"Running work remains in `_items` until the worker physically stops. A cancel-and-re-request can therefore admit duplicate work briefly. Epoch guards discard stale results, but the duplicate still costs CPU."*). That comment correctly anticipates that a re-request during this window causes wasted CPU from the old worker — but it does not anticipate what happens to the *re-request itself*.

`Request()`'s coalescing check (line 126, unchanged by Wave F):

```csharp
if (_items.TryGetValue(key, out var existing))
{
    existing.AddClaimant(claimantId, claimantToken, onCompleted, onFailed);
    Interlocked.Increment(ref _coalescedCount);
    return true;
}
```

This only checks *presence* in `_items`, never `existing.State`. `TileWorkItem.AddClaimant` (verified, full method read) has no state guard either — it unconditionally adds the claimant to `_claimants` regardless of whether the item is already `Canceled`.

So the sequence that now reliably reaches this bug:

1. Tile at key `K` (fixed `SourceId`/`TileId`/mip/epoch — unchanged unless `ResetImageCache` runs) is generating, `State = Running`.
2. Viewport interest changes (scroll away) → `CancelWorkItem(K, item)` runs → `item.State = Canceled`, but per Wave F, `item` **stays in `_items[K]`** because it's still running.
3. Viewport interest changes back (scroll back to `K`) before the old worker physically finishes → `Request(K, ...)` runs → finds `_items[K]` still populated with the *old, already-canceled* item → calls `existing.AddClaimant(...)`, attaching the new claimant (and its `onCompleted`/`onFailed` callbacks) to the dying item instead of starting fresh work.
4. The old worker eventually observes cancellation (now much sooner than pre-Wave-F, thanks to the same commit's cooperative token checks) and throws `OperationCanceledException`, caught in `StartWorkItem`, which calls `item.DispatchFailed(...)`. `DispatchFailed` snapshots **every currently-registered claimant's `OnFailed`** (verified, full method read) and invokes each — including the claimant added in step 3.
5. The re-request's caller (`SampleImageTile.OnCoordinatorPixelsGenerationFailed`) receives a cancellation/failure for a generation it never actually got to run, resets its `_generationQueued` flag, and — assuming nothing else re-triggers it — the tile sits with no fresh work in flight until the next code path happens to call `EnsurePixelsGenerationStarted` again.

This isn't a rare theoretical interleaving — it's the exact "scroll away, scroll back quickly" pattern that `ICW-143`/`ICW-144`'s own `FastScrollStress_ThreeCycles` benchmark exists to simulate, and Wave F's stated goal (faster, cooperative cancellation) makes the cancel-to-actually-stopped window *shorter* but does not close the re-request-during-that-window hole; it just changes its size. Net effect: a fast scroll-away-and-back gesture can silently swallow one legitimate re-request per affected tile, costing at least one extra round trip (viewport publish → discovered-missing → re-request) before the tile actually starts regenerating — on top of, not instead of, the CPU waste the commit's own comment already flags.

**Why this is a genuine regression and not just a restatement of the commit's known trade-off:** the commit's comment frames the cost purely as "duplicate work costs CPU." It does not flag that the coalescing path silently *drops* the re-request's intent rather than duplicating it — "duplicate admission" implies two generations run; what actually happens is one dying generation absorbs a second claimant that gets nothing.

**Recommended fix shape (two parts, both needed):**

1. In `Request()`, treat a terminal-state existing item (`Canceled`/`Completed`/`Failed`) as *not present* for coalescing purposes — proceed to create and admit a fresh `TileWorkItem` instead of calling `AddClaimant` on it.
2. Because `_items` is keyed only by `BackgroundTileCacheKey` and a stale worker can now outlive its removal from that role, `HandleWorkStopped` (and `CancelWorkItem`'s queued-path removal) must verify `_items.TryGetValue(key) == item` (reference equality, not just key presence) before removing/releasing — otherwise a late-arriving old worker's termination could clobber a *newer* item that has since taken over the same key, removing it from `_items` and releasing its cache reservation out from under it while it's still legitimately running. This second part isn't yet triggerable today only because part 1's bug currently prevents a second item from ever being created for the same key while the first is still alive — fixing part 1 without part 2 would introduce this second, currently-latent hazard.

This should attach to `ICW-WAVE-F-VIEWPORT-CANCELLATION` as a direct follow-up (it names `ICW-P0-ACTIVECOUNT-residuals` and `ICW-P1-COOPERATIVE-CANCEL` as related — this bug sits squarely between them) rather than being filed as an unrelated new ticket.

---

## Reconciliation notes (not new findings — corrections/confirmations against the council review)

- **C20 (`DrawDefectPatch` reads `DefectBitmap` into an unused local) and C23 (pixelometer fallback allocates/sorts under lock) were rejected as "stale" by the Implementation and Runtime Reviewer seat.** Re-verified against the current HEAD (`4467593`): both code paths are byte-for-byte unchanged from when originally reported — `var value = sourceRow[sourceX * 3];` in `ZeroCopyBitmapFactory.Windows.cs` is still assigned and never read, and `TryGetPixelsNonBlocking`'s fallback-candidate `List`+`OrderBy`+`ThenBy` construction under `_cacheGate` is still present verbatim. "Stale" doesn't appear to mean "already fixed" here, since neither has changed. Worth a clarifying pass on the council record — if "stale" reflects a severity/priority judgment rather than a factual one, that's a defensible call, but the current wording could be read as "no longer true," which isn't accurate. Not resurfacing these as new findings; just flagging the record so it isn't misread later as "confirmed fixed."
- **C34 (cache eviction can select an actively-generating tile because `TileCacheBudget.TryReserve` never checks `IsGenerationQueued`) was accepted by the council** ("Extend ICW-064 and ICW-104") but Wave F did not touch `TryReserve` — reverified, the eviction candidate search is unchanged. Still open, as expected; noted only so it's not assumed resolved by Wave F's unrelated cancellation work.
- **C7 (`TileWorkItem.GetClaimantIds()` orphaned)** — reverified still present and still uncalled at `4467593`. Unchanged, as expected (no ticket claims this was actioned yet).

---

*This delta report should be read alongside `icw-wave-e-audit.md` and Deltas #2–#4; it does not repeat their content. Fixed point for this pass: commit `4467593397be3201bdcafdbf03a68614392b6341`.*
