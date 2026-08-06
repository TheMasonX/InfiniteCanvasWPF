# Audit Synthesis Report

**Description:** Full reconciliation of 15 requested audit reports, followed by a three-seat council review. The report preserves supported findings, corrects provenance, rejects stale claims, and updates existing tasks.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `b5e1e8b210d3bf3c79caa0366d7ce052e6883cd5`
**Latest commit:** `b5e1e8b210d3bf3c79caa0366d7ce052e6883cd5` - `Track ICW-148: FastNoise2Bindings proper submodule registration`
**ID Hash:** `AUDIT-SYNTHESIS-26-08-03-WAVE-E`
**Author:** GitHub Copilot
**Timestamp:** 2026-08-03 04:30 US Central
**Review mode:** Full reconciliation, three-seat council
**Scope:** 15 supplied audit reports, 48 extracted claims, source paths, requirements, ADR-0003 through ADR-0006, tests, benchmarks, task tickets, `active-tasks.md`, and `task-tracker.md`

## Executive Summary

The review extracted 48 claims from 15 audit reports. The council accepted 10 finding groups, rejected 2 stale claim groups, and deferred 5 evidence-dependent concerns. The highest-risk accepted finding is the pixelometer acquisition boundary: hover reads still require migration to published-frame or resident-only reads, even though interim cache accounting exists.

The review also corrected task provenance. ICW-081 is reopened because duplicate identities and stale tracker rows remain. ICW-078 is complete. ICW-144 documents seven benchmark methods, not eight. Existing ICW-188 and ICW-189 ticket files are now registered in both live trackers. No new ICW IDs were created.

The tracker validator ran after the updates. It still fails on pre-existing legacy ticket schema and status errors. It did not report the edited ICW-081, ICW-078, ICW-144, ICW-188, or ICW-189 records.

## Review Method and Coverage

The review used commit `b5e1e8b` as the fixed point. The master ledger extracted claims before verification. Three independent council seats then reviewed implementation and runtime behavior, architecture and specification alignment, and provenance and task coverage.

The review checked source, tests, benchmarks, requirements, ADRs, task tickets, and both live trackers. No source-code changes were made. Runtime reproduction was not available for hover frequency, queue retention growth, callback failure frequency, or performance impact.

## Table of Findings

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task | Sources |
| --- | --- | --- | --- | --- | --- | ---: | --- | --- |
| F-001 | Pixelometer acquisition boundary | Spec | Update | Confirmed | P1 | 99% | ICW-076, ICW-P0-PIXELOMETER-READOUT | S1, S2, S4, S8, S11 |
| F-002 | Queued work and cache ownership | Spec | Update | Confirmed | P1 | 92% | ICW-064, ICW-144, ICW-P0-ACTIVECOUNT-residuals | S3, S5, S6 |
| F-003 | Pixelometer composition policy | Spec | Update | Confirmed | P2 | 95% | ICW-035, ICW-100 | S2, S4, S7 |
| F-004 | Ticket identity and tracker provenance | Standards | Update | Confirmed | P1 | 99% | ICW-081 | S1, S3, S5, S6, S9, S12, S15 |
| F-005 | Generator option migration | Standards | Update | Partially confirmed | P2 | 91% | ICW-088, ICW-188, ICW-189 | S6 |
| F-006 | Settings default and validation ownership | Spec | Update | Confirmed | P2 | 94% | ICW-P1-SETTINGS-VALIDATION, ICW-022 | S6, S7 |
| F-007 | Benchmark surface and method count | Standards | Update | Confirmed | P2 | 99% | ICW-133, ICW-144 | S5, S9 |
| F-008 | Spatial boundary consistency | Spec | Update | Confirmed | P2 | 94% | ICW-033, ICW-064 | S9 |
| F-009 | ViewModel and README contract drift | Spec | Update | Confirmed | P2 | 96% | ICW-016, ICW-017, ICW-022 | S13, S14 |
| F-010 | ADR boundary and scheduling conformance | Spec | Defer | Partially confirmed | P2 | 92% | ICW-018, ICW-076, ICW-143, ICW-144 | S10, S11 |
| F-011 | Stale resize and persistence claims | Spec | Reject | Refuted | None | 98% | None | S12, S14 |
| F-012 | Unverified cleanup and performance extensions | Standards | Defer | Unverified | P2 | 70% | ICW-020, ICW-021, ICW-102, ICW-144 | S2, S4, S5, S6 |

