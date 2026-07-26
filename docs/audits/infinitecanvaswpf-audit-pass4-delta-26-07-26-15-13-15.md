# InfiniteCanvasWPF — Audit Pass 4 (Delta Only)

**HEAD audited:** `d74dde2655b9cee1f6502e0200fca022ce1435dd` ("Restore viewport scrollbars and optimize direct mip generation")
**Baseline for this pass:** `62d1ce6` (my prior pass-3 report) plus 17 existing internal audit docs in `docs/audits/`, all read in full before writing anything below.
**14 new commits since pass 3**, spanning: Sonar-ticket registration → tile-cache/coalescing hardening → scrollbar restoration → a human commit ("cleaned up the mainwindow xaml") → the render-epoch hardening commit → **a revert commit that undoes part of it** (the headline finding here) → mip-pyramid contracts/generation → background noise blocks → final scrollbar/mip integration.
**Method:** Pulled HEAD tarball, diffed against my prior pass-3 tree to isolate every changed file, then — critically — walked the **per-commit patches** for the changed files one commit at a time (not just prior-vs-HEAD) to catch anything added and later reverted, since a HEAD-vs-baseline diff alone would have hidden exactly that. This is what surfaced §1 below.

This report contains **only new findings and corrections to existing tickets** — nothing already covered by any of the 17 prior audit docs or the ~130 ticket files is repeated.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **ICW-078's stale-frame-publication guard was fully implemented, then silently reverted two commits later** by `9247bff` — the epoch tracker, its `BeginRequest()`/`IsCurrent()` calls, and the `.Advance()` hook were all deleted from `MainWindow.xaml.cs`. `JIRA.md` and `active-tasks.md` **still say ICW-078 is `Done`** at current HEAD. This is an active regression of a real correctness fix, mis-tracked as complete. | **High** | 95% |
| 2 | The same revert commit reintroduced string-keyed `annotation.Features["Confidence"]`/`["Severity"]` dictionary access in `CreateAnnotationToolTip`, undoing the one call site `AnnotationFeaturePresenter.BuildTooltipContent` was built for. That method is now dead code — unused and untested — while the sidebar path (`BuildRows`) still correctly uses the presenter. Split-brain: half the annotation-display code is typed, half reverted to stringly-typed. | **Medium** | 90% |
| 3 | Three background-generation slider handlers (`OnBackgroundTargetChanged`, `OnBackgroundNoiseChanged`, `OnBackgroundCircleCountChanged`) are now empty `{ }` method bodies — live event wiring to methods that do nothing. Values are read only at Regenerate-click time via `TryReadGenerationOptions`. Not incorrect, but dead/misleading wiring nobody has cleaned up. | **Low** | 90% |
| 4 | Correction to ICW-103 (`Protect DefectBitmap GDI+ usage from concurrent mutation/dispose`): its premise assumes a `Dispose()` call exists somewhere that needs synchronizing. It doesn't — the defect-template `Bitmap` pool is **never disposed at all** when a scene is regenerated (confirmed still true at this HEAD; this is the same gap I reported in my first pass). ICW-103 should have "add the missing disposal call" in scope, not just "guard the eventual one." | **Low** (ticket-scope correction) | 85% |
| 5 | `IBackgroundTileSource` (new mip-pyramid interface) has zero implementations and zero consumers anywhere in `src/`. Consistent with ICW-076's own honest `In Progress` status — not a new problem, just confirming the ticket isn't overclaiming. No action needed. | — (verification only) | 90% |

**Bottom line:** the highest-value thing this pass found is that the backlog's own bookkeeping is now wrong in a way that matters — not a stale ticket description, but a **currently-shipping regression of a real bug** that the tracker reports as fixed. Everything else this pass found is minor by comparison.

---

## 1. [HIGH] ICW-078 fix reverted by `9247bff`, still marked `Done`
**Confidence: 95%** — directly confirmed via per-commit diff archaeology, not inference.

**Timeline, reconstructed commit-by-commit:**

1. `3dc49da` ("Harden render request epochs and document handoff") — adds `RenderRequestTracker` (`Core`), wires it into `MainWindow.xaml.cs`:
   ```csharp
   private readonly RenderRequestTracker _renderRequestTracker = new();
   ...
   var requestVersion = _renderRequestTracker.BeginRequest();       // in RenderFrameAsync
   ...
   if (!_renderRequestTracker.IsCurrent(requestVersion)) { return; }  // before publishing the frame
   ...
   _renderRequestTracker.Advance();                                   // in OnViewportMouseMove, per pan tick
   ```
   This is a correct, real fix for exactly the race its own handoff doc describes (`docs/handoffs/2026-07-25-render-stability-sprint-handoff.md`: *"in-flight frame work is ignored if a newer request supersedes it before completion"*). `JIRA.md` is updated to mark `ICW-078` `Done` in this same commit.

