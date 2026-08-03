# InfiniteCanvasWPF — Audit Delta #3

**Scope of this pass:** Followed the pixelometer/tile-generation trail (from Delta #2) down into `TileCacheBudget` — the eviction/admission class embedded at the bottom of `SampleImageTile.cs` (lines 753–890, not previously read in full) — and cross-checked it against the closed `ICW-064` ticket that governs it, plus the surrounding eviction-policy ticket cluster (`ICW-003`, `ICW-104`, `ICW-305`).
**This document is deltas only.** Several avenues investigated this pass turned out to already be fully covered by existing "Proposed" tickets rather than new gaps — see the "Investigated, already tracked" section at the end so this isn't independently re-flagged in a future pass.

---

## New Finding — `TileCacheBudget.TryReserve`'s eviction candidate search never checks `IsGenerationQueued`, contradicting the closed, P0 `ICW-064` ticket's own documented behavior — **High confidence, precise quote available**

`ICW-064` ("Bound lazy tile-cache admission without evicting visible tiles," **status: Done**, priority P0) states in its own Notes section:

> *"Cache admission reserves space before background work starts. **Eviction skips viewport-pinned and still-generating entries**; a rejected reservation leaves the placeholder in place and a failed generation releases its reservation."*

The actual eviction candidate search in `TileCacheBudget.TryReserve` (full method read, `SampleImageTile.cs` lines 804–852):

```csharp
var evictedTile = _trackedTiles.Values.FirstOrDefault(candidate =>
    !string.Equals(candidate.Id, tile.Id, StringComparison.OrdinalIgnoreCase)
    && !_pinnedTileIds.Contains(candidate.Id)
    && candidate.IsImageGenerated)
    ?? _trackedTiles.Values.FirstOrDefault(candidate =>
        !string.Equals(candidate.Id, tile.Id, StringComparison.OrdinalIgnoreCase)
        && !_pinnedTileIds.Contains(candidate.Id));
```

This filters out the tile being admitted and anything currently viewport-pinned (`_pinnedTileIds`, refreshed every frame from the visible set via `SetPinnedTiles`) — but nowhere in either tier does it check `candidate.IsGenerationQueued` (the exact property that exists for precisely this purpose: `IsGenerationQueued => Volatile.Read(ref _generationQueued) == 1 && !IsImageGenerated`). The second, fallback tier (`?? ...`) will happily select **any** non-pinned, non-self tracked tile — including one whose background generation is actively in flight on a worker thread right now.

Concretely: a tile scrolled just out of view a moment ago (so no longer in `_pinnedTileIds`, since pinning is recomputed from the current visible set every frame) but whose generation was kicked off while it *was* visible or hovered, and is still running, is a legal eviction target today. Evicting it calls `evictedTile.ResetImageCache()`, which bumps that tile's generation epoch — so when the in-flight background computation eventually finishes, `OnCoordinatorPixelsGenerated`'s stale-epoch guard (`ICW-P0-STALE-PUB`) correctly discards the result. Functionally the tile isn't corrupted, but the CPU work that was already most of the way to completion is thrown away for a candidate that, per `ICW-064`'s own written acceptance behavior, should have been skipped in favor of a truly idle (not-yet-started) candidate if one existed in the same tracked set.

This is a **regression-from-documentation**, not a newly-introduced bug: `ICW-064` is marked Done and its Notes assert the "skips ... still-generating entries" behavior as already-shipped fact, but the code doesn't implement that check. Worth flagging as a correction to `ICW-064` specifically (reopen or file a small follow-up), separate from the broader "eviction isn't LRU / uses dict order" concern already tracked under `ICW-003`/`ICW-104`/`ICW-305` — this is a different axis (in-flight-work-awareness) that those tickets don't mention either.

**Recommendation:** Add `&& !candidate.IsGenerationQueued` as a third, most-preferred eviction tier (idle-and-generated → idle-and-ungenerated → in-flight, only as a last resort), or at minimum filter in-flight tiles out of the fallback tier entirely, since evicting them wastes work without saving any *currently allocated* memory (the buffer isn't written until the factory returns) beyond the tracked-tile-count/pending-request slot itself. If `ICW-104`'s planned LRU rewrite (see below) is imminent, this can be folded into that work rather than patched standalone — but either way `ICW-064`'s status/notes should stop asserting a protection that isn't there.

---

## Investigated, already tracked (no new report needed — recorded so it isn't re-flagged)

- **Eviction uses non-deterministic dictionary-order `FirstOrDefault` instead of LRU/recency.** Already tracked three times over (`ICW-003-tilecachebudget-lru`, `ICW-104-tilecache-eviction-policy`, `ICW-305-tilecache-eviction-policy` — itself a known duplicate/orphan situation per `JIRA.md`'s own entry for `ICW-305`, *"Register the orphaned cache-policy ticket."*). Confirmed the code matches the description in all three; nothing to add.
- **`TileCacheBudget.UsedBytes` can drift from actual resident bytes because reservation release doesn't cover every discard path, and `_pixelCost` is mip-0-only.** Both already tracked in detail and at high stated confidence under `ICW-P0-LEASE-RELEASE` (98%) and `ICW-P1-PIXELCOST-MIPS` (95%) respectively — independently tracing the stale-epoch-discard path in `OnCoordinatorPixelsGenerated` (no `Release()` call on discard) confirms the mechanism `ICW-P0-LEASE-RELEASE` describes, but adds no new information beyond what that ticket already states with higher confidence than I'd independently claim.
- **`DescribeStatus()` reads `UsedBytes`, `MaxBytes`, `ResidentTileCount`, and `EvictionCount` via separate, non-atomic reads/locks**, so the status string could theoretically interleave with a concurrent eviction. Considered but not written up as its own finding — this is cosmetic-only (a status label momentarily very slightly stale, self-correcting on the next update tick) and not worth a ticket on its own; noting it here only in case a future pass is tempted to treat it as more serious than it is.

---

*This delta report should be read alongside `icw-wave-e-audit.md` and `icw-wave-e-audit-delta-2.md`; it does not repeat their content.*
