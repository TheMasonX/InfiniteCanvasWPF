# InfiniteCanvasWPF — Audit Pass 7 (Same HEAD, Deeper Static Dive)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since pass 6 — verified via commit feed diff before writing this).
**Scope this pass:** full reads of `RenderRequestTracker.cs`, `CoalescingAsyncAction.cs`, `BackgroundTileContracts.cs`, `DefectOverlaySampler.cs`, and a call-graph trace of `RenderFrameAsync`'s cancellation-token plumbing — none of these got a full dedicated read in passes 5–6, which were focused on the coordinator sprint. No new commits landed during this pass (checked before and after).

This pass produced one correction to pass 5/6's framing (§1, strengthens the fix recommendation), one new architectural finding (§2), and one confirmed-clean result worth recording so it doesn't get re-litigated later (§3).

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **The exact fix pattern for the pass-5/6 defect-pool dispose race already exists in the codebase, unused at the one call site that needs it.** `OnClosed` correctly does `await _renderAction.DisposeAsync()` before touching shared render state (bitmap factories, tile coordinator). `RegenerateSceneAsync` does not do the equivalent before calling `DisposeDefectTemplatePools`. This isn't a "needs new design" problem — it's "the pattern is one call away." One caveat that matters for *how* to apply it: cancelling the render's token does **not** stop an in-flight render early (see below) — a fix must *await* completion, not just request cancellation. | High (refines pass 5§2/pass 6) | 90% |
| 2 | **`BackgroundTileContracts.cs` mixes a live, heavily-used policy/key API with a fully-built, fully-unused source-abstraction API in one file.** `BackgroundTileCacheKey` and `BackgroundTileMipPolicy` (`GetDimensions`/`SelectMipLevel`/`MaxMipLevel`) are used throughout `SampleImageTile.cs`, `SampleImageGenerator.cs`, and `MainWindow.xaml.cs` — genuinely live, correctly shared, no duplication found. But `IBackgroundTileSource`, `BackgroundTileDescriptor`, `BackgroundTileRequest`, `BackgroundTilePayload`, and `BackgroundTileReadoutInfo` have **zero references outside this file and one test** (`SampleImageGeneratorTests.cs`, which only exercises `BackgroundTilePayload`'s own constructor guard clauses in isolation). `IBackgroundTileSource` has no implementers anywhere in the repo. This looks like scaffolding for a not-yet-started "real background image source" feature, sitting live in the main assembly, indistinguishable at a glance from the types around it that *are* load-bearing. | Medium | 85% |
| 3 | **Confirmed clean:** `CoalescingAsyncAction`'s coalescing logic (`RequestAsync`/`ProcessAsync`) was read in full and traced for the classic "lost wakeup" race (a request arriving in the gap between a loop's exit-check and its actual exit). No such gap exists — the `_requested` flag check and the processing-task lifecycle are both fully serialized under the same `_gate` lock on both sides. Recording this so future passes don't need to re-derive it. | — (informational) | 90% |

---

## 1. [HIGH, refines pass 5§2 / pass 6] The fix pattern for the defect-pool dispose race already exists — it's just not applied at the right call site

**Confidence: 90%**

`MainWindow.xaml.cs`'s `OnClosed`:
```csharp
private async void OnClosed(object? sender, EventArgs e)
{
    SaveSettings();
    _resizeTimer.Stop();
    _anchorPanTimer.Stop();
    UnsubscribeTileGenerationEvents(_tiles);
    _lifetime.Cancel();

    await _renderAction.DisposeAsync();     // <-- waits for any in-flight render to finish first
    FramePresenter.Child = null;
    _frontBitmapFactory?.Dispose();
    _backBitmapFactory?.Dispose();
    _tileCoordinator.CancelAll();
    _tileCoordinator.Dispose();
    _generationGate.Dispose();
    _lifetime.Dispose();
}
```
`CoalescingAsyncAction.DisposeAsync()` (`CoalescingAsyncAction.cs:38-67`) captures the current `_processingTask` under its lock, then `await`s it before returning — so by the time `OnClosed` reaches `_frontBitmapFactory?.Dispose()`, any render that was mid-flight is guaranteed finished. This is exactly the property pass 5§2 and pass 6's confirmation both said was missing at the `RegenerateSceneAsync` call site, and the codebase already demonstrates the author knows the pattern — it's just applied at window-close but not at scene-regenerate.

