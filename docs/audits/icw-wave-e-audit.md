# InfiniteCanvasWPF — Deep-Dive Audit: Commit `b8d95dd` (Sprint 1 Wave E)

**Commit:** `b8d95ddab66b33d808a97b6b40cdd2d4bfdbf74c` — "Sprint 1 Wave E: Post-wave cleanup, deduplication, and stress benchmarks"
**Parent:** `596fea640dfff9e5c57e0d6e8be37a43909aa41d`
**Method:** Full patch retrieved via `git`/GitHub API (35 files, +2,922/-245), cross-referenced against the complete files at this SHA (`TileWorkCoordinator.cs`, `BackgroundTileContracts.cs`, `MainWindow.xaml.cs`, `RenderRequestTracker.cs`, `Validate-TaskTracker.ps1`) and against `docs/tasks/JIRA.md`, `docs/tasks/active-tasks.md`, and the relevant ticket files, both as committed here and as they stand after the commit.
**Scope note:** This commit's *code* footprint is small and precise (a real bug fix + two defensive guards + a benchmark file); the other 32 files are documentation/tracker changes. Because the commit's own stated purpose is deduplication and validation, this audit weighs the tracker/process claims as heavily as the code.

---

## Executive Summary

The functional code change in this commit — reworking `PublishInterestSet` to call `CancelWorkItem` directly instead of stripping claimants first — is a genuine, correct fix for a real bug (stale `_generationQueued` lockout), and is already documented as such in the council review bundled in this same commit. That part of the commit is sound.

The **process claims layered on top of it are weaker than advertised**, and this is where the audit found the most actionable material:

1. **The "validation script now flags duplicate IDs" claim is false.** `scripts/Validate-TaskTracker.ps1` contains no duplicate-ID logic at all, and it explicitly excludes `active-tasks.md`/`JIRA.md` — the only two files that actually contain the duplicates — from validation entirely.
2. **ICW-081 ("ticket deduplication") is marked Done in this same commit, but at least one of the duplicate IDs it was scoped to fix — `ICW-100` — is still duplicated** in `active-tasks.md` after the commit, contradicting the commit's own closure claim.
3. **`ICW-078`'s tracker rows disagree with each other and with the code.** `JIRA.md` says Done; `active-tasks.md` still carries a stale "reverted, needs re-verification" row for the same ticket, even though the code (and a *different* ticket, `ICW-100`) confirms the fix landed.
4. **This commit's own bug fix orphaned a public method** (`TileWorkItem.GetClaimantIds()`) that has zero remaining call sites — a small, self-inflicted dead-code smell introduced by the very fix being celebrated.
5. Two design-level concerns in `TileWorkCoordinator` — an O(n)-per-promotion queue-reordering algorithm and a claimant-registration cleanup gap — are worth formalizing into their own tickets rather than living as prose bullets in `ICW-143`'s "Deferred Items" section.

Section 5 lists what's already well-tracked so this isn't read as ignoring the project's own (quite good) audit trail — several findings below build on, rather than duplicate, the council review and follow-up audit already committed alongside this change.

---

## 1. Findings: Tracker/Process Integrity (new this session)

### 1.1 The duplicate-ID validation claim does not match the validator's code — **High confidence**

Three places in this commit assert that duplicate-ID detection was added to tooling:

> JIRA.md (ICW-081 row): *"Validation script aware of duplicate IDs."*
> JIRA.md (Activity log): *"Validation script extended to flag duplicate IDs."*
> active-tasks.md (Wave E summary): *"...validation script updated"*

`scripts/Validate-TaskTracker.ps1` (136 lines, fetched in full at this commit) contains:

- No string `"duplicate"` anywhere in the file.
- No accumulation of seen `id`/`key` values, no `Group-Object`, no set-membership check — nothing that could detect two files claiming the same ID.
- A `$skipNames` list that **explicitly excludes `active-tasks.md` and `JIRA.md`** from validation (`Get-TaskFiles`, line 17). Those are the two files that actually contain the duplicate rows described elsewhere in this audit. Even if duplicate-detection logic existed, it would not run against the files where the duplicates live — only against files under `docs/tasks/tickets/`.

