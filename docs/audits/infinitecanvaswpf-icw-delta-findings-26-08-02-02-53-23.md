# InfiniteCanvasWPF — Delta Report: ADR Cross-Reference Findings, Including a Self-Correction

**Previous reports:** seven prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round finishes reading the remaining ADR documents (`0001`–`0003`, `0005`; `0004` and `0006` were read last session) and cross-checks their decisions against findings from earlier reports in this series.

---

## 1. Self-correction: my third report's recommendation to delete `IBackgroundTileSource` was wrong — retract it

**My third report** (`ICW-018` dormant-interfaces section) recommended: *"delete `IRenderer`/`ViewportRenderRequest`/`IBackgroundTileSource` outright (genuinely zero-consumer, zero test coverage, **no ADR references them**)."* That parenthetical was based on not having read `docs/ADR/` at the time — I inferred ADR-0005's likely scope from `ICW-076`'s ticket text alone, without reading the ADR itself.

Having now read **ADR-0005: Source-Agnostic Background Tile Mip Requests** in full, that claim is **false**: the ADR names `IBackgroundTileSource` explicitly and repeatedly as a core piece of its Decision — *"`IBackgroundTileSource` asynchronously resolves a request and may represent a synthetic generator, local file decoder, memory cache, HTTP client, or server-side tile service..."* — alongside `BackgroundTileDescriptor`, `BackgroundTileRequest`, and `BackgroundTilePayload`, all four of which the ADR defines as the target contracts for an explicitly still-`Proposed`, still-in-progress migration. **These four types are not dead scaffolding left over from an abandoned idea — they are the as-designed target shape of an active architectural decision whose migration (see §2 below) simply hasn't reached the step that would start using them.** Deleting `IBackgroundTileSource` would delete part of a documented architecture decision, not clean up orphaned code.

**Correction:** `IBackgroundTileSource`, `BackgroundTileDescriptor`, `BackgroundTileRequest`, and `BackgroundTilePayload` should be **kept**, with a comment or doc-link to ADR-0005 explaining why they currently have no production callers. `IRenderer<TScene,TOutput>` and `ViewportRenderRequest` remain safe deletion candidates — now that all six ADRs have been read, I can confirm neither is mentioned in any of them, so my third report's assessment of *those two specifically* stands.

**Confidence:** 95% (ADR-0005 read in full and quoted directly; the original report's claim was a search-completeness failure, not a misreading — I simply hadn't read the file that would have contradicted it).

---

## 2. ADR-0005's Implementation Sequence explicitly designed the fix for my fifth report's pixelometer finding — the fix path was already specified, it's just unstarted

My fifth report found that `MainWindow.TryReadPixelValue` (the pixelometer) triggers real tile generation as a side effect of mouse hover, via `SampleImageTile.TryGetPixelsNonBlocking` → `EnsurePixelsGenerationStarted`, violating the requirements registry's mandatory "must never initiate tile acquisition" clause. At the time, I recommended creating a tracked ticket for the deferred long-term fix, since none existed.

**ADR-0005 already specifies exactly this fix, in its own words**, independent of anything in my prior report: *"Pixel inspection is a separate source-neutral request at mip zero. Until a dedicated point-sampling capability is introduced, it may use the same asynchronous materializer and show the established placeholder while the native payload is unavailable; **hover handling must not synchronously decode or generate a native tile on the UI or raster thread**."* And its Implementation Sequence, step 4: *"Migrate `SampleImageTile` into descriptor-plus-annotation scene data, **migrate pixelometer reads to an explicit mip-zero materializer request**, and migrate renderer sampling to resident payload dimensions..."*

This means the fifth report's finding isn't an undiscovered problem needing a new ticket from scratch — it's the **directly observable symptom of ADR-0005's migration being stalled at an early step**. `ICW-076` (the ticket implementing this ADR, status "In Progress" per my third report) has evidently completed the policy/type-definition layer (the mip-selection policy class, the now-correctly-preserved `IBackgroundTileSource` family) but not step 4, the actual migration of `SampleImageTile` and the pixelometer off the old owned-delegate model. **Recommendation: link my fifth report's finding directly to `ICW-076` and ADR-0005 step 4, rather than treating it as a standalone gap requiring its own new ticket** — a new, disconnected ticket would risk producing a second, competing fix for the same problem ADR-0005 already designed a solution for.

**One severity nuance worth being precise about**, since ADR-0005's wording is stricter than the actual current violation: the ADR guards against hover handling that would *"synchronously decode or generate a native tile on the UI or raster thread"* — i.e., blocking. My fifth report already confirmed `EnsurePixelsGenerationStarted` is asynchronous (submits to the coordinator's queue and returns immediately; the mouse-move handler never blocks). So the current violation is real but is the *milder* of the two failure modes ADR-0005's language covers — "still triggers untracked-by-design async work," not "blocks the UI thread." Worth stating precisely so the eventual fix's priority is calibrated correctly against the ADR's own stated concern.

