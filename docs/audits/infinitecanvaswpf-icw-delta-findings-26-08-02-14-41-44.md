# InfiniteCanvasWPF — Delta Report: Founding DesignDoc Cross-Check

**Previous reports:** eight prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**; this round reads `DesignDoc.md`, the project's founding architecture document, for the first time in this audit series.

---

## 0. Methodology gap, disclosed plainly

`DesignDoc.md` (438 lines) sits at the repository root and is the first document `.github/agents/infinitecanvas.agent.md` instructs any contributor to read — *"DesignDoc.md for the architecture baseline... Before implementing anything non-trivial."* **This audit series never read it across eight prior sessions.** That's a real gap in this audit's own coverage, not a defect in the project, but worth stating outright rather than quietly folding in whatever this session found as if it were always part of the plan. Note also: the document's "Relevant Code" sections are illustrative example code in a generic `SpatialViz` namespace (`MapViewModel`, `GeoPoint`, `ImmutableRTreeService`) — not literal source from this repository, which uses different names throughout (`MainViewModel`, `SampleAnnotation`, `LiveSpatialIndexService`, etc.). I confirmed this before drawing any conclusions from it, to avoid comparing illustrative numbers against real implementation values as if they were meant to match (see §2 for a specific case where I checked this and it correctly turned out not to be a finding).

---

## 1. Finding: DesignDoc's founding "zoomed-out overdraw" open question maps directly onto an already-known, still-unwired code gap from my first report — but the ticket meant to own it is an empty stub

**DesignDoc.md**, under "Open Questions," poses: *"When the user applies a massive zoom-out transformation, mathematical projection dictates that millions of disparate data points will collapse onto identical physical screen pixels... Is a heatmap or accumulation buffer strategy required to prevent the CPU from wasting processing cycles over-drawing the same pixel thousands of times?"*

I initially assumed this was already addressed by the mip-level reduction system (ADR-0005/`ICW-076`), which does reduce *source sampling resolution* at low zoom. On checking more carefully, that's a different mechanism solving a related but distinct problem — it reduces how much of each tile's native pixels get sampled, not whether a tile or annotation that now covers a sub-pixel screen footprint gets rasterized at all. **The mechanism that would actually answer DesignDoc's question is the one my very first report already found dead:** `ZeroCopyBitmapFactory.DrawTile` accepts a `minimumSparseTilePixelSize` parameter (intended to skip/placeholder-render tiles below a screen-size threshold) that is **never referenced inside the method body** (confirmed unchanged across all nine sessions of this series). That finding (originally `ICW-099`/`ICW-P1-SETTINGS-VALIDATION`'s scope) is precisely the "skip over-drawing sub-pixel-sized geometry" answer DesignDoc's founding question was asking for — it just hasn't been implemented.

**There is also a dedicated ticket for this exact question that has never been filled in:** `ICW-004-zoomed-out-overdraw-spike.md` (status "Proposed"/"To Do") is an empty boilerplate template — *"Review and update the relevant implementation area... Add implementation details, blockers, or follow-up questions here"* — with zero actual scope, zero acceptance criteria specific to overdraw, and no link to `minimumSparseTilePixelSize` or `DrawTile` at all. **This is the same "empty stub ticket" pattern already found twice in this series** (`ICW-055-pixelometer-performance.md` in report 4, filled in with the pixelometer double-query finding). This is now a third confirmed instance.

