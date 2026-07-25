# InfiniteCanvasWPF — Deep-Dive Code Audit (Round 4)

**Commit audited:** `52a3442d98a47d88df345f2cec9f24b08fbecb67` ("Implement next wave of high ROI tasks")
**Prior audits (not repeated here except where status changed):** `docs/audits/infinitecanvaswpf-code-audit-26-07-24-13-10-55.md`, `...-addendum-26-07-24-22-24-24.md`, and my two follow-up reports covering `43bfd55b`→`1f291b92`. This round covers everything changed since `1f291b92`: 2 new Core files, +120 lines in `MainWindow.xaml.cs`, +184 lines in `SampleImageTile.cs`, a new `TileCacheBudget`, deterministic PRNG rework, Serilog integration, and ~90 new/changed ticket files.
**Method:** Full tarball diff against the last-audited tree to isolate every change, full-context read of every touched file, hand-traced the new cache-eviction and PRNG logic line-by-line, and cross-checked ticket claims against source rather than trusting ticket text (per instruction).

---

## 1. Executive Summary

Real progress shipped: `objectsPerTile` is now bounded (closes my original F-03), `GenerateSet` throws instead of silently ignoring `imageCount` when it conflicts with `rows` (closes F-09), the coalesced-render fault path no longer poisons the shared task (verified ICW-034 correctly), the STRtree query-immutability issue is already fixed in code despite its ticket still saying "proposed" (see §3), and Serilog is now fully wired with file + Windows Event Log sinks.

That last point is also this round's biggest miss: **the infrastructure for the single highest-priority finding across all four audits (`ICW-014`, no global unhandled-exception handler) is now completely in place and unused.** Serilog is configured, `App.xaml.cs` has `OnStartup`/`OnExit` overrides already open for editing, and still no `DispatcherUnhandledException`, `AppDomain.UnhandledException`, or `TaskScheduler.UnobservedTaskException` handler was added. This is now a 15-minute fix with zero remaining excuse.

**The more consequential discovery this round is process, not code.** The user's brief asks me to check `ICW-###` tickets before reporting to avoid duplication — I did, and the tracker itself is no longer trustworthy for that purpose:

- **23 of ~110 ticket files have zero entry in `docs/tasks/active-tasks.md`** (verified by set-diff, listed in §2) — including tickets describing bugs that are already fixed in code (wasted future effort if actioned) and at least one (`ICW-103`) that independently corroborates a real, still-open concurrency bug this audit also found (`B-02` below).
- **At least four incompatible ticket schemas coexist** (plain frontmatter, `status/scope/files_to_change` "draft" style, `id/author: Copilot/key/title` style, `repo-area/severity/assignee` style with fictional team names like `annotations-owner`, `spatial-team`) — clear evidence of multiple parallel agents filing tickets with no reconciliation pass.
- **Duplicate IDs with duplicate content**: `ICW-017` and `ICW-053` both claim to remove the same dead `RefreshCommand`; `ICW-020` and `ICW-055` both claim the same pixelometer O(1) lookup fix; `ICW-006`, `ICW-060`, and `ICW-061` all separately describe the same STRtree-immutability issue (already fixed — see §3).

I've treated this as findable evidence rather than editorializing on process — it's directly relevant to the explicit "avoid duplication" instruction, and it's the reason I verified every claim against source instead of the tracker.

### Findings this round

| ID | Severity | Confidence | Summary |
|---|---|---|---|
| C-01 | **High** | 90% | Ticket tracker integrity: 23 orphaned tickets, 4 incompatible schemas, ≥3 duplicate-ID pairs — see §2 |
| C-02 | **High** | 85% | `ICW-014` (global exception handler) still open despite Serilog now being fully wired — the remaining fix is trivial and the infrastructure cost is already paid |
| C-03 | Medium | 70% | `TileCacheBudget`'s eviction policy is FIFO-by-dictionary-enumeration-order, not LRU as its own (untracked) ticket `ICW-003` demands — and is not even reliably FIFO once evictions begin, since `Dictionary` enumeration order is undocumented/unstable after `Remove` calls |
| C-04 | Medium | 65% | `TileCacheBudget.SetPinnedTiles` protects only the *current* frame's visible tiles with no margin — combined with C-03, panning back and forth across a cache-budget boundary will thrash |
| C-05 | Medium | 70% | New `DeterministicRandom` is a mutable `struct` passed by value across method boundaries in `SampleImageGenerator`; entropy consumed inside a callee (e.g. `GenerateCenteredDefectBitmap`) never advances the caller's sequence — a fragile, non-obvious contract, independently corroborated by nothing visibly broken today but one line-reorder away from silently degrading template variety |
| C-06 | Medium | 90% | Background/defect noise sliders (`OnBackgroundNoiseChanged`/`OnBackgroundCircleCountChanged`) call full `RegenerateSceneAsync` on every `ValueChanged` tick with no debounce — dragging either slider queues dozens of wasted full scene regenerations, unlike the resize path which already has a 150ms debounce timer to copy |
| C-07 | Low | 85% | Confirmed persisting: my prior `B-01`/`B-02` (defect-template `Bitmap` pool leaked on regenerate; shared mutable non-thread-safe GDI+ objects) are both still unfixed — `ICW-042`'s own ticket defers the fix to "after `ICW-029`", which is also still open, so the leak has now survived three audit cycles. `ICW-103` (orphaned, see C-01) independently confirms the same concurrency risk. |
| C-08 | Low | 55% | `CameraTransform`'s default min/max scale widened from the previously-tuned `(0.01, 50)` to `(1e-10, 10000)` — removes a second line of defense against absurd zoom levels if `EnforceZoomFloor`'s scene-relative math ever misbehaves; unclear if intentional |
| C-09 | Low | 60% | `BlendDefect`/renderer divergence (`ICW-035`) persists unchanged; still correctly tracked, not re-detailing |

