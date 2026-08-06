# InfiniteCanvasWPF — Audit Delta #8 (post Wave G: canvas contract hardening)

**Scope of this pass:** Re-cloned to `origin/main` (3 new commits since Delta #7/#6: a 22-audit synthesis reconciliation, a canvas data-source injection commit, and **Wave G**, "harden canvas contracts and fix coordinator cancel window"). Verified Wave G's three `TileWorkCoordinator.cs` fixes (`ICW-320` F-006/F-007/F-014) against the exact mechanism traced in Delta #6 — confirmed resolved, details below. Then read the new `ICanvasSceneSource.cs`/`CanvasFrame.cs` contract boundary and its only construction call site in full, which is where this pass's new finding is.

---

## Confirmed resolved (no longer worth tracking) — `ICW-320` correctly fixes the Delta #6 coalesce-onto-terminal-item bug

Wave G changes `TileWorkCoordinator.Request` to treat an existing item as "not present" (and admit fresh work instead of coalescing) when its `State` is `Canceled`, `Completed`, or `Failed`, and pairs this with a `ReferenceEquals` check in `HandleWorkStopped` so the old, still-stopping worker can't clobber the newer item admitted for the same key while it winds down. Read both methods in full at current HEAD: this closes the exact gap Delta #6 traced (claimant coalescing onto a terminal item and inheriting its dead `CancellationTokenRegistration`) by removing the coalescing path entirely for terminal items, rather than by refreshing the registration as I'd suggested — a cleaner fix, since it also sidesteps the question of what other state on a terminal item is safe to reuse. `ICW-320`'s companion `F-014` fix (add the claimant to `_claimants` before registering the token callback, not after) closes a related but distinct synchronous-fire race I hadn't traced. All three read correctly; no residual issue found in this area this pass.

---

## New Finding — `CanvasFrame.Revision`, documented and validated as the frame boundary's stale-frame guard, is never assigned a real value at its only construction site and never consumed anywhere — the "revision identity" hardening this shipped under is marked Done but has no actual effect — **High confidence, verified both ends of the pipe**

`CanvasFrame` (the new host/canvas boundary type introduced this session, `ICW-315`/`ICW-316A`) carries a `Revision` property with this doc comment:

```csharp
/// <summary>Stale-frame revision identity (ICW-316A).</summary>
public int Revision { get; }
```

and a constructor parameter `int revision = 0`. `ICW-316A`'s own acceptance criteria (ticket read in full, **status: Done**) lists *"`CanvasFrame` construction validates item counts and raster dimensions"* under the heading *"Harden `CanvasFrame`: immutable by contract, count-consistency validation, raster-dimension validation against `ImageSource` metadata, **revision identity**."* Revision identity is explicitly called out as one of four things this hardening pass was supposed to deliver.

Checked both ends:

- **The only place `CanvasFrame` is ever constructed** (`MainWindow.PublishFrame`, `grep`-confirmed as the sole call site in `src/`) never passes `revision:` — every `CanvasFrame` published by the app for its entire lifetime has `Revision == 0`. There is no incrementing counter, camera epoch, or request version fed into it anywhere nearby (`_lastPublishedCamera`/`_frameBufferPool` are set in the same method and neither is a revision source).
- **`Revision` has zero consumers.** `grep -rn "\.Revision\b"` across `src/`, excluding `CanvasFrame.cs` itself, returns nothing. `CanvasControl.PublishFrame(CanvasFrame frame)` — the one place a consumer could plausibly compare `frame.Revision` against a previously-seen value to discard a stale/out-of-order frame — doesn't reference it either.

So the property is validated at construction (can't be negative-adjacent to the other count checks, though there's no explicit range check on `revision` itself either — any `int` including negative values is accepted), documented as the mechanism for detecting stale frames, and listed as delivered in a Done ticket — but it currently does nothing: always zero in, never read out. This is functionally indistinguishable from not having the field at all, except that it *reads* as though staleness detection exists at this boundary, which could lead a future contributor to trust a protection that isn't there (the same "shipped the shape, not the behavior" pattern flagged for `ICW-064`'s eviction-preference claim in Delta #3, now recurring at a different layer).

This is worth contrasting with the codebase's *other* stale-frame guard, `RenderRequestTracker` (`BeginRequest`/`IsCurrent`/`Advance`, verified working correctly in earlier audit passes) — that one actually threads a live version number through and gates on it. `CanvasFrame.Revision` looks like it was scaffolded to do the same thing at the new host/canvas boundary but the wiring was never finished, and nothing in the test suite caught it because there's nothing behaviorally different between "always 0" and "correctly incrementing" until a second consumer actually reads the value.

**Recommendation:** Either wire it up for real — thread `RenderRequestTracker`'s existing version counter (or a new monotonic counter local to `PublishFrame`) into the `revision:` argument, and add the one missing half: a check in `CanvasControl.PublishFrame` that ignores/discards a frame whose `Revision` is not greater than the last one displayed — or, if there's no current consumer need for it, remove the parameter and property rather than leave a documented guarantee that doesn't hold. Either way, `ICW-316A`'s "Done" status should not be read as "revision identity is enforced" until one of those happens; worth a short follow-up ticket (`ICW-316A-FOLLOWUP` or similar) rather than silently letting this drift the way `ICW-064`'s eviction claim did.

---

## Minor note — `TileCacheBudget`'s eviction path no longer bumps the tile's generation epoch, so the "epoch guards discard the stale result" comment in the `ICW-320` fix doesn't quite describe what discards the loser in the eviction case specifically

Traced for completeness, not filed as its own defect: `SampleImageTile.EvictCacheEntry` (new method backing eviction, replacing the old `ResetImageCache()`-based path audited in Delta #3) resets `_generationQueued` directly and clears `_pixels`/`_mipPixels` for the evicted key, but does **not** bump `_generationEpoch`. So when eviction triggers a fresh re-request for the same key, the old (evicted-but-still-running) generation and the new (freshly admitted) generation for that key carry the *same* epoch — the actual "first writer wins, second is dropped" behavior comes from `OnCoordinatorPixelsGenerated`'s separate `_pixels is null` check, not from the epoch comparison the `ICW-320`/Wave G commit comments attribute it to. The end result is still correct (no corruption, no double-publish) — this is a documentation-precision note, not a bug, recorded so a future reader doesn't go looking for an epoch bump that isn't part of this particular path.

---

*This delta report should be read alongside `icw-wave-e-audit.md` and `icw-wave-e-audit-delta-2.md` through `-7.md` (the latter already present in-repo at `docs/audits/icw-wave-e-audit-delta-7.md`). It does not repeat their content. Still unexplored: `CanvasControl.xaml.cs`'s wheel-zoom/pan interaction code and the method-based API surface from `ICW-319`, `CanvasViewModel.cs`'s new batched `ApplyFrame`/private-setter invariants, and the `tests/` directory — worth a follow-up pass.*
