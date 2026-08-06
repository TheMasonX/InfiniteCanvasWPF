# InfiniteCanvasWPF — Audit Pass 10 (Same HEAD): ICW-078 Regression Still Blocks an Upcoming Dependent Ticket

**HEAD:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` — still unchanged, reconfirmed via commit feed. This pass swept `SampleImageGenerator.ApplyMipDetails` (circle/label overlay rendering, not yet reviewed) and `TileWorkCoordinator`'s queue ordering (not yet checked for priority/starvation behavior), then cross-referenced what I found against the newer `ICW-14x` scheduling tickets.

---

## Summary

**One actionable finding, not a new bug but a live risk to upcoming work:** `ICW-143` ("Add viewport culling and relevance-priority tile scheduling," `To Do`, P1) explicitly lists `ICW-078` (stale-frame epoch guarding) as a hard dependency (`dependsOn: ICW-078`). I re-confirmed at this exact HEAD that `ICW-078`'s fix is **still reverted** — `RenderRequestTracker` has zero references anywhere in `MainWindow.xaml.cs` — the same regression I first found in pass 4 and re-verified in pass 6, now several commits later, still unnoticed. `task-tracker.md` still lists it as `Done`. Whoever picks up `ICW-143` next will read "dependency: Done" and reasonably assume the stale-frame guard it needs is already in place; it isn't.

**Everything else checked this pass came back clean or already-tracked** (details below, kept brief since there's nothing actionable in them beyond "verified, no issue").

---

## 1. [Action item] `ICW-143`'s stated dependency `ICW-078` is not actually satisfied
**Confidence: 95%**

`docs/tasks/tickets/ICW-143-viewport-tile-culling-and-priority.md`:
```yaml
dependsOn:
  - ICW-142
  - ICW-078
```
and its acceptance criteria include: *"A rapid sequence of viewport updates cannot publish a frame or completion callback for an obsolete request epoch"* — this is precisely what the `RenderRequestTracker` epoch guard (`ICW-078`) was built to provide. `docs/tasks/task-tracker.md` currently reads:
```
| ICW-078 | Done | Bug | Guard render and regeneration paths against stale frame publication | Render request epochs now suppress stale frame publication after newer viewport or scene state changes. |
```
But `grep -c "RenderRequestTracker" src/InfiniteCanvas.App/MainWindow.xaml.cs` still returns `0` at this exact HEAD — the field, its `BeginRequest()`/`IsCurrent()` calls, and the `.Advance()` hook are still absent, exactly as found in pass 4 (reverted by `9247bff`) and re-confirmed in pass 6 and pass 7. This has now persisted across the entire mip-pyramid, scrollbar-restoration, noise-generation, and tile-work-coordinator sprints without being caught.

**Why this matters now specifically:** `ICW-143` is `P1`, `To Do`, and is about to build viewport-priority tile scheduling on top of an epoch-guard foundation the tracker says exists. If it's implemented assuming `ICW-078` is handled, `ICW-143`'s own acceptance criterion about obsolete-epoch frames/callbacks will likely fail in testing for a reason that looks like a new bug in the `ICW-143` work, when the actual root cause is the older, silently-reverted fix it depends on. Worth re-fixing `ICW-078` (a small, mechanical re-application — the removed code is fully preserved in commit `3dc49da`'s diff) *before* starting `ICW-143`, rather than after.

**Recommendation:** Re-apply the `ICW-078` fix, correct `task-tracker.md`'s status back to reflect reality, and only then start `ICW-143`. Given this is the second time this same regression has been flagged (pass 4, pass 6, now pass 10) without being addressed, it may be worth a standing regression test (as I suggested in pass 4) rather than relying on audit passes to keep re-catching it.

---

## 2. Swept and cleared (no new finding)

- **`SampleImageGenerator.ApplyMipDetails`** (circle/label overlay drawn per mip level): reuses the same per-tile seed across all mip levels of a given tile (correct — this is what keeps detail circles in consistent relative positions across mip levels, matching what a real mipmap should show) and scales circle radius by `Math.Min(scaleX, scaleY)` to keep circles circular. Checked whether `scaleX`/`scaleY` could diverge enough to matter (`BackgroundTileMipPolicy.GetDimensions` ceiling-divides both dimensions by the same power-of-two divisor) — for the app's actual default tile dimensions (8192×2048, both clean powers of two), this divides evenly at every mip level with zero rounding divergence. Not a live issue; would only matter for non-power-of-two tile dimensions, which aren't used today.
- **`TileWorkCoordinator`'s `_queue` is a plain FIFO `Queue<BackgroundTileCacheKey>`** with no priority/recency ordering — a currently-visible tile's generation request can sit behind stale, no-longer-relevant requests enqueued moments earlier during a fast pan. This is a real gap, but it's already fully and accurately tracked: `ICW-143` (`To Do`) exists specifically to add "relevance-priority tile scheduling," and its own text already describes this exact problem ("Current visible requests outrank stale or prefetch requests after a rapid pan or zoom" is listed as an acceptance criterion for work not yet started). Not re-reporting as new since the ticket already owns it accurately — surfaced here only because it's what led me to notice finding #1's dependency gap.