## Findings

### F-001 Pixelometer acquisition boundary

**Axis:** Spec
**Provenance:** Correction and extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 99%, because the requirement, ADR, ticket, and source path agree. Runtime frequency remains unmeasured.

#### Description

The interim pixelometer path passes cache-budget accounting, but hover reads can still request tile generation when a payload is absent. The required end state consumes a published-frame snapshot or the best available resident payload and performs no acquisition on mouse movement.

#### Rationale

The requirements registry states that pixelometer reads must not acquire tiles and must use a non-blocking mip-zero path. ICW-076 repeats this boundary. The audit series identifies the remaining migration gap after the accounting fix. [functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md), [ICW-076 ticket](../tasks/tickets/ICW-076-background-tile-mip-levels.md)

#### Counter-evidence and Deduplication

The cache-accounting portion is complete under ICW-P0-PIXELOMETER-READOUT. That fix does not satisfy the separate no-acquisition contract. C24 and C25 therefore remain separate mechanisms.

#### Recommendation and Validation

Add a cold-hover test that proves no coordinator work is submitted and that a missing payload returns a defined resident or snapshot result. Route the migration through ICW-076.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-076 | Owns source-neutral mip and pixelometer migration |
| Requirement | `docs/requirements/functional-requirements-and-invariants.md` | Defines the no-acquisition invariant |
| ADR | ADR-0005 | Defines the source-neutral boundary |

#### Finding Sources

S1, S2, S4, S8, S11.

### F-002 Queued work and cache ownership

**Axis:** Spec
**Provenance:** Corroboration and extension
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 92%, because source control flow confirms the ownership concerns, but sustained runtime retention and performance impact lack soak evidence.

#### Description

Queued work can retain claimant registration until token processing, and cache admission can evict queued work unless the ownership contract explicitly cancels the work and releases its reservation. Queue scan-ahead and callback failure handling also need focused evidence.

#### Rationale

The supplied audits connect queue promotion, claimant liveness, cache admission, and callback delivery. ADR-0006 requires cancellation and reservation ownership to remain balanced. ICW-144 now tracks queue scans, allocations, callback diagnostics, and repeated measurements.

#### Counter-evidence and Deduplication

The council did not approve replacing `Queue<T>` or assigning a performance severity without benchmark thresholds. C8 remains a P2 evidence request within this P1/P2 implementation cluster.

#### Recommendation and Validation

Add tests for queued eviction, exactly-once reservation release, callback exceptions, and claimant removal. Run repeated fast-scroll benchmarks before changing the queue structure.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task | ICW-064 | Owns cache admission |
| Task | ICW-144 | Owns queue stress evidence |
| ADR | ADR-0006 | Defines scheduling and ownership intent |

#### Finding Sources

S3, S5, S6.

### F-003 Pixelometer composition policy

**Axis:** Spec
**Provenance:** Corroboration and correction
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 95%, because duplicate work and divergent composition paths are visible in source, while the correct product policy remains undecided.

#### Description

Pixelometer and renderer paths perform overlapping work and do not yet express one explicit overlap composition policy. The reports identify max-wins versus last-wins behavior and resident-mip divergence.

#### Rationale

ICW-035 already shares an overlay sampler, but the council found remaining policy and mip-selection gaps. ICW-100 remains a distinct ticket for precedence and pixelometer alignment.

#### Counter-evidence and Deduplication

The council did not choose max-wins or last-wins. The finding concerns contract divergence, not a selected replacement policy.

#### Recommendation and Validation

Add an overlapping-annotation contract test and a renderer-level pixel assertion. Decide the composition rule before implementation.

#### Finding Sources

S2, S4, S7.

### F-004 Ticket identity and tracker provenance

**Axis:** Standards
**Provenance:** Correction and corroboration
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P1
**Confidence:** 99%, because duplicate ticket files, stale tracker rows, and orphaned records are directly visible in the repository.

