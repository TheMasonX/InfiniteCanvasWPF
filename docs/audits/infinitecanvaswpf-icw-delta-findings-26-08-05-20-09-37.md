# InfiniteCanvasWPF — Delta Report: C20 and C23 Independently Verified — the "Stale" Rejection Pattern Is Systemic, Not Isolated to C11

**Previous reports:** twenty-two prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. Per last session's flagged priority, this round reads `icw-wave-e-audit-delta-5.md` (the other agent's Wave E series, previously unread by this series) and independently re-verifies C20 and C23 — the two Wave-E-originated claims the council rejected as "stale" alongside my own C11 — against the current, post-Wave-G codebase.

---

## 0. What this session found, stated plainly up front

The council's reconciliation review rejected four claims as "stale" in one line: *"Reject stale C10, C11, C20, and C23."* This series has already pushed back on C11 (report 13) without an explanation ever being given. This session finds that **the other agent's own audit series independently raised the identical objection, for C20 and C23, before this series ever looked at them** — meaning three of the four claims in that single rejection line have now been checked (by one series or the other, or both) and found not to support "stale" as a factual description at the time it was applied. This isn't three isolated disagreements; it's one systemic pattern in how that specific council line used the word "stale," now confirmed from two independent directions.

---

## 1. `icw-wave-e-audit-delta-5.md` already flagged this exact pattern for C20/C23 — confirmed by reading it directly

The document states, re-verifying against `4467593` (the HEAD at the time it was written): *"C20 (`DrawDefectPatch` reads `DefectBitmap` into an unused local) and C23 (pixelometer fallback allocates/sorts under lock) were rejected as 'stale' by the Implementation and Runtime Reviewer seat. Re-verified against the current HEAD... both code paths are byte-for-byte unchanged from when originally reported... 'Stale' doesn't appear to mean 'already fixed' here, since neither has changed."* This is the same observation report 13 made about C11, reached independently by a different audit series checking different claims. The document is careful and appropriately restrained about it — explicitly declining to resurface C20/C23 as new findings, just flagging the record. I'm treating that same discipline as the standard for this report too: not re-litigating the underlying bugs as new, only tracking what "stale" actually meant against the code.

---

## 2. Re-verified against the current (post-Wave-G) HEAD: C20 is now genuinely fixed — but not at the time it was rejected, and not because anyone fixed C20 specifically