### What I verified as correctly fixed this round

- **F-03/ICW-030** (unbounded `objectsPerTile`): `SampleImageGenerator.MaxObjectsPerTile = 256`, enforced in both the generator (`ArgumentOutOfRangeException`) and the UI (`TryReadGenerationOptions`). Confirmed by reading both sites.
- **F-09** (`GenerateSet` silently ignoring `imageCount` when `rows` conflicts): now throws `ArgumentException` on mismatch instead of silently overriding. Confirmed.
- **ICW-006/060/061** (STRtree `Query` leaking a mutable internal list): `StrTreeSpatialIndexService.Query` now unconditionally copies to an array with an explanatory comment. Confirmed fixed — **all three tickets describing this should be closed as already-resolved, not implemented.**
- **ICW-034** (coalescing render faults poisoning the shared task): hand-traced `CoalescingAsyncAction.ProcessAsync` — non-cancellation exceptions are now caught, reported via an injected `Action<Exception>` callback (itself wrapped in its own try/catch so a bad handler can't break the loop), and the `while(true)` loop correctly continues rather than exiting, so a coalesced follow-up request is no longer silently dropped. Confirmed correct.
- **ICW-064/049/050** (bounded tile cache, deterministic generation): the mechanisms are real and mostly sound — see C-03/C-05 for the two gaps found within them.
- **ICW-039/042** background-fetch failure path now releases its cache reservation (`OnTilePixelsGenerationFailed` → `_tileCacheBudget.Release(tile)`), closing a leak I would otherwise have flagged.

---

## 2. C-01 Detail — Ticket Tracker Integrity

**Evidence:**
```
comm -23 <(ls tickets/ | grep -oE 'ICW-[0-9]+' | sort -u) <(grep -oE 'ICW-[0-9]+' active-tasks.md | sort -u)
→ ICW-001 ICW-002 ICW-003 ICW-006 ICW-051 ICW-056 ICW-061 ICW-062 ICW-063
  ICW-101 ICW-102 ICW-103 ICW-201 ICW-202 ICW-203
  ICW-301 ICW-302 ICW-303 ICW-304 ICW-305 ICW-306 ICW-307 ICW-308
```
23 tickets, zero tracker rows. Four schemas confirmed by direct inspection (`head -8` on one file per family):
- Plain (`ICW-004`...`ICW-050` mainline): no frontmatter or simple `status:`/`summary:`.
- "Draft" family (`ICW-001`–`ICW-006`ish): `status: draft`, `scope:`, `files_to_change:`.
- "Copilot" family (`ICW-051`, `056`, `061`): `id/author: Copilot/key: ICW/title/status/type/priority`.
- "Team" family (`ICW-101`–`308`): `status/title/repo-area/severity/assignee` with placeholder team names (`rendering-team`, `spatial-team`, `core-team`, `annotations-owner`) that don't correspond to any real ownership structure in a solo/small greenfield repo.

**Confirmed duplicate-ID content collisions:**
| Pair | Both describe |
|---|---|
| `ICW-017` / `ICW-053` | Removing dead `RefreshCommand` from `CanvasViewportViewModel` |
| `ICW-020` / `ICW-055` | Pixelometer O(1) tile lookup |
| `ICW-006` / `ICW-060` / `ICW-061` | STRtree `Query` immutability (already fixed — see §3) |
| `ICW-022` / `ICW-052` | MainWindow decomposition (`ICW-052`'s file list even names two files, `ViewportZoomCalculator.cs`/`GenerationOptionsValidator.cs`, that don't exist in the tree — aspirational, never created) |

**Recommendation (mechanical, do first, ~1-2 hrs):**
1. Run the four-schema set through a single normalization pass (the repo already has `scripts/normalize_task_files.py` — check whether it currently handles all four schemas or only one).
2. For each duplicate pair, close the lower-quality/less-detailed one with a pointer to the survivor.
3. Add the 23 orphaned tickets to `active-tasks.md` with accurate status — several (the `ICW-006` family) can be closed immediately as already-fixed rather than scheduled.
4. Consider a CI check (ties into the already-open `ICW-036`/`013`/`051` "CI baseline" tickets — another duplicate-ID trio, incidentally) that fails a PR introducing a ticket ID that already exists.

---

## 3. C-02 Detail — Exception Safety Net Still Missing, Now Trivial

**File:** `src/InfiniteCanvas.App/App.xaml.cs` (full file, 22 lines)
**Confidence: 85%**

```csharp
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		Log.Logger = SerilogHost.Logger;
		Log.Information("Application starting");
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log.Information("Application exiting");
		SerilogHost.Shutdown();
		base.OnExit(e);
	}
}
```

`SerilogHost.cs` (new this round) configures a `LoggerConfiguration` writing to a rolling daily file (14-day retention) and the Windows Event Log for warnings and above. This is exactly the sink a `DispatcherUnhandledException` handler needs — and it still isn't wired to one. `ICW-014` remains "To Do" in `active-tasks.md`, accurately, but the remaining work is now three constructor-adjacent lines:

```csharp
DispatcherUnhandledException += (_, args) =>
{
    Log.Fatal(args.Exception, "Unhandled dispatcher exception");
    args.Handled = true; // or false, per the team's crash-vs-continue decision — this is the one real design choice left
};
```

I'm flagging this separately from the original `ICW-014` finding (not duplicating it) specifically to note the cost/benefit has changed: this was always high-value; it is now also nearly free.

**Minor, related:** `App.xaml.cs` uses tab indentation while every other file in the repo uses spaces — a one-file formatting outlier. Low severity, worth folding into whichever CI/format ticket survives the `ICW-036`/`013`/`051` de-duplication in §2.

---

## 4. C-03 / C-04 Detail — Cache Eviction Is FIFO-ish, Not LRU, With No Prefetch Margin

**File:** `src/InfiniteCanvas.Rendering/SampleImageTile.cs:375-499` (`TileCacheBudget`), call site `MainWindow.xaml.cs:284-296`
**Confidence: 70% (C-03), 65% (C-04)**

```csharp
var evictedTile = _trackedTiles.Values.FirstOrDefault(candidate =>
    !string.Equals(candidate.Id, tile.Id, StringComparison.OrdinalIgnoreCase)
    && !_pinnedTileIds.Contains(candidate.Id)
    && candidate.IsImageGenerated);
```

Two independent problems:

1. **No recency tracking at all.** `TryReserve` returns early (`return true`) for an already-tracked tile without touching its position — there is no "move to most-recently-used" step anywhere in this class. Eviction order is purely `Dictionary<TKey,TValue>.Values` enumeration order, which is *approximately* insertion order only in the absence of `Remove` calls — and `Remove` is called on every eviction and every `Release`. Per Microsoft's own documentation, `Dictionary` enumeration order is unspecified and can change after removals (free-list slot reuse). So this isn't a deliberate, working FIFO either — it degrades toward implementation-defined order the moment the cache starts evicting, which is precisely when eviction *policy* starts to matter. The orphaned ticket `ICW-003-tilecachebudget-lru.md` (see §2) independently flags wanting real LRU via a `LinkedList`-backed structure — I'd endorse that scope directly rather than writing a new ticket.
2. **Pinning has zero margin.** `SetPinnedTiles(visibleTiles)` is called every frame with exactly the current frame's intersecting tiles (`MainWindow.xaml.cs:288-289`). A tile one pixel outside the viewport gets no protection at all. Combined with (1), a user panning back and forth across a boundary where resident tiles exceed the 4 GiB budget will see genuine thrash: pan right evicts tiles on the left (via whatever order the dictionary happens to enumerate), pan back left re-fetches them from scratch, likely evicting whatever's now least-favorably-positioned in enumeration order rather than what's actually farthest from the viewport.

**Fix, in order of effort:** (a) cheap: track insertion via `LinkedList<string>` + node dictionary for true O(1) LRU, exactly as the orphaned `ICW-003` scopes it; (b) also cheap: pin a viewport-expanded margin (e.g., current visible tiles plus one ring of neighbors) rather than only the exact frame.

---

## 5. C-05 Detail — Mutable-Struct-By-Value PRNG Hand-off

**File:** `SampleImageGenerator.cs:262-278` (`BuildDefectTemplatePool`), `:444-482` (`DeterministicRandom`)
**Confidence: 70%**

`DeterministicRandom` is a hand-rolled SplitMix64 generator implemented as a mutable `struct`:

```csharp
private struct DeterministicRandom
{
    private ulong _state;
    ...
    private ulong NextUInt64() { _state += 0x9E3779B97F4A7C15UL; ... }
}
```

Every call site passes it **by value** (no `ref`/`in` anywhere — verified by grep across the file). Traced `BuildDefectTemplatePool`'s loop specifically:

```csharp
for (var index = 0; index < count; index++)
{
    var aspect = 0.45 + (random.NextDouble() * 1.95);      // advances the loop's `random`
    var templateWidth = random.Next(156, 276);              // advances it again
    var bitmap = GenerateCenteredDefectBitmap(templateWidth, templateHeight, random); // BY VALUE — a copy
    ...
}
```

`GenerateCenteredDefectBitmap` receives a *copy* of `random`'s current state, advances its own copy through several blob-geometry calls internally, and returns — none of that advancement is visible to `BuildDefectTemplatePool`'s `random` afterward. The loop's own sequence still progresses (thanks to the two direct calls per iteration before the hand-off), so I did **not** find evidence this currently produces visibly-duplicate templates — I want to be precise about that rather than overclaim. What I can confirm with high confidence is the structural fact: entropy consumed inside a callee is silently discarded by the caller. This is a well-known C# pitfall (mutable structs violate the implicit "state persists" expectation the moment they cross a by-value boundary), and it means the *apparent* seed-derivation care taken elsewhere in this file (`unchecked(seed + tileIndex * 104729)` etc.) coexists with a spot where the actual consumed-entropy accounting is opaque to the next person reading the seed math. One reordering of a `random.Next()` call from before a hand-off to after it would silently change output distribution with no compiler warning and no failing test (there is no test asserting template-to-template visual variety, only bounds/determinism tests — confirmed via the current `SampleImageGeneratorTests.cs`).

**Fix:** Change the parameter type to `ref DeterministicRandom random` everywhere it's threaded through a helper that should share the caller's sequence (the compiler will then force every call site to be explicit about it), or make it a `class` instead of a `struct` — the latter is one word and removes the entire footgun class.

---

## 6. C-06 Detail — Undebounced Regenerate-on-Slider-Drag

**File:** `MainWindow.xaml.cs:388-408`
**Confidence: 90%**

```csharp
private async void OnBackgroundNoiseChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (!IsLoaded) return;
    _backgroundNoise = (byte)Math.Round(BackgroundNoiseSlider.Value);
    await RegenerateSceneAsync(fitToWidth: false);
}
```

Identical shape for `OnBackgroundCircleCountChanged`. WPF `Slider.ValueChanged` fires continuously during a drag gesture — this queues a **full scene regeneration** (spatial index rebuild, tile metadata for up to 2000 tiles, cache-budget reset) on every intermediate tick. `_generationGate` (a `SemaphoreSlim(1,1)`) prevents them from running concurrently, but does nothing to prevent them from queuing up sequentially — every regenerate except the one matching the value the user finally releases on is 100% wasted work, and the UI will visibly stutter/flicker for the duration of the drag. The resize path already solved exactly this shape of problem with `_resizeTimer` (150ms `DispatcherTimer` debounce) three audits ago — this is a straight copy-paste of that pattern onto two new sliders.

**Fix:** Reuse or clone `_resizeTimer`'s debounce pattern for these two sliders (a single shared `_generationDebounceTimer` covering both would avoid a third near-duplicate).

---

## 7. Assumptions & Open Questions

1. Same tooling assumptions as prior rounds (tarball-based read-only review, no local build/test execution).
2. C-05's confidence is deliberately capped at 70% rather than higher, since I could not execute the code to visually/statistically confirm whether template variety is actually degraded — the finding is about a fragile *contract*, confirmed structurally, not a confirmed visible defect.
3. **Open question for the team:** is the `scripts/normalize_task_files.py` script (new this round) intended to reconcile the four ticket schemas in §2, and has it been run against the current `tickets/` directory? If yes and the orphans/duplicates in C-01 survived a normalization pass, that changes the recommended fix from "run the script" to "fix the script."
4. **Open question:** was `CameraTransform`'s default min/max scale widening (C-08, `(0.01,50)` → `(1e-10,10000)`) a deliberate response to a specific custom-zoom requirement, or an incidental side effect of removing the explicit constructor arguments at the `MainWindow` call sites? Worth a one-line confirmation either way.