#### Description

The audit corpus is not yet a reliable identity index. ICW-081 was marked complete while duplicate identity evidence and tracker divergence remained. ICW-188 and ICW-189 existed as ticket files without rows in both live trackers.

#### Rationale

The master ledger records duplicate and stale claims across the supplied reports. The council reviewed the ticket directory and both trackers and reopened ICW-081. The ticket now requires one inventory across all three surfaces.

#### Counter-evidence and Deduplication

Several historical duplicates were corrected. The council preserved those corrections and reopened only the incomplete corpus reconciliation. No new task IDs were created.

#### Recommendation and Validation

Extend the validator to check duplicate ticket identities, duplicate tracker rows, orphaned files, stale status mismatches, and required metadata. Register ICW-188 and ICW-189.

#### Finding Sources

S1, S3, S5, S6, S9, S12, S15.

### F-005 Generator option migration

**Axis:** Standards
**Provenance:** Correction and extension
**Task disposition:** Update
**Verification:** Partially confirmed
**Severity:** P2
**Confidence:** 91%, because `GeneratorOptions` is active and `MipOptions` usage requires direct-caller confirmation.

#### Description

The generator option migration has two existing ticket files but lacked tracker registration. The reports also identify an unreferenced `MipOptions` record and missing adapter, deprecation, and parity coverage.

#### Rationale

ICW-188 and ICW-189 now define separate option-record and adapter acceptance surfaces. ICW-088 remains the coordination task for parameter count and casts.

#### Recommendation and Validation

Register and implement the records, verify direct production callers, preserve forwarding overloads, and compare tile IDs and pixels for representative seeds.

#### Finding Sources

S6.

### F-006 Settings default and validation ownership

**Axis:** Spec
**Provenance:** Extension and corroboration
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 94%, because duplicated defaults and validation gaps are directly reported and linked to existing settings tasks, while the canonical value policy remains open.

#### Description

Noise defaults occur in multiple locations, and `BackgroundNoise` validation does not fully match the effective consumer range. Numeric settings also use inconsistent input constraints.

#### Rationale

The reports identify `NoiseOctaves` drift and settings validation gaps. ICW-P1-SETTINGS-VALIDATION and ICW-022 already own canonical validation, setting consumption, and regeneration preservation.

#### Counter-evidence and Deduplication

The council did not select a new default value. It requires an ownership table first.

#### Recommendation and Validation

Create a source, consumer, and expected-value table. Add validation tests for each declared field and a consumption test through the render and generation call graph.

#### Finding Sources

S6, S7.

### F-007 Benchmark surface and method count

**Axis:** Standards
**Provenance:** Correction and sharpening
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 99%, because the benchmark source contains seven methods and the shipped compositor path is not fully represented.

#### Description

The benchmark record previously described eight scenarios. The source contains seven benchmark methods, with parameterized cases inside those methods. The projection benchmark also needs coverage of the shipped tile compositor path.

#### Rationale

ICW-144 now records the seven-method count. ICW-133 requires repeated measurements and realistic shipped-path coverage. One-iteration Dry output remains a smoke check only.

#### Recommendation and Validation

Update the benchmark matrix to include the shipped compositor overload, repeated runs, allocations, sample count, cache state, mip, and machine metadata.

#### Finding Sources

S5, S9.

### F-008 Spatial boundary consistency

**Axis:** Spec
**Provenance:** Corroboration and correction
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 94%, because closed and half-open interval behavior differs in source, but a user-visible edge failure is not reproduced.

#### Description

Spatial query and placement paths use different boundary semantics. Shared-edge and edge-coordinate behavior therefore lacks one explicit contract.

#### Recommendation and Validation

Define the boundary policy and add tests for left, top, right, bottom, and shared-edge cases. Keep the work under existing boundary tickets.

#### Finding Sources

S9.

### F-009 ViewModel and README contract drift

**Axis:** Spec
**Provenance:** Correction and corroboration
**Task disposition:** Update
**Verification:** Confirmed
**Severity:** P2
**Confidence:** 96%, because the shipped orchestration, README claims, and ticket wording can be compared directly.

