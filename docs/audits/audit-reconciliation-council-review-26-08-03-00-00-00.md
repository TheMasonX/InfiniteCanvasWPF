# Council Review: Audit Reconciliation and Task Coverage

**Description:** Three independent seats reviewed the pre-council master findings list and the 15 requested audit reports against source, requirements, ADRs, tests, benchmarks, and trackers.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `b5e1e8b210d3bf3c79caa0366d7ce052e6883cd5`
**Latest commit:** `b5e1e8b210d3bf3c79caa0366d7ce052e6883cd5` - `Track ICW-148: FastNoise2Bindings proper submodule registration`
**Author:** GitHub Copilot
**Timestamp:** 2026-08-03 00:00 US Central
**Review mode:** Full reconciliation, three-seat council
**Scope:** 48 claims in the pre-council ledger, 15 requested audits, source paths, requirements, ADR-0003 through ADR-0006, tests, benchmarks, task trackers, and ticket files.

## Decision

Accept only independently supported runtime, specification, provenance, and tracker findings, route related work through existing ICW tasks, and reject stale or unsupported report claims.

## Evidence Reviewed

- [Master findings list](master-findings-list-26-08-03-00-00-00.md)
- [DesignDoc.md](../../DesignDoc.md)
- [README.md](../../README.md)
- [Requirements registry](../requirements/functional-requirements-and-invariants.md)
- [ADR-0003](../ADR/0003-live-hybrid-spatial-indexing.md)
- [ADR-0004](../ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md)
- [ADR-0005](../ADR/0005-source-agnostic-background-tile-mips.md)
- [ADR-0006](../ADR/0006-viewport-aware-tile-work-scheduling.md)
- [Active tracker](../tasks/active-tasks.md)
- [JIRA tracker](../tasks/JIRA.md)
- All 15 audit sources listed in the master ledger.

## Seat Findings

| Seat | Recommendation | Confidence | Blocking concern |
| --- | --- | ---: | --- |
| Implementation and Runtime Reviewer | Keep C9, C22, C24, C34, C37, and C39. Keep C8, C21, C35, C36, and C38 as measured follow-ups. Reject stale C10, C11, C20, and C23. | 0.97 | Pixelometer acquisition, queued-work eviction, callback diagnostics, and ADR-0006 ordering lack complete focused regression coverage. |
| Architecture and Specification Reviewer | Confirm C13-C19, C25-C33, C39-C44, and C46 with narrowed impact. Split accounting from no-acquisition behavior, preserve ADR-0005 boundaries, and correct README and registry drift. | 0.96 | ADR-0005 and ADR-0006 define intent beyond current production integration. Owners must decide whether to implement or revise the proposed ADRs. |
| Provenance, Task, and Tracker Reviewer | Reopen ICW-081, correct ICW-078 and ICW-017 tracker status, correct ICW-144 method count, register ICW-188/189, and route C40-C42 through existing tasks. | 0.98 | The validator does not check all tracker surfaces, and duplicate IDs represent both historical artifacts and distinct active mechanisms. |

## Accepted Findings