**Also confirmed while locating this ticket: `ICW-004` is itself a duplicate ID** — a second, unrelated file (`ICW-004-bounded-pixel-generation.md`, about bounding `Task.Run` concurrency, a legitimate but entirely different concern already substantially addressed by `TileWorkCoordinator`'s `DefaultMaxConcurrency`) shares the same ID. This is a fourth confirmed duplicate-ID data point for the already-tracked `ICW-081` cleanup, alongside the `ICW-055`, `ICW-100`, and `ICW-064` duplicates found in earlier sessions.

**Recommendation:** fill in `ICW-004-zoomed-out-overdraw-spike.md` with concrete scope: wire `MinimumSparseTilePixelSize` through `MainWindow.RenderFrameAsync`'s call to `GenerateFrozenBitmap` (already recommended in my first report, §3.10), and implement the actual skip-below-threshold logic inside `DrawTile` using its existing, currently-ignored parameter. This closes a nine-session-old finding, answers the project's own founding design question, and gives a currently-empty ticket real content — three birds with one fix.

**Confidence:** 90% (the dead-parameter fact was already at 95% confidence from repeated direct reads across nine sessions; the connection to DesignDoc's specific framing and to `ICW-004`'s empty-stub status are both directly confirmed by reading the relevant files this session).

---

## 2. Non-finding, reported for transparency: zoom-clamp bounds do not match DesignDoc's example numbers, and this is correctly not a bug

DesignDoc's illustrative `CameraTransform` example uses `_minScale = 0.1; _maxScale = 50.0`. The actual `InfiniteCanvas.Core.CameraTransform` uses `MinimumScale = 0.0000000001` (1e-10) and `MaximumScale = 10000`, with an explicit code comment: *"The actual zoom-in/out limits are determined by the viewport size and the content bounds, which are enforced in `ClampToBounds`."* I checked this specifically because a numeric mismatch against a founding document looked, at first glance, like exactly the kind of drift this series has flagged before (e.g., the `NoiseOctaves: 3` vs. `5` drift in an earlier report). **On inspection, this is not a drift bug** — DesignDoc's numbers are illustrative round values in generic example code, not a literal specification, and the real implementation's very loose outer bounds plus a separate, tighter `ClampToBounds` mechanism is a reasonable, deliberate two-layer design (loose safety rail + content-aware practical limit). Reporting a mismatch here would have been a false positive; I'm noting the check and its negative result so a future session doesn't spend time re-deriving the same non-finding.

**Confidence:** 90% (both value sets read directly; the "not a bug" conclusion rests on the explicit code comment documenting the two-layer design, which is direct evidence rather than a charitable assumption).

---

## 3. Confirmed resolved: DesignDoc's "resize debouncing" open question is properly handled

DesignDoc's second open question asks how aggressively window-resize events should be throttled before tearing down and reallocating the unmanaged buffer. `MainWindow.OnViewportSizeChanged` already implements exactly this: a `_resizeTimer` is stopped and restarted on every resize event, and only `OnResizeElapsed` (firing after the timer settles) triggers `ClampCameraToScene()` and `RequestRenderAsync()` — a standard, correct debounce pattern. No gap here. Noted for completeness rather than as an action item, so this question is marked answered rather than re-investigated later.

**Confidence:** 90% (code read directly; did not independently verify the timer's configured interval is well-tuned, only that debouncing exists and is structurally correct).

---

## 4. Corrections Summary Table

| Ticket / Doc | Current status/claim | Correction | Basis |
|---|---|---|---|
| `ICW-004-zoomed-out-overdraw-spike.md` | Proposed/To Do, empty template | **Fill in with concrete scope**: wire and implement `minimumSparseTilePixelSize` in `ZeroCopyBitmapFactory.DrawTile` (dead parameter, confirmed since report 1) — this is the actual mechanism DesignDoc's founding overdraw question calls for. | §1 |
| `ICW-081` (duplicate-ID tracker hygiene) | Proposed | **Append data point**: `ICW-004` is a fourth confirmed duplicate ID (alongside `ICW-055`, `ICW-100`, `ICW-064`), spanning "bounded pixel generation" (concurrency) and "zoomed-out overdraw" (rendering skip) — again, unrelated topics under one ID. | §1 |
| DesignDoc.md Open Question 1 (heatmap/overdraw) | Open, unresolved | **Now traceable to a specific code fix** (§1) rather than an abstract architectural question — closing the `minimumSparseTilePixelSize` gap effectively answers it for the tile-based (not raw point-cloud) architecture this project evolved into. | §1 |
| DesignDoc.md Open Question 2 (resize debouncing) | Open, unresolved | **Confirm resolved**: `MainWindow`'s `_resizeTimer` pattern already implements correct debouncing. | §3 |

---

## 5. Assumptions & Open Questions

- DesignDoc's third open question (GPU/DirectX/`D3DImage` pivot) is a strategic technology-direction question, not something resolvable by reading more code — no finding to report either way; noted so a future session knows it was considered and correctly set aside as out of scope for a code audit.
- I did not verify the `_resizeTimer`'s configured debounce interval is well-tuned (too short risks thrashing, too long risks visible lag) — only that the debounce mechanism itself exists and is wired correctly.
- Open process question, following directly from §0: should a future session in this series treat `DesignDoc.md`, `README.md`, and `docs/tasks/JIRA.md` as a mandatory first-read checklist (mirroring what the project's own agent definition already requires of any contributor), given this session found real, connectable material in the one founding document that had gone unread for eight sessions?

---

*Methodology note: this session read `DesignDoc.md` in full for the first time in this series, explicitly checked whether its illustrative example code's numeric constants should be compared against real implementation values before doing so (avoiding a false-positive drift claim), and traced both of its two concretely-checkable open questions against current code — one resolved and confirmed compliant, the other found to connect directly to a specific, already-identified, still-open code gap from the very first report in this series.*
