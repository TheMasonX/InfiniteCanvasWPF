# InfiniteCanvasWPF — External Audit Validation & Compatibility-Plan Risk Review

**HEAD used for verification:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (the same HEAD audited across passes 5–12; unchanged as of this review).
**Inputs reviewed:** the four uploaded documents — the original external bug audit (the external bug audit request document, IDs `ICW-P0/P1/P2-###`), an independent second bug audit (the independent bug audit report document, IDs `ICW-BUG-###`), a synthesis of both (the audit synthesis document), and the proposed compatibility architecture plan document ("Changes Needed to Make ICW Suitable as a Reusable Production Viewport Engine").
**Scope of this report:** per instructions, this contains only *new* findings — places where direct verification against current HEAD confirms, corrects, sharpens, or adds evidence beyond what the four documents themselves already state, plus risk analysis of the proposed architecture plan grounded in what passes 5–12 already established about this codebase's actual behavior. It does not restate the external documents' own content, and it does not repeat findings already filed in this audit series' passes 1–12 except where new evidence changes their assessment.

All four external documents were generated from a fixed source bundle (`icw-concat.manifest.csv`, 48 files) rather than live HEAD — several of their claims were checkable directly against the current, actively-changing codebase, which this report does.

---

## Executive Summary

| # | Finding | Type | Confidence |
|---|---|---|---|
| 1 | **Independent cross-validation, high confidence:** the second external audit's `ICW-BUG-001`/`002`/`003` describe the same three coordinator defects this series found independently in pass 6 (§1 claimant tokens hardcoded to `CancellationToken.None`, §3 queued-item stranding) and this review's own re-verification (active-count released before physical work stops). Two independent audits (this series, methodical file-by-file reading; theirs, a single-pass bundle review) converging on the same root causes in the same file is meaningfully stronger evidence than either audit alone. | Corroboration | 95% |
| 2 | **Verified and sharpened:** `ICW-P0-003`/`ICW-BUG-002`'s claim that cancellation releases a coordinator concurrency slot before the physical `Task.Run` delegate actually stops is confirmed directly in code (`CancelWorkItem`, `TileWorkCoordinator.cs:381-386`) — `_activeCount` is decremented immediately upon calling `item.CancelWork()`, with no wait for the factory to observe the token. This means the coordinator's `DefaultMaxConcurrency = 4` is not a hard ceiling on physically-concurrent work during a cancellation burst — and this directly compounds pass 12's §2 finding (concurrent GDI+ `Bitmap`/`Graphics`/`FillEllipse` usage in tile generation): the realistic worst case for simultaneous GDI+ operations is not "up to 4," it's unbounded by how many stale, still-running factory delegates a fast-scroll cancellation storm can accumulate before they individually finish. | Confirmed + escalates pass 12 | 90% |
| 3 | **Confirmed, and found to be worse than described:** the second audit's `ICW-BUG-005` ("pixelometer readout initiates tile generation") is accurate — `TryReadPixelValue` calls `tile.TryGetPixelsNonBlocking(mipLevel, out ..., out ...)` using the overload that defaults `tryReserveCacheEntry` to `null`. Tracing what `null` does at the coordinator (`Request`'s admission check is skipped entirely when `tryReserve` is `null`) shows this isn't just "hover starts generation" — **hover-triggered tile generation is completely invisible to `TileCacheBudget`'s accounting**: it's never added to `_trackedTiles`, never counted in `UsedBytes`, and therefore can never be evicted by the normal eviction path either. A tile generated this way holds real heap memory for the rest of the scene's lifetime, unaccounted and unrecoverable by the budget system. | New, sharper than source claim | 88% |
| 4 | **Pushback, with a more precise replacement finding:** `ICW-P0-004`/`ICW-BUG-011/012` ("cache reservation release is not an ownership contract — `ReleaseReservation` only increments a counter") is technically accurate about that one method, but the conclusion overstates its practical impact. `TileCacheBudget.TryReserve`/`Release` (in `SampleImageTile.cs`) are keyed by `tile.Id`, not by coordinator work-item key, and are idempotent per tile — a canceled or failed *generation attempt* doesn't need its own release path, because the tile's budget reservation legitimately persists until the tile itself is evicted or the scene is torn down, both of which already correctly adjust `UsedBytes` through a different, tile-scoped path. The coordinator's `ReleaseReservation` being a no-op counter looks like vestigial/diagnostic-only code given this, not an active leak, in every case *reachable today*. However, direct tracing of `PixelCost` (`_pixelCost = checked(pixelWidth * pixelHeight)`, set once at construction, never revised) found a real, separate, more precise gap: **it accounts for mip-0 size only** — a tile with several generated mip levels resident consumes up to ~33% more actual heap than `TileCacheBudget` believes it's tracking (geometric series of halved-dimension mips converges to 4/3× the base cost). | Corrects source claim + new finding | 75% |
| 5 | **Confirmed plausible, and genuinely new relative to this series' own 12 passes:** `ICW-P0-002`/`ICW-BUG-008` (front/back buffer `InteropBitmap` reuse racing WPF's compositor) describes a real mechanism this audit series had not examined — `PublishFrame` (`MainWindow.xaml.cs:465-483`) recycles the just-retired front buffer as the next back buffer immediately after handing the new frame to `FramePresenter.Child`, with no synchronization against WPF's asynchronous render-thread compositor potentially still reading the old buffer's memory-mapped section. This is a distinct actor from every buffer/dispose race this series found in passes 5–9 (which were all about the *defect template pool*, not the *frame surface* itself) — worth tracking as its own item, not folded into those. | New territory, source claim validated | 70% |