**One important wrinkle for whoever implements this fix:** cancelling `_renderAction`'s token does not stop an in-flight render early. `RenderFrameAsync`'s actual pixel work runs as:
```csharp
var frame = await Task.Run(() => { /* ... factory.GenerateFrozenBitmap(...) incl. DrawDefectPatch/LockBits ... */ }, cancellationToken);
```
A `CancellationToken` passed to `Task.Run` only prevents the delegate from *starting* if it's still queued; once `GenerateFrozenBitmap` has begun executing, nothing inside that lambda checks `cancellationToken` again, so cancellation has no effect on an already-running render — it will run to completion regardless. That's fine for `OnClosed`, which just awaits completion rather than expecting early termination. It means a `RegenerateSceneAsync` fix built the same way (`await`, don't just cancel) would behave the same — reliably wait rather than reliably cancel-and-stop — which is sufficient to close this hazard, but worth calling out explicitly since "just cancel it" is the more tempting-looking one-line fix and wouldn't actually work.

**Recommendation:** expose a non-destructive `Task WaitForIdleAsync()` on `CoalescingAsyncAction` (capture `_processingTask` under `_gate` the same way `DisposeAsync` does, await it, but skip the `_disposed = true` / `_lifetime.Cancel()` steps so the action remains usable afterward). Call it from `RegenerateSceneAsync` immediately before `SampleImageTile.DisposeDefectTemplatePools(_tiles)`. This is a small, additive change that reuses code already proven correct at the `OnClosed` site, rather than inventing new synchronization.

---

## 2. [MEDIUM] Unused source-abstraction types live alongside load-bearing ones in `BackgroundTileContracts.cs`

**Confidence: 85%**

Reference counts (production `src/` + `tests/`, this file excluded):

| Type | Used outside this file? |
|---|---|
| `BackgroundTileCacheKey` | **Yes** — `SampleImageTile.cs`, `TileWorkCoordinator.cs` (as the generic `TKey`), constructed directly at multiple sites |
| `BackgroundTileMipPolicy` (`GetDimensions`/`SelectMipLevel`/`MaxMipLevel`) | **Yes** — `SampleImageTile.cs`, `SampleImageGenerator.cs`, `MainWindow.xaml.cs`; no duplicated halving/log2 arithmetic found elsewhere, so this part is a genuine single source of truth today |
| `BackgroundTileDescriptor` | No — only constructed inside two unit tests in `SampleImageGeneratorTests.cs` |
| `BackgroundTileRequest` | No — same two tests only |
| `BackgroundTilePayload` | No — same two tests, exercising only its constructor's length-validation guard clause |
| `BackgroundTileReadoutInfo` | No references found anywhere, including tests |
| `IBackgroundTileSource` | No references found anywhere; **zero implementing classes in the repo** |

The naming and shape of the unused half (`Descriptor` + `Request` + `Payload` + a `ResolveAsync` interface) reads like scaffolding for a future "real" background-image source (loading actual images rather than only synthetic-generated ones) — plausible given the project is explicitly a synthetic-defect-image sandbox today. That's a reasonable thing to have sketched out early. The risk isn't that it exists; it's that it's indistinguishable from live code at a glance (same file, same visibility, same namespace as `BackgroundTileCacheKey`/`BackgroundTileMipPolicy`, which *are* load-bearing), so a future contributor extending tile-source behavior could easily either (a) build on top of the unused interface assuming it's already wired somewhere, or (b) not notice it exists and duplicate similar concepts elsewhere.

**Recommendation:** either wire `IBackgroundTileSource` in now if the feature it anticipates is imminent, or move the unused types into a clearly-labeled `Planned/` or `Experimental/` namespace (or a `// Not yet wired — see ICW-XXX` doc comment block) so the split between "live contract" and "future contract" is visible without having to grep for references the way this audit did. If there's no near-term plan to use it, consider deleting it — it's fully covered by two tests that would need to move or go with it, so the removal is low-risk either way.

---

## 3. [Confirmed clean] `CoalescingAsyncAction` coalescing logic has no lost-wakeup gap

**Confidence: 90%**

Traced the specific race this class of coalescing primitive commonly has: a new request arriving in the window between the processing loop deciding to exit and actually exiting, which would leave the new request's data un-serviced with no one scheduled to pick it up. In this implementation:
- `RequestAsync()` sets `_requested = true` and decides whether to start a new `ProcessAsync()` loop, both under `lock (_gate)`.
- `ProcessAsync()`'s loop-continuation check (`if (!_requested || _disposed) return;` then `_requested = false;`) is also under the *same* `lock (_gate)`.

Because both sides of the handoff share one lock and there's no `await` between the check and the flag mutation on either side, there's no window where a caller could observe "a loop is running, I don't need to start one" while the loop has already committed to exiting. Recording this as confirmed-correct so it isn't re-flagged as a suspect in a future pass without cause — the design is sound; the actual gaps in this codebase are at the call sites that use it (§1), not in the primitive itself.

---

## Suggested Priority

1. **§1** — cheapest of everything raised across passes 5–7 for this specific hazard, and the reference implementation is sitting nine lines away in the same file. Bundle with pass 6's §1/§2 (claimant-token wiring) since both are "fix the integration, not the primitive" items in the same file.
2. **§2** — no urgency (nothing is broken), but cheap to resolve one way or the other (wire it, relabel it, or delete it) before it accumulates more surface area or a future contributor builds on the unused half by mistake.

## Assumptions & Open Questions

- §1's recommendation (`WaitForIdleAsync`) is a suggested shape, not a mandate — any mechanism that guarantees "no render is mid-`Task.Run` when `DisposeDefectTemplatePools` runs" satisfies the finding; extracting from the proven `DisposeAsync` code path is simply the lowest-risk route since it reuses logic already exercised by the existing `OnClosed` path.
- §2's "scaffolding for a future real image source" interpretation is inferred from naming/shape, not confirmed by any ticket, ADR, or comment found in the repo. If there is a ticket describing this intent that wasn't surfaced by this pass's searches, the recommendation still holds (label it either way) but the "why it exists" framing may need correction.
- As with all prior passes, this is a static, source-only review — no build or test execution was performed (no Windows execution environment available).
