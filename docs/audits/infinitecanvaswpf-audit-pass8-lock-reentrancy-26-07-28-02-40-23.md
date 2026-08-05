# InfiniteCanvasWPF — Audit Pass 8 (Same HEAD): Reentrant Lock Chain in Cache Eviction

**HEAD:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` — still unchanged; confirmed again via commit feed. No new commits to run a true delta against, so this pass continues the concurrency review of the tile-generation/cache-eviction machinery from pass 6, going one layer deeper into `TileCacheBudget` and its interaction with `TileWorkCoordinator`.

---

## Executive Summary

**One finding, high confidence, worth a full write-up on its own:** the cache-eviction path only avoids an actual deadlock today because three separate classes — `TileWorkCoordinator`, `TileCacheBudget`, and `SampleImageTile` — happen to re-enter the *same* `TileWorkCoordinator._lock` instance on the *same thread*, and `System.Threading.Lock` (the .NET 9 lock type used throughout this codebase) happens to support reentrancy. None of these three classes know about this dependency on each other. The chain is real, live-wired, and hit on every normal cache-eviction event (not an edge case) — it just doesn't deadlock *yet* because nothing in the chain has ever hopped threads. It is one small, easy-to-make refactor away from a hard deadlock.

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | `TileWorkCoordinator.Request` holds `_lock`, calls `TileCacheBudget.TryReserve` synchronously inside it (as the `tryReserve` admission check), which — on eviction — calls `SampleImageTile.ResetImageCache`, which calls back into `TileWorkCoordinator.RemoveClaimant`, which re-acquires the *same* `_lock`. Currently safe only because everything is synchronous, single-threaded, and `Lock` is reentrant. | **High** (fragility / latent deadlock risk) | 85% |

---

## 1. The chain, traced end to end

**Step 1 — outer lock acquired:**
```csharp
// TileWorkCoordinator.Request(...)
lock (_lock)
{
    ...
    if (tryReserve is not null && !tryReserve())   // ← tryReserve is _tileCacheBudget.TryReserve
    {
        ...
        return false;
    }
    ...
}
```

**Step 2 — `tryReserve()` calls into a different class's lock, on the same thread, still inside `_lock`:**
```csharp
// TileCacheBudget.TryReserve(SampleImageTile tile)
lock (_trackedTiles)
{
    ...
    while (UsedBytes > _maxBytes)
    {
        var evictedTile = /* pick a tile to evict */;
        ...
        evictedTile.ResetImageCache();   // ← still inside lock(_trackedTiles), still inside the caller's lock(_lock)
        ...
    }
    return true;
}
```

**Step 3 — `ResetImageCache` calls back into the coordinator, which re-acquires `_lock`:**
```csharp
// SampleImageTile.ResetImageCache()
if (_coordinator is not null)
{
    var oldRevision = epoch - 1;
    var claimant = GetClaimantId();
    for (var mip = 0; mip <= BackgroundTileMipPolicy.MaxMipLevel; mip++)
    {
        var oldKey = new BackgroundTileCacheKey("synthetic", Id, oldRevision, mip);
        _coordinator.RemoveClaimant(oldKey, claimant);   // ← calls back into TileWorkCoordinator
    }
}
lock (_cacheGate) { ... }
```

**Step 4 — `RemoveClaimant` re-enters the very lock acquired in Step 1:**
```csharp
// TileWorkCoordinator.RemoveClaimant(...)
lock (_lock)   // ← same field, same instance, same thread as Step 1 — this is a re-entry, not a new acquisition
{
    ...
}
```

**I confirmed this is live-wired, not theoretical:** `MainWindow.xaml.cs:361` passes `_tileCacheBudget.TryReserve` directly as the `tryReserve` delegate into the call chain that reaches `TileWorkCoordinator.Request`. Cache eviction (Step 2's `while (UsedBytes > _maxBytes)` loop) is not a rare edge case — it's the *normal*, expected behavior of a bounded cache under everyday panning/zooming through a scene larger than the cache budget. This chain runs, in full, every time the cache is full and a new tile needs admission.

**Why it doesn't deadlock today:** `System.Threading.Lock` (the type backing `_lock` here, per .NET 9's `lock` statement) explicitly supports reentrancy — a thread already holding it can re-enter without blocking. Every hop in the chain above (`Request` → `TryReserve` → `ResetImageCache` → `RemoveClaimant`) executes synchronously, inline, on one thread. So Step 4's re-acquisition of `_lock` succeeds immediately because it's the same thread that already holds it.

**Why this is fragile rather than fine:** none of `TileWorkCoordinator`, `TileCacheBudget`, or `SampleImageTile` are aware that they're participating in this chain. Nothing documents "if you call `TryReserve`, you might re-enter `TileWorkCoordinator._lock` via a completely different object's method." Three independent abstractions are silently coupled through a shared lock that none of them expose or acknowledge in their public surface. This is exactly the kind of hidden coupling that turns into a production deadlock the moment someone makes an entirely reasonable, locally-scoped change elsewhere, without knowing about this chain at all — for example:
- Moving `ResetImageCache`'s coordinator notification to `Task.Run`/a background queue (a very natural "let's not do coordinator bookkeeping on the eviction thread" refactor) would put Step 4's `lock (_lock)` on a *different* thread than the one still holding it from Step 1 — instant deadlock, since a genuinely different thread can't re-enter a `Lock` another thread holds.
- Adding any `await` anywhere between Steps 1 and 4 (e.g., making `TryReserve` or `ResetImageCache` `async` for any reason) risks resuming on a different thread post-await depending on the synchronization context, breaking the single-thread assumption the same way.
- This is precisely the *shape* of problem the recent "cache eviction deadlock" fix commit (`8127200`) already had to spend real effort on — that fix addressed a *starvation* deadlock (no evictable tiles found, admission permanently rejected), not this *lock-reentrancy* one. They're different bugs in the same neighborhood; this one just hasn't been triggered yet because nothing has changed the threading model since it was written.

**Recommendation:**
1. At minimum, document the dependency explicitly — a comment at each of the three call sites noting "this executes while `TileWorkCoordinator._lock` is held; do not make this path asynchronous or move it to another thread without redesigning the lock strategy" — so the next well-intentioned refactor doesn't reintroduce this blind.
2. Better: break the synchronous callback chain. `TileCacheBudget.TryReserve` doesn't need to trigger `ResetImageCache`'s coordinator-notification side effect *inline* during eviction — it could instead return the list of evicted tiles to its caller (`Request`, still holding the outer lock only for the admission decision itself), and have the coordinator notification happen *after* `Request`'s `lock (_lock)` block has already exited. This removes the re-entrant hop entirely rather than relying on `Lock`'s reentrancy to paper over it.
3. If reentrancy is intentionally relied upon, consider a lightweight thread-affinity assertion (e.g., an `Environment.CurrentManagedThreadId` check with a debug-only assert) at the point `RemoveClaimant` is called from `ResetImageCache`, so a future accidental cross-thread call fails loudly in testing rather than silently deadlocking in production.

This is a design/robustness finding, not a currently-manifesting bug — flagging it now, while it's cheap to fix, given how much effort the surrounding "stuck generation"/"cache eviction deadlock" bug family has already cost this project.
