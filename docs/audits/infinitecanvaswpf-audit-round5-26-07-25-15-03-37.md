# InfiniteCanvasWPF — Deep-Dive Code Audit (Round 5)

**Commit audited:** `9f96fe55d6436204adcc43c0bc6e4e6986784929`
**Prior audits (not repeated except where status changed):** four rounds of my own reports (all now committed under `docs/audits/`) plus, new this round, the repo's **own internal cross-validation pass**, `docs/audits/infinitecanvaswpf-critical-peer-review-26-07-25.md`, which independently re-verified my prior findings against source and triaged them. I read that document first and built this round on top of it rather than re-deriving what it already settled.
**Method:** Full tarball diff against the last-audited tree (`52a3442`), full-context read of every new/changed file, hand-verification of the peer review's own claims (not taken at face value either), and a fresh line-by-line pass of the genuinely new surfaces this commit introduces: the global exception handlers, camera-native scrollbars, the About dialog, and the new minimum-sparse-tile-pixel-size gate.

---

## 1. Executive Summary

This is the first round where the team's own process caught up to mine: the peer-review document independently reproduced my ticket-tracker findings (duplicate `ICW-065`, orphaned `ICW-061`–`063`), correctly closed several of my older findings as already-fixed (STRtree copy-on-query, `GenerateSet` validation, coalescing fault containment), and correctly declined to open tickets for two things I'd flagged only as questions, not defects (the widened `CameraTransform` scale bounds; `SampleAnnotation` record equality) — both good calls I'm ratifying here rather than re-litigating.

**The headline fix landed correctly.** `App.xaml.cs` now registers `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException`, all logging through the Serilog pipeline that was wired up two rounds ago. I traced all three handlers by hand — they're correctly scoped (dispatcher exceptions are logged and marked handled so the app survives; `AppDomain`/unobserved-task exceptions are logged as fatal without trying to falsely "handle" the unhandleable). This closes the single most-repeated finding across all four prior rounds.

**Genuinely new, well-executed work this round:** camera-native scrollbars (`ViewportScrollbarPolicy`, a pure/testable class in the same good pattern as `ViewportZoomPolicy`), an About dialog, and a new min-pixel-size gate (`ShouldGenerateForPixelSize`) that skips sparse-tile image generation when a tile projects too small to matter — and, notably, the slider that drives that last feature correctly calls the *cheap* `RequestRenderAsync()` rather than a full scene regeneration, unlike the two older noise-tuning sliders (`C-06` from last round, still unfixed) that do the expensive thing for no reason. That contrast is worth putting in front of whoever owns `C-06` — the correct pattern now exists in the same file.