2. `9247bff` ("Restore scrollbar wiring and harden canvas interactions") — **deletes every one of those four lines**:
   ```diff
   -    private readonly RenderRequestTracker _renderRequestTracker = new();
   ...
   -        var requestVersion = _renderRequestTracker.BeginRequest();
   ...
   -        if (!_renderRequestTracker.IsCurrent(requestVersion))
   -        {
   -            return;
   -        }
   -
   ...
   -        _renderRequestTracker.Advance();
   ```
   Nothing in this commit's message, `JIRA.md` row, or `active-tasks.md` row for `ICW-078` mentions this removal — the row for `ICW-078` is untouched by this commit and still reads `Done`. The commit's own JIRA-row summary for what it *did* change is about scrollbar wiring and camera-scale defaults (matches ICW-079), not ICW-078 — this looks like a merge/rebase-style loss rather than a deliberate, acknowledged revert.

3. I confirmed directly against the **current HEAD** file (not just the commit diff) that all four elements remain absent: `grep -n "RenderRequestTracker\|requestVersion\|BeginRequest\|IsCurrent" MainWindow.xaml.cs` returns zero matches. `RenderFrameAsync` currently runs start-to-finish with no epoch check at all before `PublishFrame(factory, frameVisual)`.

4. The `RenderRequestTracker` class and its dedicated test file (`RenderRequestTrackerTests.cs`) **still exist and still pass** — they're just no longer called from anywhere in the app. This is the exact "tests are green so it must be fixed" trap: a reader checking `dotnet test` results, or the JIRA row, or even the class's own existence, would reasonably conclude ICW-078 is handled. Only reading `MainWindow.xaml.cs` itself shows otherwise.

**Why this matters concretely:** the bug ICW-078 was filed for — a slow frame completing after a newer pan/zoom/regenerate request and briefly flashing stale content — is live again today, at HEAD, in a build whose own tracker says it's fixed.

**Recommendation:**
- Re-apply the four reverted elements (trivial — they're fully preserved in `3dc49da`'s diff, this is a clean re-application, not a redesign).
- Change `ICW-078`'s status back to `In Progress` (or open a fresh regression ticket referencing it) until the re-fix is confirmed present at a specific commit.
- Given this happened once already during an otherwise-unrelated scrollbar-restoration commit, consider adding a lightweight regression test that asserts `MainWindow` actually calls into `RenderRequestTracker` (e.g. a reflection-based or behavioral test that fails if the field is removed again) — the existing `RenderRequestTrackerTests.cs` tests the primitive in isolation and, as demonstrated, cannot catch it being unwired from its only call site.

---

## 2. [MEDIUM] `AnnotationFeaturePresenter.BuildTooltipContent` reverted to dead code by the same commit
**Confidence: 90%**

Same commit (`9247bff`) also reverts `CreateAnnotationToolTip`:
```diff
     private static ToolTip CreateAnnotationToolTip(SampleAnnotation annotation)
     {
+        var confidence = annotation.Features["Confidence"];
+        var severity = annotation.Features["Severity"];
         return new ToolTip
         {
-            Content = AnnotationFeaturePresenter.BuildTooltipContent(annotation)
+            Content = $"{annotation.Id}\n{annotation.Classification}\nConfidence {confidence:P1}  |  Severity {severity:P1}"
         };
     }
```
Confirmed unchanged at current HEAD (`MainWindow.xaml.cs:628-636`). `AnnotationFeaturePresenter.BuildTooltipContent` (the method, not the class) is still defined but has zero callers anywhere in `src/` and zero references in `tests/InfiniteCanvas.Tests/AnnotationFeaturePresenterTests.cs` — it's simultaneously dead **and untested**, which is a cleaner signal than §1 (no green test is hiding this one), but it sat unnoticed regardless because nobody grepped for the method's call sites after the revert. The presenter's other method, `BuildRows`, is still correctly wired into the sidebar `DataGrid` (`SampleImageTile.cs:548`) — so this is specifically a partial, one-call-site regression, not a wholesale abandonment of the presenter abstraction.

This directly undoes part of what `ICW-031`/`ICW-080` (typed annotation-metrics presentation) were meant to fix: the string-keyed `Features["Confidence"]` access is exactly the primitive-obsession pattern those tickets target, now living again in the tooltip path.

