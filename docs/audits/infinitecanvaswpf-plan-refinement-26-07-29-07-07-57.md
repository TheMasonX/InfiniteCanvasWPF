# ICW Compatibility Plan — Refinement Pass

**Subject document:** the compatibility architecture plan document ("Changes Needed to Make ICW Suitable as a Reusable Production Viewport Engine") — read in full this pass (all 1922 lines/22 sections), cross-checked against current HEAD (`139a8b6`) and against the other three uploaded documents.
**Prior output:** `infinitecanvaswpf-external-audit-validation-26-07-29-06-45-13.md` (claim-by-claim validation). This report is additive — concrete, section-numbered edits to the plan itself, not a restatement of that validation.

**Overall assessment first, since it changes the shape of useful feedback:** the plan is genuinely comprehensive at the specification level. Nearly every gap this audit series independently found already has a corresponding section, contract, or acceptance criterion somewhere in the plan's 22 sections (readback determinism, surface leases, physical-concurrency correctness, versioned spatial entities, overlay batching are all already specified correctly). The refinements below are not "the plan is missing X" so much as three narrower things: (a) one place where a *different* uploaded document already contains a better answer than this plan does, and the two should be reconciled; (b) two specific, still-open code-level facts this audit series has now confirmed that the plan's existing acceptance criteria should name explicitly rather than leave implicit; (c) one gap the plan genuinely doesn't cover anywhere.

---

## Refinement 1 — Adopt Integration-1's Phase 0/1 sequencing instead of this plan's own Phase 1–6 order

**Where:** §21, Recommended Implementation Order.

The plan's own sequencing puts "Package and Boundary Cleanup" in Phase 1 and defers "Cancellation and Scheduler Hardening" — the claimant-token, active-count-timing, and queued-work-stranding fixes — to Phase 6, after Phases 2–5 have already built `SceneSnapshot`, `RenderFrameSnapshot`, source adapters, the layer graph, and the lease-based resource model on top of the coordinator as it exists today.

The independent bug audit report document — one of the three other documents in this same upload — already proposes the better order, and it isn't reflected here:
> **Phase 0 — Safety Harness Before Refactor:** render publication stale-generation rejection; physical concurrency cap under cancellation; queued work draining after running cancellation; pixelometer does not initiate tile acquisition; old scene survives failed regeneration; old published surfaces do not mutate after new publication; live spatial index replacement/move/delete semantics; memory lease exactly-once release accounting.
> **Phase 1 — Fix P0/P1 Correctness Before UI Scalability:** enable frame/viewport claimant IDs instead of `DefaultCoordinatorClaimant`; change active count to represent physically executing factories only; make native tile generation cooperatively cancellable; add stale-frame rejection; make regeneration transactional; convert readout to published-frame readout.

This audit series independently confirmed all three of the coordinator-specific items in that list (claimant tokens hardcoded to `CancellationToken.None`; `_activeCount` decremented before physical work stops; queued items with stale tokens not pulled by `DrainQueue`) are real, present at current HEAD, and — per the second external audit's own `ICW-BUG-001/002/003` — independently confirmed by yet another reviewer. These are bugs in *existing* code, not missing architecture; none of them require any of the new types this plan proposes to fix. Building five phases of new abstraction on a coordinator whose admission accounting is already known to be wrong means every one of those new abstractions (`PublishedSurfaceLease`, `IMemoryGovernor`, the layer graph's invalidation tracking) inherits an unreliable foundation for however long Phases 2–5 take, purely because of sequencing, not because the new designs are wrong.

**Concrete edit:** replace this plan's Phase 1 with Integration-1's Phase 0 + Phase 1 (merged), and renumber the plan's existing Phase 1 ("Package and Boundary Cleanup") to Phase 2. This costs nothing — none of Integration-1's Phase 0/1 items require the assembly split this plan's original Phase 1 does, so they can proceed in parallel with, or entirely before, any packaging work.

---

## Refinement 2 — Generation-time GDI+ usage is a distinct, unaddressed touchpoint from the one `R-004` already covers