#### Description

The reports identify stale `RefreshCommand` wording, overlapping ViewModel ownership, and README claims that do not match the shipped orchestration.

#### Rationale

ICW-017 is complete because `ApplyFrame` is canonical and the dead command path is removed. ICW-016 and ICW-022 retain documentation and ownership alignment work where needed.

#### Recommendation and Validation

Keep the completed status for ICW-017. Correct remaining README and ownership wording, then add focused ViewModel ownership tests where behavior remains unresolved.

#### Finding Sources

S13, S14.

### F-010 ADR boundary and scheduling conformance

**Axis:** Spec
**Provenance:** Correction and extension
**Task disposition:** Defer
**Verification:** Partially confirmed
**Severity:** P2
**Confidence:** 92%, because ADR-0005 and ADR-0006 state the relevant intent, but both architecture boundaries require explicit owner decisions before implementation claims.

#### Description

The reports correctly reject deleting `IBackgroundTileSource` without revising ADR-0005. ADR-0006 also specifies center-distance and mip-suitability tie-breakers that are not fully represented in the current scheduling task.

#### Recommendation and Validation

Preserve the source-neutral boundary. Decide whether ADR-0006 remains authoritative, then implement deterministic tie-breaker tests or record an explicit deferral.

#### Finding Sources

S10, S11.

### F-011 Stale resize and persistence claims

**Axis:** Spec
**Provenance:** Rejected
**Task disposition:** Reject
**Verification:** Refuted
**Severity:** None
**Confidence:** 98%, because source and task evidence show resize debounce and background-image persistence are present.

#### Description

The reports claimed unresolved resize debounce and background-image persistence defects. The council found both behaviors implemented and tracked as complete.

#### Recommendation and Validation

Keep the claims out of the accepted findings. Correct stale audit wording if later reports repeat it.

#### Finding Sources

S12, S14.

### F-012 Unverified cleanup and performance extensions

**Axis:** Standards
**Provenance:** Extension
**Task disposition:** Defer
**Verification:** Unverified
**Severity:** P2
**Confidence:** 70%, because the mechanisms are plausible and locally indicated, but the required runtime or indirect-consumer evidence is incomplete.

#### Description

The audit series proposes dead `DefectBitmap` cleanup, fallback sorting cost, queue allocation cost, and several diagnostics extensions. These remain evidence requests rather than completed defects.

#### Recommendation and Validation

Check indirect consumers before removing bitmap paths. Use focused allocations and repeated benchmarks before assigning implementation priority.

#### Finding Sources

S2, S4, S5, S6.

## Assumptions

| ID | Assumption | Effect if false | Evidence needed | Owner |
| --- | --- | --- | --- | --- |
| A-1 | ADR-0005 remains the active source-neutral boundary. | ICW-076 routing changes. | Architecture decision record update. | Architecture owner |
| A-2 | ICW-078 implementation and focused tests remain at the fixed point. | Reopen stale-frame task. | Re-run focused render tests. | Rendering owner |
| A-3 | ICW-188 and ICW-189 describe distinct migration surfaces. | Merge or rescope tickets. | Direct caller and API inventory. | Rendering owner |

## Open Questions

| ID | Question | Why it matters | Cheapest resolution | Owner |
| --- | --- | --- | --- | --- |
| Q-1 | Which source owns noise defaults and `NoiseOctaves`? | Prevents configuration drift. | Produce a source, consumer, and expected-value table. | Settings owner |
| Q-2 | Should ADR-0006 tie-breakers be implemented or revised? | Determines scheduler acceptance criteria. | Record an architecture decision and add deterministic queue tests. | Scheduling owner |
| Q-3 | Which overlap composition policy should pixelometer and renderer share? | Prevents inconsistent readouts. | Add an overlapping-annotation contract test and decide the rule. | Rendering owner |
| Q-4 | Does pixelometer migration require a published-frame snapshot or resident-only read? | Determines ICW-076 implementation boundary. | Add a cold-hover test and inspect frame publication ownership. | Rendering owner |
| Q-5 | What benchmark threshold justifies queue replacement? | Prevents speculative data-structure work. | Run adversarial repeated benchmarks with allocation and lock metrics. | Performance owner |

