# InfiniteCanvasWPF — Delta Report: A High-Severity, Untracked Bug Survives Two More Hardening Waves — `AddClaimant`'s Re-Coalesce Path Never Refreshes the Cancellation Registration

**Previous reports:** twenty-three prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. Continuing last session's priority, this round reads `icw-wave-e-audit-delta-6.md` (previously unread) and re-verifies its central finding against the current, post-Wave-G code.

---

## 0. Headline, stated plainly

`icw-wave-e-audit-delta-6.md` (the other agent's series) diagnosed a precise, high-severity bug in `TileWorkItem.AddClaimant`: when a claimant re-coalesces onto an already-tracked work item (the normal, expected path for any tile generation that survives more than one frame), the method updates the claimant's callbacks but **never refreshes its `CancellationTokenRegistration`** — leaving the claimant permanently bound to a registration on a token that already fired, with no way for any future token to cancel it. **I re-verified this against the current code, after both Wave F and Wave G's hardening passes through this exact method, and it is still fully present, unchanged, and untracked by any ticket.** This is worth flagging with real urgency: it silently defeats claimant-token cancellation for exactly the multi-frame generations `ICW-204` was written to fix, and it has now survived at least 16 commits (per `delta-6`'s own count at the time it was written) plus two subsequent hardening waves that specifically touched this same method for other reasons.

---

## 1. The bug, re-confirmed against current source

Current `TileWorkItem.AddClaimant` (verified by direct read this session):
```csharp
lock (_claimantLock)
{
    var existing = _claimants.Find(c => c.Id.Equals(claimantId));
    if (existing is not null)
    {
        _claimants[_claimants.IndexOf(existing)] = existing with
        {
            OnCompleted = onCompleted,
            OnFailed = onFailed
        };
        return;   // claimantToken (the newly-passed one) is never touched
    }

    // ICW-320 F-014's fix lives here, in the first-add path only:
    _claimants.Add(new ClaimantEntry(claimantId, onCompleted, onFailed, null));
    if (claimantToken.CanBeCanceled)
    {
        var registration = claimantToken.Register(() => RemoveClaimant(claimantId));
        ...
    }
}
```
`ICW-320`'s F-014 (verified in reports 21 and 23) correctly fixed the **first-add** path's ordering bug. It did not touch the **re-coalesce** path (`existing is not null`) at all — confirmed by re-reading the method in full this session; the `with { OnCompleted = ..., OnFailed = ... }` expression only updates callbacks, never `Registration`, and the stale `Registration` from the original token carries forward unchanged.

**Why this matters at the severity `delta-6` assigns it:** `MainWindow.RenderFrameAsync` replaces and cancels the shared `_frameTileCts` every single frame (confirmed by this series in earlier sessions — the two-frame-deferred CTS disposal pattern read in report 2). Every claimant token therefore fires on the very next frame after admission, regardless of whether the tile is still visible or still generating. For any generation that takes longer than one frame — which is precisely the case `ICW-204` exists to handle — the tile re-`Request()`s on the next frame with a fresh token, coalesces onto the still-running item, and hits the `existing is not null` branch above. The **original** registration (on the now-fired, frame-N token) is spent — a `CancellationTokenRegistration` on an already-canceled token never fires again — and no registration is ever created for the frame-N+1 token that was just supplied. After exactly one coalesce cycle, this claimant has no live registration on any token that will ever fire again. `delta-6` further confirmed that neither `PublishInterestSet` (skips `Running` items by design) nor `DrainQueueWithLivenessCheck` (only checks `Queued` items) can cancel it either — the token registration was the only remaining cancellation path for a `Running` item, and it's now permanently disabled for this claimant. **The tile's generation will run to completion and hold its cache reservation for its full duration no matter how far the tile scrolls off-screen**, for any generation lasting more than one frame — silently reintroducing the exact "uncancellable long-running work" failure mode this project's own ticket chain (`ICW-142` → `ICW-P1-CLAIMANT-TOKENS` → `ICW-204`) has already iterated on twice.