**Where:** §13 (Render Planner and Compositor Split) or a new subsection under §11 (Resource Ownership and Memory Governance).

Integration-1's `R-004 — Remove GDI Lock From DrawDefectPatch` addresses one real GDI+ touchpoint: `DrawDefectPatch`'s `Bitmap.LockBits` call in the per-frame *render* hot path. That is a different call site from one this audit series found independently (pass 12 §2): `SampleImageGenerator.ApplyDetailsWithGdiPlus`, which creates a `Bitmap`/`Graphics`/`SolidBrush` and calls `FillEllipse`/`LockBits` during tile *generation* — code that now runs inside the `TileWorkCoordinator`'s concurrent `Task.Run` factories, up to `DefaultMaxConcurrency` at a time (and, per Refinement 1's confirmed bug, potentially more than that during a cancellation burst). Neither this plan nor any of the other three uploaded documents mentions this second call site — R-004's fix, if implemented exactly as written, would leave it completely untouched.

**Concrete edits:**
- Add an explicit line to §11 or §13's rules: *"Any platform-interop drawing (GDI+, or an equivalent on other platforms) used inside a generation factory is subject to the same physical-concurrency guarantees as the scheduler itself — verify this explicitly, don't assume managed-code concurrency limits imply platform-interop concurrency limits."*
- In §18.2's Integration Tests list, sharpen "Cancellation storm with temporarily non-cooperative factories" to specifically use the real circle-stamping/GDI+ factory as the test subject, not a synthetic delay — it's the actual non-cooperative code in this codebase today (confirmed: the mip-level factory only checks cancellation *after* the expensive work completes, not during it), so testing against a mock that cooperates better than production code would pass without proving anything about the real risk.

---

## Refinement 3 — Two independent instances of the same settings-consistency gap; make the pattern explicit in §16

**Where:** §16 (Settings and Policy Model), §16.2 Acceptance Criteria.

This audit series (pass 9) found `CanvasUserSettings.IsValid` never checks `ObjectsPerTile`'s upper bound, while the generator it feeds enforces one via a hard `throw` — a settings file can carry a value that passes file-load validation and then throws during the very next scene generation. Independently, Integration-1's `R-007` found a *different* instance of the same general failure mode: `CanvasUserSettings` declares and validates `MinimumSparseTilePixelSize`, but `MainWindow.RenderFrameAsync` never actually passes a configured value into `GenerateFrozenBitmap` — the setting is validated and stored but not consumed where it matters. Two independently-discovered instances of "a setting is checked in one place but not reconciled with where it's actually used" is worth treating as a pattern the new architecture should structurally prevent, not two unrelated one-off bugs to patch individually.

**Concrete edits to §16.2's Acceptance Criteria:**
- Add: *"Every declared option field has exactly one validation function, used identically by the UI input path, the persisted-settings load path, and any host-adapter translation path — not independently re-implemented per entry point."*
- Add: *"Every declared option field is demonstrably read somewhere in the actual render/generation call graph — add a test that would have caught `MinimumSparseTilePixelSize` never reaching `GenerateFrozenBitmap`, and one that would have caught `ObjectsPerTile`'s upper bound being absent from file-load validation."* Naming both known historical instances in the test's own comments/description is worth doing explicitly, since both are concrete, already-diagnosed regressions rather than hypothetical risks — a future implementer shouldn't have to rediscover either from scratch to know what the test is guarding against.

---

## Refinement 4 — Name the concrete `PixelCost`/mip-undercounting bug as the regression `§11.4`'s criterion already targets

**Where:** §11.4 Acceptance Criteria, and §18.1 Required Unit Tests.

§11.4 already lists *"Mip memory is counted separately from native tile memory"* as an acceptance criterion — the specification is already correct. What's missing is naming the concrete, already-confirmed defect this criterion needs to fix: `SampleImageTile._pixelCost` is computed once at construction from mip-0 dimensions only (`checked(pixelWidth * pixelHeight)`) and never revised as additional mip levels get cached — `TileCacheBudget.UsedBytes` can undercount actual resident memory by up to ~33% once a tile has accumulated several mip levels (a geometric series of quarter-sized mips converges to 4/3× the base cost), which is the steady state any reasonably long viewing session reaches.