**Recommendation:** Either implement the claimed check (a `Group-Object id`-style pass over *all* tracked files, including the two tracker files, that fails when a group count exceeds 1) or correct the commit's own record to stop asserting a capability that doesn't exist. This is a cheap, mechanical fix and should be the actual next step for `ICW-081`, not a new ticket.

### 1.2 `ICW-081` is marked Done, but the duplicate it names is still duplicated — **High confidence**

The council review bundled in this same commit (`docs/audits/sprint1-wave-d-council-review-26-07-30.md`) is explicit about scope:

> *"Resolve duplicate IDs (ICW-100 x4, ICW-102/094/014/098/099 x2)"* — listed as a **deferred, non-blocking** item, to be done *"before creating any new ICW-P0/P1 ticket files."*

This commit's Wave E work then marks `ICW-081` (docs/tasks/tickets/ICW-081-audit-ticket-corpus-reconciliation.md) `status: Done`, and both `JIRA.md` and `active-tasks.md` describe the dedup as accomplished ("ICW-098/099/100 duplicates resolved with dedup notes").

Checking `active-tasks.md` **as committed at this SHA**, two unrelated rows both use the ID `ICW-100`:

- Row (line 21): *"Re-apply and verify `RenderRequestTracker` wiring (ICW-078 regression)"* — status **Done**.
- Row (line 134): *"Define overlay precedence and align pixelometer sampling with rendered mip — **RETAINED as unique ticket**"* — status **To Do**.

The second row's own text ("RETAINED as unique ticket," "This is a distinct concern from RenderRequestTracker (Done)") shows the author *knew* these were two different concerns sharing one ID and chose to keep both under `ICW-100` rather than renumber the second one — the exact opposite of the pattern used correctly elsewhere in this same file (e.g., `ICW-094-RESET`, `ICW-098-scrollbar` were given distinct suffixes specifically to resolve this kind of collision).

**Correction to record:** `ICW-081` should not be closed while a duplicate it explicitly scoped in ("ICW-100 x4") is still open. Either reopen `ICW-081` with a residual sub-item for `ICW-100`, or fix it now: rename the overlay/pixelometer ticket to `ICW-100-OVERLAY` (or similar) following the project's own established convention, and update its one cross-reference in `ICW-143`'s dependency list if any exists.

### 1.3 `ICW-078` carries two contradictory tracker rows — **High confidence**

`active-tasks.md` line 99 (still present at this commit):

> *"**Status correction:** Wiring was implemented then reverted in commit `9247bff`. `RenderRequestTracker` calls (BeginRequest/IsCurrent/Advance) are absent from MainWindow.xaml.cs at HEAD 139a8b6. ICW-100 tracks re-application."* — status **In Progress**.

But at this commit's HEAD, `MainWindow.xaml.cs` (verified directly, lines ~367–428 of `RenderFrameAsync`) *does* call `_renderRequestTracker.BeginRequest()`, gates `PublishFrame` on `IsCurrent(requestVersion)`, and calls `.Advance()` after publish — matching exactly what row 21 (`ICW-100`, status Done) and `JIRA.md`'s `ICW-078` row (status Done) both already claim.

So as of this commit: `JIRA.md` is accurate, one `active-tasks.md` row (`ICW-100`) is accurate, and a second `active-tasks.md` row (`ICW-078` itself) is stale and describes a regression that has since been fixed — but nothing in Wave E's dedup pass touched it, because Wave E's dedup scope was ID collisions, not stale content. This is worth calling out precisely because it's the kind of drift `ICW-081` exists to catch, and it's a false negative for that ticket's own review process: the `ICW-078` row is *not* an ID collision, it's a *stale-status* row that a purely ID-based grep-for-duplicates check (even a correctly implemented one, see §1.1) would never catch.