---

## 1. Cross-validation between this audit series and the second external audit

**Confidence: 95%**

The second uploaded document's `ICW-BUG-001` ("Frame-Level Claimant Cancellation Is Disabled"), `ICW-BUG-002` ("Physical Concurrency Limit Can Be Exceeded After Cancellation"), and `ICW-BUG-003` ("Running Cancellation Can Strand Queued Work") describe — independently, from a static bundle review — the same three defects this series found through direct, methodical reading in pass 6 (§1, §3) and this review's own re-verification (§2 below). Neither audit had access to the other's findings at the time of writing (this series' passes 5–12 predate this upload; the external documents were produced from a separate source bundle). Two independently-produced audits landing on the same specific mechanisms in the same 600-line file is a meaningfully stronger signal than either audit alone — it's worth explicitly noting in whatever tracking these findings get folded into (this series has been recommending fixing pass 6 §1/§2/§3 before `ICW-143` starts; this cross-validation is a good reason to treat that as higher-confidence than a single-audit finding would otherwise warrant).

---

## 2. `_activeCount` decrements before physical work stops — verified, and it escalates the GDI+ concurrency finding

**Confidence: 90%**

```csharp
// TileWorkCoordinator.cs:381-386 (CancelWorkItem, in-flight branch)
if (wasRunning)
{
    // In-flight work: signal cancellation via the work token source.
    item.CancelWork();
    _activeCount = Math.Max(0, _activeCount - 1);
}
```
`item.CancelWork()` only requests cancellation of `_workCts` — it does not, and cannot, force the already-running `Task.Run` delegate to stop instantaneously. The delegate stops only when it next checks the token (and per pass 6's own finding, the mip-level factory only checks cancellation *after* the expensive work completes, not during it). `_activeCount` is decremented on the same line regardless, immediately making a "slot" available to `DrainQueue` for a new admission.

This directly changes the risk assessment of pass 12's §2 finding (concurrent GDI+ `Bitmap`/`Graphics`/`FillEllipse`/`LockBits` usage inside tile generation, gated at "up to `DefaultMaxConcurrency = 4`"). That gate is not real during a cancellation burst: a canceled item's factory can still be mid-`ApplyDetailsWithGdiPlus` on its own thread when a *new* item is admitted into the "freed" slot, and that pattern can repeat — the practical ceiling on simultaneous GDI+ work is bounded only by how many stale delegates accumulate before they individually finish, not by `DefaultMaxConcurrency`. Given the stability sprint's own motivating scenario was exactly this kind of cancellation storm (fast scrolling), this raises pass 12 §2 from "worth a stress test" to "worth prioritizing that stress test before the next round of concurrency tuning" — the assumption that concurrency is capped at 4 no longer holds even in the currently-shipped code, let alone under increased load.

**Recommendation:** unchanged in kind from the external audits' own (`_activeCount` should represent physical execution, decremented in the worker's terminal `finally`, not at cancellation-request time) — noting it here specifically to connect it to the GDI+ exposure this audit series already found, since neither external document mentions GDI+ or `ApplyDetailsWithGdiPlus` at all.

---

## 3. Pixelometer-triggered generation bypasses cache budget accounting entirely

**Confidence: 88%**