**Confidence:** 90% (ADR-0005's text quoted directly and compared against the exact call chain already traced in report 5; the "milder failure mode" distinction is a direct re-read of report 5's own findings against the ADR's wording, not new code investigation).

---

## 3. ADR-0003 states the exact architectural goal my fourth report found `ISpatialIndexService<T>` already violates

My fourth report (§2.3) found `CanvasViewportViewModel<T>` downcasts to the concrete `LiveSpatialIndexService<T>` type (`is LiveSpatialIndexService<T> liveSpatialIndexService`) to read `LastPublishedAtUtc`, because `ISpatialIndexService<T>`'s two-member interface doesn't expose it — and characterized this generically as a "shallow interface" / leaky-abstraction code smell.

**ADR-0003: Live Hybrid Spatial Indexing** states the interface's intended purpose explicitly: *"`ISpatialIndexService<T>` remains the abstraction boundary so alternate builders (STR-tree, linear, domain-specific binning) can be evaluated **without changing rendering or camera code**."* `CanvasViewportViewModel<T>` is exactly "rendering/camera code" in this sense — it's the consumer this ADR's abstraction boundary is meant to insulate from implementation swaps. The downcast found in report 4 is a direct, concrete violation of this ADR's stated goal, not merely a generic interface-design nitpick: **if the STR-tree or linear builder were ever substituted for `LiveSpatialIndexService<T>` in this ViewModel's construction, `LastSnapshotPublishedAtUtc` would silently and permanently stay null with no error** — precisely the kind of implementation-swap breakage ADR-0003 exists to prevent.

**Recommendation:** upgrade report 4's finding from a generic code-smell observation to an explicit ADR-0003 compliance gap; the fix recommendation from report 4 (add `LastPublishedAtUtc` to the interface, nullable, or a narrow opt-in `ISpatialIndexSnapshotInfo`) stands unchanged, but now has a concrete architectural document to cite as justification rather than general software-design principle.

**Confidence:** 90% (ADR-0003 read in full and quoted directly; report 4's original code trace was already at 95% confidence and is unaffected by this cross-reference — only the framing/justification is strengthened).

---

## 4. Corrections Summary Table

| Ticket / Doc / Prior Report | Current status/claim | Correction | Basis |
|---|---|---|---|
| My 3rd report (`ICW-018` section) | Recommended deleting `IBackgroundTileSource` alongside `IRenderer`/`ViewportRenderRequest`, stating "no ADR references them" | **Retract for `IBackgroundTileSource`** — ADR-0005 names it explicitly as a live architectural target. Keep the deletion recommendation for `IRenderer`/`ViewportRenderRequest` only, now confirmed absent from all six ADRs. | §1 |
| My 5th report (pixelometer "must never initiate acquisition" finding) | Recommended creating a new tracked ticket for the deferred long-term fix | **Redirect, don't duplicate**: link to `ICW-076`/ADR-0005 step 4 instead of a fresh ticket — this is that migration's unstarted step, not a separate problem. Also: clarify the current violation is the async (milder), not synchronous/blocking (ADR's stricter concern), variant. | §2 |
| My 4th report (`ISpatialIndexService<T>` shallow-interface finding) | Framed as a generic interface-design code smell | **Strengthen**: this is a direct violation of ADR-0003's explicitly stated abstraction-boundary goal, not just a style preference. Fix recommendation unchanged. | §3 |

---

## 5. Assumptions & Open Questions

- I have now read all six ADRs in `docs/ADR/`; I did not separately re-verify `ICW-076`'s exact current implementation state against every clause of ADR-0005's five-step Implementation Sequence (only steps discussed in §2 above) — a future session could productively do a full step-by-step checklist verification the way this report did for one step.
- ADR-0001 and ADR-0002 (both status **Accepted**, unlike the other four which remain **Proposed**) were read in full this session and found consistent with everything already verified in prior sessions — no new findings from those two, noted here so a future session doesn't need to re-read them looking for something that isn't there.
- Open question, process-level: given this session found one prior report's own recommendation needed retracting once more source material (the ADRs) was read, should future sessions in this series read `docs/ADR/` and `docs/requirements/` as a mandatory first step before making any "delete this" or "this is dead code" recommendation, rather than treating architecture-doc discovery as opportunistic? This would have prevented the report-3 error rather than requiring a later correction.

---

*Methodology note: this session completed reading `docs/ADR/` (the two remaining unread files from last session, `0001` and `0002`, plus `0003` and `0005`) and, for each of the four `Proposed`-status ADRs, checked whether its stated Decision or Implementation Sequence bore directly on any finding already made in this report series — three did, including one case where the cross-check revealed a prior report's own recommendation was based on an incomplete search and needed retracting.*