**Recommendation:** Update the `ICW-078` row in `active-tasks.md` to `Done`, cross-reference `ICW-100`'s evidence, and add a short note to `ICW-081`'s acceptance criteria distinguishing "duplicate ID" cleanup from "stale status" cleanup — they need different detection strategies and the ticket currently only claims to solve the former.

---

## 2. Findings: Code (new this session)

### 2.1 `TileWorkItem.GetClaimantIds()` is now fully orphaned — **High confidence**

This commit's own bug fix removes the only call site of `GetClaimantIds()`. Before the fix, `PublishInterestSet` called `item.GetClaimantIds()` to snapshot claimant IDs before removing them one at a time; the fix replaces that whole loop with a direct `CancelWorkItem(key, item)` call, which never touches `GetClaimantIds()`.

Verified against the full file at this SHA: `GetClaimantIds()` is defined once (line 753) and referenced nowhere else in `TileWorkCoordinator.cs`, and a repo-wide check found no other caller in `MainWindow.xaml.cs` either.

This is a small thing, but it's a clean instance of "the fix and the cleanup are the same commit and should have caught this" — the pre-existing council review already lists *"GetClaimantIds() LINQ allocation"* as a deferred performance item under `ICW-143`. That framing is now outdated: it's not an allocation to optimize, it's dead code to delete. The public surface area matters here too — `GetClaimantIds()` returns `object[]`, a boxed-identity leak surface if anything external ever depended on it, so removing it is lower-risk than most dead-code deletions.