```csharp
// MainWindow.xaml.cs:1536 (TryReadPixelValue, called from UpdatePixelometer on every mouse move)
var hasSourcePixels = tile.TryGetPixelsNonBlocking(
    mipLevel, out sourcePixels, out var residentMipLevel);
```
This calls the overload of `TryGetPixelsNonBlocking` that defaults `tryReserveCacheEntry` to `null` (confirmed — `TryReadPixelValue` never supplies one). Tracing that `null` through:
```csharp
// SampleImageTile.cs, EnsurePixelsGenerationStarted
var admitted = _coordinator.Request(
    key, ..., tryReserve: tryReserveCacheEntry);   // null, passed straight through
```
```csharp
// TileWorkCoordinator.cs, Request
if (tryReserve is not null && !tryReserve()) { return false; }   // skipped entirely when null
```
Work is admitted unconditionally. Since `tryReserve` is what would normally call `TileCacheBudget.TryReserve(tile)` — the only place that adds a tile to `_trackedTiles` and counts its `PixelCost` toward `UsedBytes` — a tile generated purely because the mouse hovered over it never enters that accounting at all. Two consequences, both confirmed by inspection of `TileCacheBudget` (pass-review, this session): it doesn't count against the budget, **and** it can never be evicted by the normal `TryReserve`-triggered eviction loop, since that loop only ever considers `_trackedTiles.Values`. The generated pixel buffer is real, resident, heap-allocated memory that exists completely outside the system meant to govern memory — not merely "hover has a side effect" (the second audit's framing) but "hover can grow untracked, unrecoverable memory usage for the rest of the scene's lifetime."

**Recommendation:** beyond the second audit's own recommendation (readout should consume a published-frame snapshot rather than touching tile acquisition APIs at all), if any interim fix is needed before that larger change lands, the cheapest containment is simply passing the *same* `tryReserveCacheEntry` (`_tileCacheBudget.TryReserve`) into `TryReadPixelValue`'s call, so hover-triggered generation at least participates in the same accounting as render-triggered generation, even before the deeper "readout shouldn't trigger acquisition at all" fix is built.

---

## 4. `ReleaseReservation`'s "no-op counter" is likely benign today — the real gap is `PixelCost` undercounting mip memory

**Confidence: 75%**

`TileWorkCoordinator.ReleaseReservation` (`TileWorkCoordinator.cs:431-434`) genuinely is just `Interlocked.Increment(ref _reservationReleases)` — the external audits' description of the code is accurate. Where this review parts ways is on impact: `TileCacheBudget.TryReserve`/`Release` (`SampleImageTile.cs:777-840`) don't take their cue from the coordinator's per-work-item release at all. They're keyed by `tile.Id`, and `TryReserve` is explicitly idempotent (`if (_trackedTiles.ContainsKey(tile.Id)) return true;`) — a tile's budget reservation is a property of the *tile* persisting for as long as it's tracked, not of any individual generation attempt. A canceled or failed attempt doesn't strand budget, because nothing about a cancelled attempt implies the tile itself should stop being tracked — it'll likely be requested again soon, and re-reserving on every transient cancellation would be wasted churn, not a fix. The tile's reservation is correctly released exactly when it should be: via `TileCacheBudget`'s own eviction logic (direct `Interlocked.Add(ref _usedBytes, -evictedTile.PixelCost)`, `SampleImageTile.cs:816`) or via `MainWindow`'s explicit `.Release(tile)` calls during scene teardown. In every reachable path this review traced, budget accounting is self-consistent without the coordinator's counter doing anything.