**What's still open, confirmed by direct source reading, not assumption:**
- Tile-cache eviction is still FIFO-by-dictionary-order, now formally registered as `ICW-305` (it was orphaned last round; it's tracked now, still unimplemented).
- `ICW-029` (shutdown races with in-flight regeneration) is unchanged and still real.
- Ticket-tracker duplication (`ICW-081`) is *scoped correctly* but **not yet executed** — the same ~20 duplicate-ID pairs I found last round still exist as separate files today; only the frontmatter schema was normalized, not the underlying duplication.
- One small, freshly-introduced tracker-drift instance: `ICW-082` (persist background-image visibility) is marked "Proposed" in `active-tasks.md`, but I confirmed the actual fix already shipped in this exact commit — the tracker just hasn't caught up to its own source yet.
- A live piece of institutional memory worth calling out directly: `ViewportZoomPolicy.cs` now carries the comment `// NOTE: STOP CHANGING THIS LOGIC. IT IS NOT XOR ^. DO NOT REMOVE THIS COMMENT.` on the exact branch I hand-verified as correct two rounds ago. Someone (or some agent) evidently "simplified" `xIsClamped || yIsClamped` to an XOR at some point — silently changing behavior for the both-axes-clamped case — and it had to be caught and reverted (`ICW-072`, status "Reverted"). This is the clearest concrete evidence yet of the exact risk multiple uncoordinated agents pose to a shared codebase: a plausible-looking "simplification" regressed working logic with no test catching it, because the both-clamped branch still has no dedicated test today (confirmed — see §4).

### Findings this round

| ID | Severity | Confidence | Summary |
|---|---|---|---|
| D-01 | Low | 90% | `ICW-082` fix already shipped in this commit; tracker still says "Proposed" — minor, self-correcting drift, flagged so it isn't mistakenly re-implemented |
| D-02 | Medium | 60% | The `xIsClamped ^ yIsClamped` regression-and-revert on `ViewportZoomPolicy` (`ICW-072`) happened on the one branch still lacking a dedicated test; the defensive comment prevents a repeat only as long as someone reads it before editing |
| D-03 | Low | 70% | Confirmed contrast: `OnMinimumSparseTilePixelSizeChanged` correctly debounces-by-not-needing-one (cheap `RequestRenderAsync`), while the two older noise sliders (`C-06`, prior round) still call full `RegenerateSceneAsync` per tick in the same file — the fix pattern now exists locally and should be copied, not re-designed |
| D-04 | Low | 55% | `ICW-081` ticket-corpus reconciliation is correctly scoped but not executed — verified the same ~20 duplicate-ID file pairs from my last report still exist unmerged today |
| D-05 | Informational | 90% | Verified `ICW-305` (cache eviction policy) is now correctly registered against the exact evidence I supplied last round; no new code change yet, nothing further to add |

### Verified this round (no new issue, stated for the record)

- **`ICW-014` exception handlers**: correct, complete, hand-traced. `DispatcherUnhandledException` → log + `Handled = true`; `AppDomain.UnhandledException` → log fatal (correctly does not attempt to "handle" a domain-level unhandled exception, since that's not possible); `TaskScheduler.UnobservedTaskException` → log + `SetObserved()`, which matters directly given the codebase's existing fire-and-forget `Task.Run` calls in `SampleImageTile`. Both subscribe in `OnStartup` and correctly unsubscribe in `OnExit`.
- **`ViewportScrollbarPolicy`**: pure, side-effect-free, hand-traced `ComputeMetrics`/`ComputePanDelta` against the affine camera transform math used elsewhere in the codebase — consistent and correct. Known remaining gaps in its WPF-side wiring (overlay geometry, nullable initialization) are already captured by `ICW-077`, not re-detailing.
- **`AboutDialog`**: simple, correctly modal (`Owner` set before `ShowDialog()`), no issues found. Stylistic note only: it's built entirely in code rather than XAML, unlike the rest of the app's UI — inconsistent but not a defect.
- **Peer review's own triage**: spot-checked its three "Rejected or Already-Resolved" calls (STRtree copy-on-query, coalescing fault containment, `GenerateSet` validation) against current source myself rather than trusting the write-up — all three hold up.

---

## 2. D-01 Detail — `ICW-082` Already Fixed, Tracker Not Yet Updated

**Confidence: 90%**

```csharp
// CanvasUserSettings.cs — property exists
public bool ShowBackgroundImages { get; init; } = true;
```
```csharp
// MainWindow.xaml.cs — fully wired: load, apply, and save all present
148: ShowBackgroundImagesCheckBox.IsChecked = settings.ShowBackgroundImages;
407: if (_annotationDisplayOptions.ShowBackgroundImages) { ... }
1407: ShowBackgroundImages = ShowBackgroundImagesCheckBox.IsChecked ?? true,
```
All three legs of the round trip (load into UI, apply during render, save back out) are present in this exact commit. `active-tasks.md` still lists `ICW-082` as `Proposed`. This is likely just sequencing — the tracker entry was probably written before this commit's source changes were finalized — but I'm flagging it explicitly so nobody re-implements an already-shipped fix. **Recommended action:** flip `ICW-082` to `Done` and add the round-trip test the ticket already calls for (I did not find a `ShowBackgroundImages`-specific case in `CanvasUserSettingsTests.cs` — the property is covered generically by the record's other round-trip tests but has no dedicated assertion).

---

## 3. D-02 / D-03 Detail — Two Small, Concrete Signals Worth Acting On

**D-02, confidence 60%:** The revert comment on `ViewportZoomPolicy.cs:28` is real, load-bearing institutional memory sitting in a place future edits (by any agent or person) will encounter mid-edit, not before. A comment is not a control — it only works if read. The both-axes-clamped branch (`xIsClamped && yIsClamped`) still has no dedicated test in `ViewportZoomPolicyTests.cs` (confirmed — the four existing tests cover single-axis-clamped and free-axis-recovery paths, not the simultaneous case). **Recommended action:** add one test asserting the exact regression this comment is guarding against — `xIsClamped=true, yIsClamped=true` zooming in should use `Math.Max` semantics, not XOR-gated logic — so the guard is enforced by CI, not by hoping the comment gets read.

**D-03, confidence 70%:** Direct code comparison, same file, same commit:
```csharp
// New this round — correct, cheap
private async void OnMinimumSparseTilePixelSizeChanged(...)
{
    ...
    await RequestRenderAsync();   // just re-renders with the new gate value
}

// Unchanged from last round — still expensive, still un-debounced
private async void OnBackgroundNoiseChanged(...)
{
    ...
    await RegenerateSceneAsync(fitToWidth: false);   // full scene rebuild per slider tick
}
```
Not a new bug — this is the same `C-06` finding from last round, still open — but it's now demonstrably fixable by copying a pattern that exists three sliders down in the same class, which lowers the remaining effort to essentially zero. Worth re-surfacing with this framing so it doesn't get deprioritized as "needs design work" when it doesn't.

---

## 4. D-04 Detail — Ticket Reconciliation Scoped, Not Yet Executed

**Confidence: 55%**

```
ls docs/tasks/tickets/ | grep -oE 'ICW-[0-9]+' | sort | uniq -c | sort -rn | head
      2 ICW-065   2 ICW-064   2 ICW-063   2 ICW-062   2 ICW-061
      2 ICW-055   2 ICW-054   2 ICW-053   2 ICW-052   2 ICW-051 ...
```
Same duplicate-ID count as last round. `ICW-081` (status `Proposed`) correctly names this exact problem, including the `ICW-065` duplicate specifically — so the diagnosis is right and already written down; the merge/close pass itself just hasn't run yet. Nothing new to add beyond confirming the gap is real and unchanged — deliberately keeping this brief since `ICW-081` already has the acceptance criteria needed to close it.

---

## 5. Assumptions & Open Questions

1. Same tooling assumptions as prior rounds (tarball-based read-only review, no local build/test execution).
2. I did not re-run or re-verify the three other new audit documents committed alongside the peer review (`icw-deep-dive-audit-26-07-25-08-09-54.md`, `ICW-Audit-7-25-26-audit-26-07-25-03-11-26.md`, `icw-audit-7-25-26-audit-26-07-25-03-30-00.md`) claim-by-claim — I relied on the peer review's own cross-validation of those, since re-deriving its work would itself repeat the duplication problem this whole thread is about. If useful, a dedicated pass reconciling all *audit documents* (as opposed to tickets) against each other would be a reasonable next step, mirroring `ICW-081` but for `docs/audits/` instead of `docs/tasks/tickets/`.
3. `ICW-076` (source-agnostic background tile mips) has an ADR on file but zero implementation code yet (`grep -r "mip" src/` returns nothing) — correctly still "In Progress" at the design stage, nothing to audit there this round.
4. Open question for the team: is there a reason `ICW-082`'s tracker status wasn't updated in the same commit that shipped its fix? If the answer is "the tracker update is a separate, deliberate step," that's fine — just confirming it's not a sign the automation writing `active-tasks.md` is reading from a different source than the one being committed.
