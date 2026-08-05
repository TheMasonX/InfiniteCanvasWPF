# InfiniteCanvasWPF — Delta Report: Ticket-Corpus Data Integrity Findings and a CameraSnapshot Divide-by-Zero Confirmation

**Previous reports:** fourteen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. This round reads the `ICW-301`–`308` ticket family (a design/primitive-obsession-focused batch not previously read in full) and directly verifies one of its claims against source rather than assuming the ticket text is accurate.

---

## 1. New finding: `ICW-307`'s YAML frontmatter has a literal duplicate `status:` key with conflicting values

**Checked directly, not assumed.** `docs/tasks/tickets/ICW-307-bgra32-overflow.md`'s frontmatter contains two `status:` keys:
```yaml
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-26
status: Done
```
This is a real, machine-checkable data-integrity defect in the ticket corpus itself — a YAML parser reading this frontmatter will resolve the duplicate key according to its own last-value-wins or first-value-wins convention (most common YAML parsers, including Python's `PyYAML` and most JS/TS YAML libraries, take the **last** occurrence, which would resolve to `Done` here — consistent with the file's own "Work completed" section further down, so the *practical* outcome is probably accidentally correct, but this is happenstance, not something to rely on). Any tooling that validates frontmatter strictly (a JSON-schema-style linter, or a stricter YAML parser that rejects duplicate keys outright) would fail on this file. The same file also has its **entire two-line "Validation commands:" block duplicated verbatim**, immediately following itself — a smaller instance of the same paste-merge sloppiness.

**Recommendation:** fix the frontmatter to a single `status: Done` key, and de-duplicate the validation-commands block. More broadly: whatever process eventually executes `ICW-081`'s ticket-corpus reconciliation should include a machine-checkable frontmatter-validity pass (parse every ticket's YAML strictly, reject duplicate keys) alongside the already-known duplicate-*ID* problem — this is a different failure mode (one file, internally malformed) than the duplicate-ID problem (two files, same ID), and neither `ICW-081`'s existing scope nor `scripts/Validate-TaskTracker.ps1` (per its own ticket, `ICW-084`) has been confirmed to catch this specific case.

**Confidence:** 98% (the frontmatter is quoted verbatim from the file; this is a direct textual fact, not an inference).

---

## 2. New finding, smaller: `ICW-306` has the same duplicated-validation-commands pattern

`docs/tasks/tickets/ICW-306-pixel-format-assumptions.md` also has its "Validation commands:" block repeated verbatim, immediately following itself (no frontmatter duplication this time, just the body-section repeat). Combined with `ICW-307`'s instance, this is now two confirmed occurrences of the same authoring pattern — likely both tickets were updated via the same script or copy-paste workflow when their "Work completed" sections were appended, and that workflow duplicates the trailing section instead of replacing it. Worth a quick corpus-wide grep for `Validation commands:.*Validation commands:` (or similar) as part of whatever process fixes `ICW-307`, since there may be more instances among the ~90 ticket files not individually read this session.

**Confidence:** 95% (text quoted directly from the file).

---

## 3. Correction, reiterated: `ICW-305`'s eviction-policy mischaracterization is still present in the ticket text, unfixed since my fourth report (over a dozen sessions ago)

`docs/tasks/tickets/ICW-305-tilecache-eviction-policy.md` still states, unchanged: *"`TileCacheBudget.TrackTile` currently evicts dictionary-first entries which is unpredictable."* My report 4 (session 4 of this series) already corrected this: `TileCacheBudget.TryReserve` implements a real, if simple, policy — it prefers evicting tiles that have already been **generated** but are no longer claimed, over ungenerated ones, falling back to dictionary order only among ties. This is not "dictionary-first" as the ticket claims; it's "generated-and-unclaimed-first, dictionary-order as a tiebreaker." I'm re-flagging this now specifically because **it has had multiple opportunities to be corrected since** (this ticket family was presumably in scope for at least one of the several council/synthesis passes that have happened since report 4) and the text is still exactly as it was. Recommend this specific correction be applied directly to `ICW-305`'s Summary section this time, rather than left in an audit report for a future pass to rediscover a third time.

**Confidence:** 90% (re-confirmed by reading `ICW-305`'s current text directly this session and comparing against my own report 4's already-verified code-level finding, which was not independently re-derived this session but has had no code changes in the relevant file since).

---

## 4. Extension: `ICW-301`'s `CameraSnapshot` divide-by-zero concern, confirmed with the exact mechanism and calibrated real-world risk

`ICW-301` (Proposed) describes the concern only generically: *"Public `record struct` types such as `CameraSnapshot` can be default-initialized, producing invalid state (e.g., zero scale) that causes division-by-zero or NaN propagation."* I checked `CameraSnapshot` directly rather than taking this at face value:

```csharp
public readonly record struct CameraSnapshot(double ScaleX, double ScaleY, double OffsetX, double OffsetY)
{
    public SpatialBounds GetViewportBounds(double screenWidth, double screenHeight)
    {
        // ... input validation on screenWidth/screenHeight only ...
        return new SpatialBounds(
            -OffsetX / ScaleX,
            -OffsetY / ScaleY,
            screenWidth / ScaleX,
            screenHeight / ScaleY);
    }
}
```

**Confirmed: exactly four unguarded divisions by `ScaleX`/`ScaleY`, and no guard anywhere in the type against `ScaleX == 0` or `ScaleY == 0`.** `default(CameraSnapshot)` (all four fields `0.0`) would produce `-0.0/0.0 = NaN` for both offset terms and `±Infinity` for both extent terms, all four silently flowing into a `SpatialBounds` with `NaN`/`Infinity` fields — no exception, no validation, values that would then propagate into whatever spatial query or tile-selection logic consumes that bounds next.

**Calibrating real-world risk, since the ticket doesn't:** I checked how `CameraSnapshot` actually reaches the app. Every occurrence in `MainWindow.xaml.cs` receives it as a method **parameter**, always sourced from a live `CameraTransform` instance's own snapshot — and `CameraTransform`'s internal state defaults to `TransformState.Identity = new(1, 1, 0, 0)` (scale 1, not 0) per its own static initializer. So the current production call graph never actually constructs a bare `default(CameraSnapshot)` and hands it to `GetViewportBounds`. **The defect is real and exactly as described; the current practical exposure is low, because nothing in the shipped code path currently default-constructs this type.** The risk is forward-looking: any future test, benchmark, or refactor that constructs `new CameraSnapshot()` or forgets to initialize scale before calling `GetViewportBounds` would hit this with no compiler or runtime warning. This is worth stating precisely in the ticket rather than leaving the severity implicit — it changes this from "fix urgently" to "cheap, worth doing opportunistically, not a live production bug."

**Confidence:** 95% (the four unguarded divisions and the type's complete lack of validation are directly confirmed by reading the full 32-line type definition; the "low current practical risk" conclusion is based on tracing every `CameraSnapshot` call site in `MainWindow.xaml.cs`, not an exhaustive whole-solution call-graph analysis).

---

## 5. Corrections Summary Table

| Ticket | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-307` | Frontmatter and body both show signs of a sloppy merge | **New finding**: literal duplicate `status:` YAML key (`Proposed` then `Done`) plus a duplicated validation-commands block. Fix the frontmatter; add a corpus-wide check for the same pattern elsewhere. | §1 |
| `ICW-306` | Body has a duplicated section | **New finding** (smaller): duplicated "Validation commands:" block, same authoring pattern as `ICW-307`. | §2 |
| `ICW-305` | Proposed; describes eviction as "dictionary-first" | **Re-flag existing correction**: still inaccurate as of this session, unchanged since my report 4 corrected it. Recommend applying the fix to the ticket text directly this time. | §3 |
| `ICW-301` | Proposed; generic "e.g., zero scale" description | **Extend with confirmed mechanism**: exactly four unguarded divisions in `GetViewportBounds`, confirmed via direct read. **Calibrate severity**: no current production call site constructs a default/zero-scale instance — risk is forward-looking (tests, benchmarks, future refactors), not a live bug today. | §4 |

---

## 6. Assumptions & Open Questions

- I read the full `ICW-301`–`308` batch (8 tickets) this session but only independently re-verified `ICW-301` and `ICW-305` against source directly (both required for the findings above); `ICW-302`, `ICW-303`, `ICW-306`, `ICW-308` were read but not independently re-checked against current code this session — `ICW-302` and `ICW-306` both show "Done" with specific "Work completed" notes that read as plausible and consistent with prior sessions' reads of `ZeroCopyBitmapFactory.Windows.cs`/`DefectTemplateFactory.cs`, but weren't re-diffed line-by-line this session.
- `ICW-303` (P/Invoke `SafeHandle` hardening) remains Proposed and unverified this session — it targets the same `ZeroCopyBitmapFactory.Windows.cs` file this series has read in full multiple times, so a future session could check its specific claims (`DangerousGetHandle` usage, `SetLastError` annotations) quickly using material already on file rather than a fresh read.
- Open question: given this is now the second instance (after `ICW-305`) of an audit-report correction not making it back into the ticket text across multiple sessions, would it be more effective for future sessions in this series to propose the exact corrected ticket text as a ready-to-paste diff, rather than a prose description of the correction, to lower the friction of actually applying it?

---

*Methodology note: this session read all eight tickets in the `ICW-301`–`308` batch directly rather than relying on any prior report's characterization of them, then verified two specific claims against current source (`CameraSnapshot`'s full type definition for `ICW-301`; `TileCacheBudget`'s eviction logic, cross-checked against my own report 4's already-established finding, for `ICW-305`) before writing anything up. The two frontmatter/duplication findings in §1–§2 were discovered incidentally while reading the tickets for content, not from a targeted search — a corpus-wide grep for the same pattern was recommended but not performed this session.*