| ID | Verification | Severity | Confidence | Standards axis | Spec axis | Disposition |
| --- | --- | --- | ---: | --- | --- | --- |
| C2-C5 | Confirmed corrections | P1 process risk | 99% | Duplicate identities and stale rows fail the task-tracker contract. | ICW-081 completion claim is not supported by the current corpus. | Reopen ICW-081. |
| C6 | Confirmed correction | P3 | 99% | Benchmark documentation is inaccurate. | Benchmark coverage exists, but the method count is seven, not eight. | Update ICW-144. |
| C9 | Confirmed | P2 | 98% | Public value type permits invalid default state. | Default interest-set operations can dereference null-backed sets. | Add follow-up under ICW-143 or coordinator cleanup. |
| C12 | Confirmed cleanup | P3 | 100% | Repeated source identity literals create drift risk. | Current behavior is consistent. | Route through ICW-018 cleanup. |
| C14-C19 | Confirmed or partial corrections | P2 | 88-99% | Dead options, duplicated defaults, and inconsistent validation weaken maintainability. | Settings and generator contracts lack one canonical default and validation policy. | Extend existing ICW-088, ICW-188, ICW-189, and ICW-P1-SETTINGS-VALIDATION work. |
| C22 | Confirmed, policy split | P2 | 95% | Pixelometer repeats spatial work and composition ownership. | Overlap composition policy is not explicit and paths disagree. | Update ICW-035 and ICW-100 scope without closing either concern. |
| C24-C25, C41 | Confirmed split | P1 for no-acquisition, low for accounting | 94-99% | Hover reads cross the acquisition boundary. | Requirements and ADR-0005 require resident or published-frame reads without hover-triggered acquisition. | Route migration through ICW-076 and preserve the completed interim accounting record. |
| C26 | Confirmed | P2 | 98% | Benchmark does not exercise the shipped compositor surface. | Performance evidence requirement is not met by the point-cloud overload alone. | Update ICW-133 benchmark matrix. |
| C27 | Confirmed observation | P2 | 94% | Boundary policy is inconsistent. | User-visible impact remains unproven without edge tests. | Extend existing boundary task. |
| C28-C29, C44-C45 | Confirmed correction | P2 | 94-98% | ViewModel ownership and README claims diverge from shipped orchestration. | One canonical state owner and accurate documentation are unresolved. | Update ICW-017 and ICW-016 records. |
| C30-C33 | Narrowed findings | P2 or P3 | 90-99% | Competing binding ownership, unbounded inputs, repeated scrollbar conditionals, and missing accessibility metadata are quality gaps. | No direct user-facing failure is proven for the binding or slider claims. | Extend ICW-022, ICW-077, ICW-037, and settings work without new IDs. |
| C34-C38 | Confirmed implementation concerns | P1 or P2 | 86-100% | Cache admission and queue operations retain avoidable work and allocations. | Active queued work should not be evicted, and callback failures should remain observable. | Extend ICW-064 and ICW-144; add coordinator cleanup evidence. |
| C39 | Confirmed partial conformance | P2 | 97-98% | Implementation does not match the proposed ADR ordering policy. | ADR-0006 requires center-distance and mip-suitability tie-breakers. | Implement or explicitly revise ADR-0006 before acceptance. |
| C40-C42 | Confirmed correction or redirection | P1-P2 | 91-99% | ADR traceability and setting ownership need correction. | Preserve ADR-0005 source-neutral intent and keep threshold and overdraw work open. | Update ICW-018, ICW-076, ICW-004, and ICW-P1-SETTINGS-VALIDATION. |
| C43, C46 | Confirmed non-findings | None | 96-99% | No defect found. | Resize debounce and background-image persistence are present. | Reject as findings and correct stale notes only. |
| C47-C48 | Confirmed provenance corrections | P2-P3 | 96-97% | Repeated audits must not inflate net-new counts. | Granular duplicate observations support existing ICW-081 scope. | Correct source ledger and ICW-081 evidence. |

## Synthesis

### What changes now

1. Reopen ICW-081 and expand validation to ticket files, active-tasks.md, and JIRA.md.
2. Update ICW-078 and ICW-017 tracker and ticket status to match completed source behavior.
3. Correct ICW-144 from eight benchmark scenarios to seven benchmark methods, and keep repeated hardware evidence open.
4. Register existing ICW-188 and ICW-189 in both trackers. Do not create duplicate IDs.
5. Split pixelometer cache accounting from the no-acquisition requirement and route the remaining migration through ICW-076.
6. Preserve `IBackgroundTileSource` unless an ADR explicitly changes the boundary.
7. Extend settings, boundary, benchmark, scheduler, README, and ViewModel ownership tasks with the council acceptance criteria.

### What remains deferred