**Confidence:** 95% (the exact code, the exact call chain, and the absence of any fix in either Wave F or Wave G are all directly re-verified this session against current source, independent of `delta-6`'s own already-careful trace).

---

## 2. This bug is currently untracked — no ticket exists for it

A search of `docs/tasks/tickets/` for any ticket referencing `AddClaimant`'s re-coalesce path or registration refresh returns nothing. `ICW-204` itself (Done, in both `active-tasks.md` and `JIRA.md`) has a tantalizingly close but under-specified hint in its own "Next steps" column: *"Optional follow-up: avoid dooming in-flight work when a frame boundary creates a zero-claimant window."* This shows the original `ICW-204` work sensed something in this vicinity, but filed it as a vague, optional, low-priority follow-up rather than the precisely-traced, high-severity, already-confirmed-live bug `delta-6` actually found — the "optional" framing significantly undersells what's actually going on here. This gap between the two matters: an "optional follow-up" note is easy to defer indefinitely; a specifically-diagnosed, high-confidence, currently-live cancellation-defeat bug with a five-line fix already written is a very different priority.

**Recommendation:** file this properly — a new ticket (this series has no naming authority, but something like `ICW-327-addclaimant-recoalesce-registration-refresh` would fit the existing sequence) citing `delta-6`'s trace directly, with its already-provided fix:
```csharp
if (existing is not null)
{
    existing.Registration?.Dispose();
    var registration = claimantToken.CanBeCanceled
        ? claimantToken.Register(() => RemoveClaimant(claimantId))
        : (CancellationTokenRegistration?)null;
    _claimants[_claimants.IndexOf(existing)] = existing with
    {
        OnCompleted = onCompleted,
        OnFailed = onFailed,
        Registration = registration
    };
    return;
}
```
Given `ICW-320`/`ICW-322` already demonstrated exactly the right pattern for careful, documented fixes in this same method this cycle, this should be a small, low-risk, in-character addition to that same body of work — not a new investigation. `delta-6`'s own recommended regression test (hold a generation open across 3+ simulated frame boundaries via re-`Request()` with fresh tokens on the same claimant ID, then confirm cancelling the *latest* token actually cancels the work) is exactly the right shape and should be added alongside the fix.

**Confidence:** 90% (the absence of an existing ticket is confirmed by direct search; the recommended priority framing is this session's own judgment, informed by the severity already established in §1).

---

## 3. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `TileWorkItem.AddClaimant` re-coalesce path | Untracked; not touched by `ICW-320` (Wave F) or `ICW-322` (Wave G) | **Confirmed still live**, high severity — permanently defeats claimant-token cancellation for any multi-frame tile generation, exactly the case `ICW-204` was built to handle. Fix already fully specified by `delta-6`. | §1 |
| `ICW-204`'s "optional follow-up" note | Present in `active-tasks.md`/`JIRA.md` | **Correction**: this vague, low-priority note is the same underlying issue `delta-6` diagnosed precisely and rated High severity — the "optional" framing significantly understates it. Recommend replacing the vague note with a real ticket citing the precise mechanism. | §2 |

---

## 4. Assumptions & Open Questions

- `icw-wave-e-audit-delta-7.md` and the two external-audit-review documents remain unread this session — this session's entire budget went to verifying `delta-6`'s single finding thoroughly given its severity, rather than spreading across all four remaining documents thinly. Recommend `delta-7` be the very next document read, given the pattern so far (every Wave-E delta read this series has contained at least one still-relevant, independently-confirmable finding).
- I did not attempt to reproduce this bug's user-visible symptom (excess background CPU/held cache reservations during fast scroll-away) via any runtime execution — no .NET runtime is available through this session's tooling. The finding rests entirely on static code tracing, consistent with every other finding in this series, but worth stating plainly given the severity being asserted here.
- Open question: given this is now the **second** time this series (via reading a Wave-E delta) has surfaced a precisely-diagnosed, high-severity, already-fix-specified bug that neither this series nor the project's own hardening waves caught independently, is there value in a future session specifically cross-referencing every remaining unread Wave-E delta's findings against `active-tasks.md`'s "Done" rows before those rows are trusted, the same discipline this series has already applied to its own reports' claims in reports 12 and 13?

---

*Methodology note: this session read `icw-wave-e-audit-delta-6.md` in full, identified its one substantive finding as high-priority given its severity rating and precision, then independently re-verified every step of its trace against the current source — `AddClaimant`'s exact current body, confirmation that `ICW-320`'s fix didn't touch the re-coalesce branch, and a direct search for any existing ticket — before writing this report, rather than repeating the finding on the strength of `delta-6`'s own analysis alone.*