## Requests

| Priority | Request | Rationale | Required response |
| --- | --- | --- | --- |
| P1 | Approve or revise the ADR-0005 and ADR-0006 boundaries. | The decisions control pixelometer migration and scheduling acceptance. | Record the decision in the relevant ADR and task. |
| P2 | Provide repeated fast-scroll and cold-hover evidence. | Runtime frequency and performance impact remain open. | Attach benchmark and focused test results to ICW-076 and ICW-144. |

## Source Ledger

| ID | Source | Type | Revision or date | Read directly | Use and limitation |
| --- | --- | --- | --- | --- | --- |
| S1-S15 | The 15 supplied reports listed in [master findings list](master-findings-list-26-08-03-00-00-00.md) | Audit reports | 2026-07-31 through 2026-08-03 | Yes | Claim extraction and provenance only. Reports do not prove their own conclusions. |
| S16 | [DesignDoc.md](../../DesignDoc.md) | Design | `b5e1e8b` | Yes | Architecture and open-question comparison. |
| S17 | [README.md](../../README.md) | Documentation | `b5e1e8b` | Yes | Runtime and MVVM contract comparison. |
| S18 | [functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md) | Requirement | `b5e1e8b` | Yes | Behavioral invariants and mandatory audit requirements. |
| S19 | [ADR-0003](../ADR/0003-live-hybrid-spatial-indexing.md), [ADR-0004](../ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md), [ADR-0005](../ADR/0005-source-agnostic-background-tile-mips.md), [ADR-0006](../ADR/0006-viewport-aware-tile-work-scheduling.md) | ADRs | `b5e1e8b` | Yes | Boundary and sequencing decisions. |
| S20 | [active-tasks.md](../tasks/active-tasks.md), [task-tracker.md](../tasks/task-tracker.md), affected ticket files | Tasks | 2026-08-03 worktree | Yes | Status, identity, and task coverage. |
| S21 | Source, test, and benchmark paths cited by the council review | Code, test, benchmark | `b5e1e8b` | Yes | Independent mechanism checks. Runtime soak and user reproduction were unavailable. |
| S22 | [audit-reconciliation-council-review-26-08-03-00-00-00.md](audit-reconciliation-council-review-26-08-03-00-00-00.md) | Council report | 2026-08-03 | Yes | Three-seat recommendations and dissent. |

## Task and Sprint Updates

| Finding | Task action | Tracker locations | Sprint impact |
| --- | --- | --- | --- |
| F-001, F-003, F-010 | Update existing pixelometer, blend, and mip boundary tasks. | ICW-035, ICW-076, ICW-100, `active-tasks.md`, `task-tracker.md` | Keep migration ahead of new source integrations. |
| F-002, F-007 | Extend queue evidence and correct the seven-method count. | ICW-064, ICW-144, ICW-133, `active-tasks.md`, `task-tracker.md` | Require repeated evidence before queue redesign. |
| F-004 | Reopen corpus reconciliation and extend validator scope. | ICW-081, `active-tasks.md`, `task-tracker.md` | Place before new audit-derived backlog growth. |
| F-005 | Register existing generator option tickets and coordinate with ICW-088. | ICW-088, ICW-188, ICW-189, both trackers | No new IDs. |
| F-006, F-008, F-009 | Extend existing settings, boundary, ViewModel, and documentation tasks. | ICW-016, ICW-017, ICW-022, ICW-033, ICW-P1-SETTINGS-VALIDATION | No architecture change. |
| F-011, F-012 | Reject stale claims and defer evidence-dependent cleanup. | No new task; existing related tickets remain open where applicable. | No sprint reorder. |

## Validation Result

Command: `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

Result: Failed on pre-existing legacy errors, including missing required fields in older ticket files, invalid `Deprecated` and `Reverted` statuses, unsupported `todo` status, unsupported `High` priority, and unsupported `Chore` type. The command did not report the edited ICW-081, ICW-078, ICW-144, ICW-188, or ICW-189 records.

No source-code implementation was requested or performed.