**Recommendation:** Delete `GetClaimantIds()` (and its XML doc, which explicitly says *"Used by PublishInterestSet to remove claimants for non-interest tiles"* — a comment that's now describing removed behavior). Update `ICW-143`'s deferred-items note accordingly rather than leaving it worded as an allocation-tuning task.

### 2.2 Claimant `CancellationTokenRegistration`s are not cleared when `CancelWorkItem` culls a queued item — **Medium confidence, real mechanism, bounded impact today**

`CancelWorkItem` (queued-item branch) calls `RemoveFromQueue` and `item.DispatchFailed(...)`, then removes the item from `_items`. It never calls `item.RemoveClaimant(id)` for the claimants still attached to that item, so their `CancellationTokenRegistration`s (registered in `TileWorkItem.AddClaimant`) are never disposed here.

Tracing the actual consequence in the current single call site (`MainWindow.RenderFrameAsync`'s per-frame claimant token): the orphaned `TileWorkItem` — no longer reachable from `_items`, `_queue`, or anything else the coordinator tracks — stays alive anyway, kept rooted by the still-registered callback closure inside the per-frame `CancellationTokenSource`. It's only released when that frame's CTS is itself cancelled (next frame) and disposed (the frame after that), at which point the registration fires, calls the (now-pointless) `RemoveClaimant` on an already-abandoned item, and the object chain finally becomes collectible. So in the current, exclusively frame-scoped usage, this is a **bounded, self-healing 1–2 frame delay**, not a permanent leak — consistent with the "no data corruption, wasted resources" pattern the bundled follow-up audit already found for the related `ICW-P0-ACTIVECOUNT` residuals.

The reason this is still worth flagging on its own: `TileWorkCoordinator`'s public API (`Request(... object claimantId, CancellationToken claimantToken ...)`) is generic and says nothing about claimant lifetime being frame-scoped — that's a convention enforced only by the one current caller, not by the type. If a future caller registers a claimant with a long-lived or non-cancelling token (anything that never fires and isn't `CancellationToken.None`, which is explicitly exempted via `if (claimantToken.CanBeCanceled)`), any work item culled via `CancelWorkItem` for that claimant would retain its registration indefinitely — a genuine unbounded leak, not a bounded one.

**Recommendation:** Have `CancelWorkItem` call the item's own claimant-clearing logic (e.g., expose an internal `ClearClaimants()` on `TileWorkItem` that disposes every registration and empties `_claimants`) instead of relying on the claimant token eventually firing on its own. This is a small, local change and removes an implicit assumption (all claimant tokens are short-lived and will fire soon) that isn't documented anywhere near the `Request` method it depends on.

### 2.3 `ViewportInterestSet`'s new null guards don't cover `default(ViewportInterestSet)` — **Medium confidence, design smell**

The Wave D→E null-guard fix converts `ViewportInterestSet` from a primary-constructor record struct to one with an explicit constructor that throws on null `visibleKeys`/`prefetchKeys`. That's correct as far as it goes, but `ViewportInterestSet` is a `struct`, and struct default construction (`default(ViewportInterestSet)`, an uninitialized field, `new ViewportInterestSet[n]`) **bypasses every user-defined constructor** — the null guard only fires for callers who explicitly invoke `new ViewportInterestSet(a, b)`. A `default`-constructed instance has both properties `null`, and the very first call to `.Contains()` or `.IsVisible()` on it throws `NullReferenceException` from inside `VisibleKeys.Contains(key)`.

Today's single call site always constructs explicitly and the field default (`_interestSet = ViewportInterestSet.Empty`) is set correctly, so this isn't live. But the guard reads as more protective than it is — anyone skimming the constructor and seeing `ArgumentNullException.ThrowIfNull` may reasonably assume the type can't exist in a null-backed state, which isn't true for a struct with reference-typed members.

**Recommendation:** Either (a) make `VisibleKeys`/`PrefetchKeys` fall back to a static empty set when read (`VisibleKeys => _visibleKeys ?? EmptyKeySet`), so `default` is safe by construction, or (b) add a one-line comment at the constructor noting the guard doesn't cover `default(ViewportInterestSet)` so future readers don't over-trust it, or (c) convert the type to a `sealed class` if the null-safety is meant to be a real invariant rather than a best-effort check — records-as-classes get the same syntax and equality semantics without the `default` escape hatch.

### 2.4 XML doc `<param>` names no longer match the constructor after the refactor — **Low confidence severity, easy fix**

The type-level XML doc above `ViewportInterestSet` still documents `<param name="VisibleKeys">`/`<param name="PrefetchKeys">` (capitalized, matching the old primary-constructor parameter names). The new explicit constructor's parameters are `visibleKeys`/`prefetchKeys` (lowercase). The doc comment is now describing parameters that don't exist under those names — harmless at runtime, but it's exactly the kind of small papercut that erodes trust in a codebase's documentation over many such refactors, and a nullable/doc analyzer would likely flag it.

**Recommendation:** Move the `<param>` docs onto the constructor itself (where they already partially duplicate the constructor's own `/// <summary>`) and drop them from the type declaration, or reconcile casing.

### 2.5 Benchmark scenario count is off by one relative to three tracker claims — **Low severity, but worth a one-line fix**

`JIRA.md`, `active-tasks.md`, and the `ICW-144` ticket file all say *"8 benchmark scenarios"* / *"8 scenarios"*. Counting `[Benchmark]`-attributed methods in the committed `TileWorkCoordinatorBenchmarks.cs`: `PublishInterestSet_EmptyQueue`, `PublishInterestSet_AllVisible`, `PublishInterestSet_NoneVisible`, `PublishInterestSet_MixedVisibility`, `DrainQueue_FifoFallback`, `DrainQueue_VisiblePromoted`, `FastScrollStress_ThreeCycles` — **7**, not 8. (Each is separately parameterized by `[Params(10, 50)]`, giving 14 total BenchmarkDotNet cases, which may be the source of the miscount, but that's a parameter sweep, not a distinct scenario.)

Small, but notable given this is a commit whose entire second half is about tracker accuracy.

---

## 3. Design/Algorithmic Concerns (extending, not duplicating, existing deferred items)

`ICW-143`'s "Deferred Items" and the council review's "Performance" seat already flag *"O(n) RemoveFromQueue"* and *"GetClaimantIds() LINQ allocation"* as known, accepted-for-now costs. Having read the actual loop, the real risk is sharper than "O(n) somewhere" and worth its own line item rather than a bundled bullet:

### 3.1 The priority-promotion scan in `DrainQueueWithLivenessCheck` is worse than linear in the adversarial case a stress test would actually hit

When the dequeued head of the queue is not in `interestSet.VisibleKeys`, the method drains the **entire remaining queue** into a `remaining` list while scanning for one visible item to promote, then re-enqueues everything (`deferred` first, then `remaining`) — an O(n) pass to promote **at most one** item. Because the just-reprocessed non-visible key is placed back at the front, the *next* iteration of the outer `while` immediately dequeues it again and, if another visible item still exists further back, repeats the full O(n) scan-and-rebuild to promote just that one. For a queue shaped like `[stale, stale, ..., stale, visible, visible, ..., visible]` — precisely the shape produced by a real fast-scroll-away-then-back gesture, and the scenario `FastScrollStress_ThreeCycles` in this commit's own benchmark is positioned to measure — this is **O(n·k)** for `k` visible items behind `n − k` stale ones, not O(n).

The existing benchmark doesn't currently exercise this: `QueueDepth` is capped at 10/50 and the promotion-related scenarios (`DrainQueue_VisiblePromoted`) only test a single mixed batch, not the multi-cycle "many stale ahead of many visible" shape. `FastScrollStress_ThreeCycles` gets closer but still only issues one promotion-triggering call per cycle rather than draining a queue with many interleaved stale/visible items.

**Recommendation:** This deserves its own line under `ICW-144` (or a new sub-ticket, e.g. `ICW-144-QUEUE-ALGORITHM`) with an explicit acceptance criterion: *"Benchmark a queue shaped with alternating stale/visible runs (not just one contiguous block) and assert wall-time scales sub-quadratically with queue depth."* If the benchmark confirms the quadratic-ish behavior, the fix is a data-structure change, not a tuning tweak — see §3.2.

### 3.2 `Queue<T>` is the wrong shape for a structure that needs mid-collection removal and priority reordering

Both `RemoveFromQueue` (already flagged) and the promotion scan above exist because `Queue<T>` only supports FIFO push/pop — every "remove this one item" or "reorder by priority" operation has to fake it by fully draining and rebuilding. This is a structural mismatch, not a series of unrelated micro-inefficiencies: a `PriorityQueue<TElement, TPriority>` (available in modern .NET, already implicitly a dependency of this codebase's target framework) keyed by `(isVisible ? 0 : 1)` would turn both the promotion scan and cancellation-driven removal into O(log n) operations, at the cost of needing a stable tie-breaker for equal priority (insertion order, e.g. a monotonic counter) to preserve today's FIFO-within-priority behavior. Alternatively, splitting into two queues (visible / non-visible) and draining the visible one first is a smaller, more incremental change with similar effect and no new dependency.

**Recommendation:** Treat this as the actual fix behind the currently-vague "O(n) RemoveFromQueue" deferred bullet — recommend scoping it as its own ticket once `ICW-144`'s benchmarks quantify the cost (the council review's own stated trigger condition: *"After ICW-144 benchmarks quantify the overhead"*).

### 3.3 `DispatchCompleted`/`DispatchFailed` swallow all claimant-callback exceptions silently

```csharp
foreach (var cb in callbacks)
{
    try { cb?.Invoke(CacheKey, pixels); }
    catch { }
}
```

Any exception thrown by a claimant's `onCompleted`/`onFailed` callback — including a genuine bug in `SampleImageTile`'s handling code — disappears with no log line, no counter increment, nothing. Given this same file is otherwise disciplined about `Log.Debug`/`Log.Warning`/`Log.Error` at every state transition, this bare `catch { }` stands out. It would hide exactly the kind of "callback threw, tile silently never updates" bug that `ICW-143`'s post-review fix (§ this commit) was created to fix in the first place — if a *different* bug caused the same kind of silent stuck-tile symptom via a thrown callback instead of a dropped callback, nothing here would surface it.

**Recommendation:** At minimum, `Log.Warning(ex, "Coord claimant callback threw for {SourceId}/{TileId}", ...)` inside each catch. This is a few-line change with real debuggability upside and no behavior change (callbacks should still not be allowed to fault the coordinator's dispatch loop).

---

## 4. What's Already Well-Handled (for balance)

To avoid re-litigating ground the project's own audit trail already covers well:

- The actual `PublishInterestSet` bug fix in this commit is correct and matches the council review's own trace of the failure mode (claimant-list-emptied-before-`DispatchFailed`-snapshot).
- `ICW-P0-ACTIVECOUNT`'s decrement-ownership fix (verified in an earlier commit, re-confirmed by the bundled follow-up audit) is sound; this audit did not find a new issue in that area beyond what's already tracked as Residuals A/B.
- The `_disposed` guard added to `DrainQueueWithLivenessCheck` in this commit closes a real (if narrow) gap consistent with every other public method on this class.
- The bundled follow-up audit (`infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md`) is unusually rigorous — it diffs every changed line against a byte-for-byte prior copy rather than trusting handoff prose, and its findings (the inert `RenderRequestTracker` guard given current call-graph serialization, the always-empty `PrefetchKeys`, the `ICW-P0-ACTIVECOUNT` residuals) all check out on independent re-reading and are not repeated here.

---

## 5. Summary of Recommended Actions

| # | Item | Type | Suggested disposition |
|---|---|---|---|
| 1 | Implement real duplicate-ID detection in `Validate-TaskTracker.ps1`, including `active-tasks.md`/`JIRA.md` | Correction | Reopen `ICW-081` acceptance criteria |
| 2 | Resolve the still-duplicated `ICW-100` ID in `active-tasks.md` | Correction | Reopen `ICW-081`, or fix directly (rename overlay ticket) |
| 3 | Update the stale `ICW-078` row in `active-tasks.md` to Done; note stale-status vs. duplicate-ID as distinct dedup categories | Correction | Small `ICW-081` follow-up |
| 4 | Delete orphaned `TileWorkItem.GetClaimantIds()` and its doc comment | Cleanup | Fold into `ICW-143` deferred-items cleanup |
| 5 | Have `CancelWorkItem` explicitly clear claimant registrations instead of relying on eventual claimant-token firing | Robustness | New small ticket, or fold into `ICW-P0-LEASE-RELEASE`'s cleanup pass |
| 6 | Guard `ViewportInterestSet` against `default(ViewportInterestSet)`, or document the gap, or convert to a class | Design smell | Small follow-up on `ICW-143` |
| 7 | Fix `<param>` doc mismatch on `ViewportInterestSet` | Nitpick | Bundle with #6 |
| 8 | Correct "8 scenarios" → "7 scenarios" (or add an 8th) across `JIRA.md`/`active-tasks.md`/`ICW-144` | Nitpick | Bundle with next `ICW-144` touch |
| 9 | Add a benchmark for alternating stale/visible queue shapes; formalize the O(n·k) promotion-scan risk | Performance/Testing | New sub-ticket under `ICW-144`, e.g. `ICW-144-QUEUE-ALGORITHM` |
| 10 | Replace `Queue<T>` with a priority-queue or two-queue split once #9's benchmark quantifies the cost | Design | Follow-on to #9, matches council's own stated trigger condition |
| 11 | Log (don't silently swallow) exceptions in `DispatchCompleted`/`DispatchFailed` | Robustness | Small, low-risk fix — any next touch of `TileWorkCoordinator.cs` |

---

*Audit performed against commit `b8d95ddab66b33d808a97b6b40cdd2d4bfdbf74c` and its full tree at that SHA. Confidence levels are stated per finding; all code-line references were read in full at this commit rather than inferred from the diff alone.*