**Recommendation:** Restore `CreateAnnotationToolTip` to call `AnnotationFeaturePresenter.BuildTooltipContent(annotation)` (one line, already-tested method, no design work needed), then either delete `BuildTooltipContent` if it's decided the tooltip shouldn't use it, or add a call-site test asserting the tooltip actually renders through the presenter — mirroring the reflection/behavioral-test suggestion in §1, since this is the second finding this pass from the *same* commit doing the *same* kind of silent partial revert.

---

## 3. [LOW] Three background-generation slider handlers are empty no-ops
**Confidence: 90%**

```csharp
private void OnBackgroundTargetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
}

private void OnBackgroundNoiseChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
}

private void OnBackgroundCircleCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
}
```
(`MainWindow.xaml.cs`, introduced by `f42c260`.) These were previously live-updating handlers (`_backgroundNoise = (byte)Math.Round(...); await RegenerateSceneAsync(...)`), deliberately gutted when the noise/target/circle-count values were moved to be read once, at Regenerate-click time, inside `TryReadGenerationOptions`. That move itself looks intentional and reasonable (it avoids a regenerate-per-slider-tick storm, which is the right instinct) — but the three handlers were left in place as empty stubs still wired up in `MainWindow.xaml`'s `ValueChanged` bindings, rather than removed along with the binding. This is inert, but it's exactly the kind of "why does this exist" landmine a greenfield project shouldn't accumulate: a future reader has to trace into an empty method body to confirm it really does nothing, and the XAML event subscription itself is now pointless overhead.

**Recommendation:** Delete the three handler methods and their `ValueChanged="..."` bindings in `MainWindow.xaml`; the sliders' current values are already read directly from the `Slider.Value` properties inside `TryReadGenerationOptions`, so no behavior changes.

---

## 4. [LOW] Ticket-scope correction: ICW-103 assumes a disposal path that doesn't exist yet
**Confidence: 85%**

`ICW-103` ("Protect `DefectBitmap` GDI+ usage from concurrent mutation/dispose during background render") is scoped around synchronizing `DrawDefectPatch`'s `LockBits` against a concurrent `Dispose()` of the same `Bitmap`. I re-confirmed, at this HEAD, that no such `Dispose()` call exists anywhere in the regenerate path — `RegenerateSceneAsync` (`MainWindow.xaml.cs:173-179`) reassigns `_tiles` without disposing the outgoing tiles' `DefectTemplate` bitmap pool (this is the same gap from my first report, still present, still unticketed under its own name). ICW-103's acceptance criteria (*"Add synchronization... so `DrawDefectPatch` cannot lock disposed bitmaps"*) presuppose a dispose call will be added by someone, somewhere, without saying where — but no ticket currently owns adding it.

**Recommendation:** Either fold "add the missing `Dispose()` call in `RegenerateSceneAsync`" explicitly into ICW-103's scope (it's the natural prerequisite — you can't have a *concurrent*-dispose bug until there's a dispose call at all), or split it into its own ticket so it doesn't get lost waiting for ICW-103 to be picked up. Right now the actual leak (unbounded native GDI+ handle growth across Regenerate clicks) is the more urgent of the two problems and has no ticket of its own.

---

## 5. Verified, Not New: `IBackgroundTileSource` is correctly reported as incomplete
**Confidence: 90%** — verification only, no action needed.

`IBackgroundTileSource` (`BackgroundTileContracts.cs:128-133`) has no implementations and no consumers anywhere in `src/`. This matches ICW-076's own status text (*"Nonzero mip variants still need materializer/cache reservation, variant pinning, and external-source coalescing"*, status `In Progress`) — flagging here only to confirm the ticket is **not** overclaiming, in contrast to §1's finding. `BackgroundTileMipPolicy.SelectMipLevel`/`GetDimensions` (the pure-logic half of the mip work) look correct on inspection: mip level selection via `floor(log2(1/minScale))` clamped to `[0, MaxMipLevel]` gives mip 0 at 100%+ zoom and increases monotonically as the camera zooms out, matching the intended behavior.

---

## Suggested Priority

1. **§1** — re-apply the four reverted lines; this is a live correctness regression in a build that reports itself as fixed. Highest priority in this entire report.
2. **§2** — one-line restore, bundle with §1 since it's the same commit and the same "silent partial revert" failure mode.
3. **§4** — scope correction, costs nothing to fold into ICW-103 now before someone implements it against the wrong assumption.
4. **§3** — trivial cleanup, batch with any other low-priority pass.