I checked `ZeroCopyBitmapFactory.Windows.cs` directly for C20's cited line, `sourceRow[sourceX * 3]`. **It's gone.** The current code reads `sourcePixels![sourceRowOffset + sourceX]` — single-byte Gray8 indexing, no `* 3` stride, no unused local. This matches what report 22 (this series, previous session) already confirmed independently while tracing a different historical finding (`pass6`'s finding #3/#5): `ICW-321` ("dead `DefectBitmap` LockBits sampling removed from `DrawDefectPatch`," Wave G) removed the entire dead-sampling code region this line lived in.

**The precise, honest framing matters here:** C20's rejection as "stale" was **factually wrong at the moment it was made** — confirmed wrong by the other agent's own re-check at `4467593`, and it would have been equally wrong if checked at any point before Wave G landed. It only became *retroactively* true, and only as an incidental side effect of `ICW-321` deleting the entire surrounding dead-code block for reasons unrelated to C20's ticket ID — nothing in `ICW-321`'s own text or the Wave G handoff mentions "C20" or references the Wave E audit series at all (checked in report 21; `ICW-321`'s described motivation is the same dead-code-removal thread `pass6`'s finding #5 and `pass5` originated, from this series' own backfilled history, not C20). So: C20's specific ticket was never actually "fixed" as a deliberate response to being rejected — it just happened to be swept up by unrelated cleanup that eliminated the code it pointed at. This is a different, milder failure mode than C11/C23's rejections (§3) — worth distinguishing precisely rather than treating all three the same way.

**Confidence:** 95% (both the current absence of the cited line and its provenance via `ICW-321` are directly confirmed; the "not deliberately fixed in response to C20" conclusion is based on the absence of any cross-reference to C20 or the Wave E series anywhere in Wave G's documentation, which is evidence of absence rather than a definitive negative proof).

---

## 3. Re-verified against the current (post-Wave-G) HEAD: C23 remains genuinely open, and the newly-added "clean" pixelometer path routes through it too

C23 describes the pixelometer's mip-fallback path allocating a `List` and sorting it (`OrderBy`/`ThenBy`) while holding `_cacheGate`. I checked `SampleImageTile.TryGetBestResidentMip` directly: it still constructs `var fallbackCandidates = new List<(int MipLevel, byte[] Pixels)>(_mipPixels.Count + 1);` and still chains `.OrderBy(candidate => Math.Abs(candidate.MipLevel - mipLevel)).ThenBy(candidate => candidate.MipLevel)` — unchanged, exactly matching both the original claim and the other agent's `delta-5` re-check.

**What's new this session, and worth flagging specifically:** report 20/21 already confirmed `ICW-312` added `SampleImageTile.TryGetResidentPixels` as a clean, non-generation-triggering read path for the pixelometer, closing a real, separate violation (hover must never start tile generation). I traced `TryGetResidentPixels`'s own body: when the exact requested mip isn't resident, its final fallback is a direct call to `TryGetBestResidentMip` — **the same shared method C23 describes.** So the pixelometer's brand-new, purpose-built "clean" read path still bottoms out in the allocate-and-sort-under-lock pattern C23 flagged, for exactly the case (mip not yet resident) that's most likely during active panning/zooming — the same high-frequency, latency-sensitive scenario the acquisition-triggering fix was built to protect. Fixing the acquisition-triggering half of the pixelometer's problems did not touch this allocation half, because both the old and new pixelometer paths converge on the same shared fallback method.

**Recommendation:** since this is now confirmed live and reachable from the pixelometer's intended long-term path (not just a to-be-replaced old path), it's worth prioritizing above wherever it currently sits. A reasonable fix, in the same spirit as this series' other allocation-reduction recommendations: replace the `List`+LINQ construction with a direct scan over `_mipPixels` (already a small, bounded dictionary — `BackgroundTileMipPolicy.MaxMipLevel` bounds its size) tracking the best candidate by absolute mip-distance in a single pass, avoiding both the list allocation and the sort.

**Confidence:** 90% (the exact code fragment, its unchanged status, and its reachability from the new `TryGetResidentPixels` path are all directly confirmed via source read).

---

## 4. What this means for C10 and C11, tying the pattern together

With C20 and C23 now independently checked (this session, plus the other agent's own `delta-5`) and C11 already checked twice by this series (reports 6 and 13/21), the full picture for this one council line is:
- **C10**: independently spot-checked by this series in report 13 and found plausibly justified (no XML `<param>` tags exist to mismatch, so the specific claim as stated didn't apply) — the one claim in this batch where "stale" or "no defect found" would have been a reasonable call.
- **C11**: rejection unsupported then (report 13) and still unsupported now (report 21, after an entire additional hardening wave touched the same file).
- **C20**: rejection unsupported at the time (confirmed independently by both audit series), coincidentally resolved later by unrelated work.
- **C23**: rejection unsupported at the time and still unsupported now, and now demonstrably reachable from newer code than existed when the claim was first made.

Three of four "stale" labels in a single review line did not hold up under direct verification by at least one audit series, and two of those three still don't hold up today. This is enough of a pattern to treat as a process finding in its own right, not just four unrelated disagreements: whatever "stale" was intended to communicate in that specific review pass, it doesn't reliably track "the underlying code changed" or "this was already fixed" for the majority of the claims it was applied to.

---

## 5. Corrections Summary Table

| Item | Council disposition | Correction | Basis |
|---|---|---|---|
| C20 (`DrawDefectPatch` unused local) | Rejected as "stale" | **Confirmed the rejection was wrong at the time**; the underlying code has since been removed, but incidentally, by `ICW-321`, not as a deliberate response to C20. | §2 |
| C23 (pixelometer fallback allocation/sort under lock) | Rejected as "stale" | **Confirmed still open**, and now also reachable from the newly-added `TryGetResidentPixels` path — worth reprioritizing given its reach into the pixelometer's intended long-term code path. | §3 |
| C11 (`CancelWorkItem` lock contract, this series) | Rejected as "stale" | **Reaffirmed unresolved** (no new evidence this session beyond what report 21 already established). | §4 |
| The council's "stale" labeling for this batch generally | One-line rejection, no individual rationale | **Process finding**: 3 of 4 claims in this single line failed independent verification by at least one audit series at the time of rejection. Recommend the review process require a one-line evidence citation per rejected claim going forward (already suggested for C11 alone in report 21; now generalized given the batch pattern). | §4 |

---

## 6. Assumptions & Open Questions

- `icw-wave-e-audit-delta-6.md` and `-delta-7.md` remain unread this session — `delta-5` alone contained enough directly-relevant material (the C20/C23 flag) to warrant this report on its own; the other two deltas are still a priority for a near-future session.
- I did not read the two "external-audit-review" documents this session either (`addendum-26-07-30-05-30-01.md`, `and-architecture-feedback-26-07-29-21-24-17.md`) — still outstanding from the prior session's list.
- Open question, now posed with three data points instead of one: should the review/reconciliation process retroactively correct this specific line's disposition text for C20 and C23 (distinguishing "coincidentally resolved" from "still open" from "originally rejected without support"), or is a standalone audit note like this one and `delta-5` sufficient as the corrective record? I don't have a strong view — flagging it as a process question for whoever owns that reconciliation document, not a code question.

---

*Methodology note: this session read `icw-wave-e-audit-delta-5.md` in full, found it had already made the core observation this session set out to check, and then independently re-verified both of its underlying code claims (C20, C23) against the current HEAD — which has moved substantially since `delta-5` was written — rather than treating `delta-5`'s already-stated conclusion as sufficient on its own. C20 turned out to have changed status since `delta-5`; C23 had not.*