**Concrete edit:** add `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative` to §18.1's Required Unit Tests, with the `PixelCost` computation above cited as the specific defect it supersedes — the plan's own `ResourceKey` design (§6.1, which already includes an `int? MipLevel` field) is the right foundation for this fix, since the current bug exists specifically because `TileCacheBudget.TryReserve`/`Release` are keyed by `tile.Id` alone rather than by a key that includes mip level. As long as whichever `IMemoryGovernor` implementation eventually lands reserves bytes per `ResourceKey` (including mip level) rather than collapsing back to one reservation per tile the way the current code does, this is fixed as a natural consequence of following the plan's own already-correct design — the risk is only in an implementation that technically uses the new `ResourceKey` type but still aggregates by tile out of habit, silently reproducing the current bug in a fancier-looking wrapper.

---

## Refinement 5 — Add an explicit note on migrating a codebase that's still under active, rapid iteration

**Where:** new subsection, §21 (Recommended Implementation Order), or as a preface to it.

Nothing in the plan addresses a practical risk specific to *this* codebase's current state: it is under very active, rapid iteration (this audit series' own timeline: a full coordinator subsystem built and hotfixed five times within roughly an hour, with 13 further commits landing before this document review even began). An 8-phase, 5-assembly migration proposed against a moving target risks the target moving faster than the migration in its early phases, or new feature work (e.g., further generator/background-noise parameters) landing mid-migration built against the *old* `MainWindow`-owned contracts, creating more to migrate later rather than less.

**Concrete edit:** add an explicit sequencing note: either (a) freeze new demo-app feature work for the duration of Phases 1–2 (the boundary and snapshot-core phases, where the surface most feature work would touch is being restructured), or (b) if feature work can't pause, mandate that anything landing during the migration be built directly against the emerging new contracts (even in skeleton form) rather than against `MainWindow`'s current mutable-field model — a "strangler fig" constraint that keeps the migration target from moving away from the migration itself. Given this plan is meant to be handed to whoever implements it, this is worth stating explicitly rather than assuming it's obvious — the other three uploaded documents don't address it either, and it's a real risk specific to how this particular codebase has actually been behaving, not a generic migration-planning platitude.

---

## Summary of concrete edits, by plan section

| Plan section | Edit |
|---|---|
| §21 (Implementation Order) | Replace Phase 1 with Integration-1's Phase 0 + 1 (merged); renumber current Phase 1 to Phase 2 |
| §11 / §13 | Add explicit platform-interop-concurrency rule; name `ApplyDetailsWithGdiPlus` as a second GDI+ touchpoint beyond `R-004`'s `DrawDefectPatch` |
| §16.2 | Add single-validation-function and field-is-actually-consumed acceptance criteria, citing both `ObjectsPerTile` and `MinimumSparseTilePixelSize` as the motivating regressions |
| §11.4 / §18.1 | Add `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative`, citing the `_pixelCost` mip-0-only computation as the specific defect |
| §21 (new preface) | Add active-development migration-sequencing note (freeze feature work or strangler-fig new work against emerging contracts) |

## Assumptions & Open Questions

- Refinement 1's recommendation assumes Integration-1's Phase 0/1 item list is itself complete for "fix existing bugs before building new architecture" — this audit series independently confirmed the coordinator-specific items on that list, but did not independently re-verify every item on it (e.g., "old published surfaces do not mutate after new publication" maps to this series' own still-open front/back-buffer finding, confirmed plausible but not empirically reproduced).
- Refinement 5 is a process/sequencing recommendation rather than a technical one — its value depends on factors (team size, how much feature work is actually planned during the migration window) this review has no visibility into; included because the pattern (rapid iteration observed directly during this audit) is a fact this review can attest to, even though the right response to it is a judgment call for whoever owns the schedule.
- As with all prior passes, this is static source and document review only — no build or test execution was performed.