That doesn't mean the underlying instinct (accounting is fragile here) is wrong — it correctly is, just via a different, more precise mechanism than either external audit identified:
```csharp
// SampleImageTile.cs:67
_pixelCost = checked(pixelWidth * pixelHeight);   // set once, at construction, from mip-0 dimensions only
```
`PixelCost` never changes after construction. A tile that has generated mip-0 plus several progressively-smaller mip levels (each cached separately, per pass 6/12's tracing of `EnsureMipPixelsGenerationStarted`) holds more actual heap memory than `PixelCost` represents — a geometric series of quarter-sized mips converges to 4/3× the base cost, so `TileCacheBudget.UsedBytes` can undercount real memory pressure by up to ~33% once a scene's tiles have accumulated several mip levels, which is exactly the steady-state a long viewing session would reach.

**Recommendation:** if the coordinator's `ReleaseReservation`/reservation-counter design is revisited (as both external documents recommend, via an `ICacheReservation : IDisposable` contract), that's a reasonable direction for making the *ownership model* explicit even if it isn't fixing an active leak today — but whatever replaces it should also fix `PixelCost` to reflect actual resident bytes across all cached mip levels for a tile, not just mip-0's dimensions, or the new, more rigorous-looking abstraction will faithfully carry forward the same undercount in a fancier form.

---

## 5. Front/back buffer `InteropBitmap` reuse — validated as new, distinct territory

**Confidence: 70%**

```csharp
// MainWindow.xaml.cs:465-483 (PublishFrame)
FramePresenter.Child = frameVisual;
var previousFront = _frontBitmapFactory;
_frontBitmapFactory = renderedBuffer;
_backBitmapFactory = null;
if (previousFront is not null && previousFront.Width == renderedBuffer.Width && previousFront.Height == renderedBuffer.Height)
{
    _backBitmapFactory = previousFront;   // recycled for the *next* frame's off-screen composition
}
```
The just-retired front buffer becomes the next back buffer immediately, with nothing here waiting for WPF's compositor (which runs on its own render thread, asynchronously from this UI-thread assignment) to confirm it's actually done reading the old `InteropBitmap`'s backing memory-mapped section before that same section gets reused and overwritten for the next frame. This is a plausible race and a real risk on the same general theme as this series' passes 5–9 defect-pool-disposal findings, but it's a genuinely different actor (the frame surface itself, not the shared defect-template bitmap pool) that this audit series had not examined — confirmed real and worth tracking as `ICW-P0-002`/`ICW-BUG-008` describe, not folded into the existing defect-pool tickets.

---

## 6. Architecture Plan Risk Assessment — sequencing and blind spots in the compatibility proposal

The proposed "reusable production viewport engine" restructuring (5 new assemblies, ~40 new types: `SceneSnapshot`, `RenderFrameSnapshot`, `PublishedViewportFrame`, source adapters, a declarative layer graph, lease-based resource governance) is a coherent, well-reasoned direction *in the abstract* — its own "Definition of Done" and phase breakdown are internally consistent. Two concrete risks, grounded in what this audit series has directly verified about the current codebase's actual state, rather than the plan's own text:

**Sequencing risk:** the plan's own Phase 6 ("Cancellation and Scheduler Hardening") is where `_activeCount`/claimant-token/queued-work-stranding fixes (§1–2 above) are scheduled — but Phases 2–5 (Snapshot/Publication Core, Source Adapter Layer, Layer Graph, Resource Ownership and Surface Leases) all build new abstractions *on top of* the coordinator as it exists today. A `PublishedSurfaceLease` or `IMemoryGovernor` built in Phase 5 sitting on top of a coordinator whose `_activeCount` doesn't yet reflect physical execution (not fixed until Phase 6) risks the new lease/governor layer inheriting the same "believes a slot is free when it isn't" problem one abstraction layer up, rather than being insulated from it. Given both external audits and this series independently converged on these three coordinator defects as high-confidence and cheap to fix in isolation, moving the specific fixes from §1–2 above earlier — a "Phase 0" or folded into Phase 1 — would give every later phase a foundation whose concurrency accounting is actually trustworthy, rather than deferring that to Phase 6 after four phases of new code have already been written against the old, known-inaccurate assumptions.

**Blind spot — settings/ViewModel layer:** the plan is scoped entirely to rendering/acquisition/resource/layer architecture and doesn't mention the settings-persistence or ViewModel-lifecycle layer at all. This audit series' pass 5 §1 (background-noise settings silently reset on every `RegenerateSceneAsync` because `InitializeSpatialState` reconstructs `MainViewModel`) and pass 9 §1 (`CanvasUserSettings.IsValid` missing an upper bound on `ObjectsPerTile`, letting a bad settings file round-trip forever) both live in exactly the code the plan's own Phase 1 ("Move MainWindow responsibilities into services") will touch — extracting `MainWindow`'s settings/ViewModel-construction responsibility into a service is the natural point to also fix both bugs, since the refactor will be rewriting that exact code path anyway. Neither bug is mentioned in the plan's "Definition of Done" checklist, meaning as currently scoped, a team could complete every phase of the compatibility work and still ship with both bugs intact, since nothing in the plan's acceptance criteria would catch them.

**Recommendation:** add both to Phase 1's scope explicitly (they're cheap, already-diagnosed, and directly overlap the code that phase already touches), and consider moving the specific, already-diagnosed portions of Phase 6 (the three coordinator defects in §1 above) earlier in the sequence — not the full cancellation/scheduler hardening effort, just the parts that are already fully understood and would otherwise be built around rather than fixed by four intervening phases.

## Assumptions & Open Questions

- §4's 75% confidence reflects genuine uncertainty about whether the coordinator's reservation-release design was *intended* to be vestigial (this review's read) versus an incomplete implementation of a feature that was meant to do more — that's a question about original intent this static review can't settle definitively, only argue from observed behavior.
- §5's 70% confidence reflects the same category of uncertainty as pass 12 §2: the mechanism is confirmed by reading, but whether WPF's compositor actually creates a practically-hittable race in this specific usage pattern is an empirical question, not one this review can close through static analysis alone.
- As with all prior passes in this series, this review is static source analysis only — no build or test execution was performed.