- Any source-code implementation for the confirmed runtime findings. The user requested audit synthesis, not implementation.
- Queue data-structure replacement until benchmark thresholds show a material benefit.
- Noise default value changes until an ownership table identifies the canonical source.
- Spatial interface expansion until diagnostic semantics are defined for all implementations.

## Dissent

- C8 is a real claimant-registration retention mechanism, but its severity is disputed. Keep it as a P2 evidence request unless a retained-registration test demonstrates sustained growth.
- C22 proves duplicate work and inconsistent composition paths, but the correct overlap policy remains open. Do not choose max-wins or last-wins without a requirement decision.
- C27 proves interval mismatch, but not a user-visible edge defect. Require edge tests before assigning higher severity.
- C39 is a direct ADR conformance gap only if ADR-0006 remains authoritative. Because it is Proposed, the architecture owner may revise the decision instead of implementing every tie-breaker.
- C42 confirms unused threshold plumbing and an open overdraw investigation, but thresholding does not answer every zoomed-out overdraw question.

## Acceptance Criteria and Evidence Gates

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-restore` passes.
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release --no-restore` succeeds.
- Windows bitmap tests run for bitmap, tile, or compositor changes.
- The task validator checks duplicate ticket files, duplicate tracker rows, orphaned files, stale statuses, and missing metadata.
- Pixelometer tests prove cold hover reads do not submit coordinator work.
- Cache tests prove queued work is not silently evicted, or that eviction cancels work and releases ownership exactly once.
- Benchmark tests cover the shipped tile compositor overload and use repeated runs for performance claims.
- Boundary tests cover left, top, right, bottom, and shared-edge cases.
- ADR-0006 either implements deterministic distance and mip tie-breakers or records an explicit deferral.
- Settings tests identify one owner for defaults and one validation function per field.

## Open Questions

| ID | Question | Cheapest resolution | Owner |
| --- | --- | --- | --- |
| Q1 | Which noise default source owns `NoiseOctaves` and related values? | Produce a source, consumer, and expected-value table. | Settings owner |
| Q2 | Should ADR-0006 tie-breakers be implemented or revised? | Architecture decision with deterministic queue-order tests. | Scheduling owner |
| Q3 | What overlap composition rule should pixelometer and renderer share? | Add overlapping-annotation contract test and requirement decision. | Rendering owner |
| Q4 | Does `IBackgroundTileSource` remain the production boundary? | Approve, revise, or supersede ADR-0005. | Architecture owner |
| Q5 | What benchmark threshold justifies replacing queue structures? | Run adversarial queue benchmark with allocation and lock metrics. | Performance owner |

## Concrete Task Actions

| Finding | Task action | Tracker locations |
| --- | --- | --- |
| C2-C5, C47-C48 | Reopen ICW-081 and correct validator and identity inventory. | ICW-081 ticket, active-tasks.md, JIRA.md |
| C6, C35-C38 | Correct ICW-144 method count and add queue allocation and callback evidence gates. | ICW-144 ticket, active-tasks.md, JIRA.md |
| C14-C19 | Register ICW-188/189, update ICW-088, and extend settings validation scope. | Existing tickets, both trackers |
| C24-C25, C40-C41 | Route pixelometer migration through ICW-076 and preserve ADR-0005. | ICW-076, ICW-018, requirements registry |
| C26 | Exercise the shipped tile compositor in ICW-133. | ICW-133 ticket, benchmark source |
| C27 | Extend boundary semantics evidence. | ICW-033 or ICW-064 boundary ticket |
| C28-C29, C44-C45 | Synchronize ViewModel ownership and README/task wording. | ICW-016, ICW-017, both trackers |
| C39 | Resolve ADR-0006 tie-breaker conformance. | ADR-0006, ICW-143, ICW-144 |
| C42-C43, C46 | Keep overdraw and sparse threshold work open, remove stale persistence note. | ICW-004, ICW-P1-SETTINGS-VALIDATION, requirements registry |

## Status

Council review complete. No source-code changes were made.
