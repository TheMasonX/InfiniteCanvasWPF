# InfiniteCanvasWPF Critical Peer Review

Date: 2026-07-25
Scope: Cross-validation of the four supplied external audit reports against the current worktree, source, tests, and task trackers.

## Executive Summary

The external audits are directionally useful but mix current findings, already-resolved findings, duplicate backlog items, and claims based on an older repository snapshot. Two code-level findings survive current-source verification: background-image visibility is not persisted, and tile-cache eviction has no explicit recency policy. The audits also identify a repository-process defect: the ticket corpus contains duplicate identities and orphaned entries, which makes future audit de-duplication unreliable.

The global exception-safety finding is corroborated but not new: the current worktree now registers Dispatcher, AppDomain, and TaskScheduler handlers in `App.xaml.cs`; the remaining work belongs to ICW-014 and ICW-029. The STRtree immutability, GenerateSet argument attribution, and coalescer fault-containment findings are verified fixed or already tracked and are not reopened here.

## Validated New Findings

### ICW-081: Ticket-corpus identity and tracker integrity

- Severity: P1 process risk
- Confidence: High
- Evidence: `docs/tasks/tickets` contains 85 markdown files. Current inventory reports duplicate numeric identity ICW-065 (`ICW-065-spatial-tests-and-docs.md` and `ICW-065-viewport-scrollbars-and-zoom-navigation.md`) and ICW-061/ICW-062/ICW-063 ticket files absent from the live tracker tables. The directory also contains multiple metadata styles, including schema-compliant frontmatter, legacy plain metadata, and unowned draft-style records.
- Risk: Future audits can create duplicate work or miss existing work; status and evidence cannot be trusted without re-reading every ticket.
- Action: ICW-081. Normalize identities and schemas, preserve substantive evidence, and add duplicate-ID validation.

### ICW-082: Background-image visibility is not persisted

- Severity: P2
- Confidence: High
- Evidence: `CanvasUserSettings` contains `ShowLabels`, `ShowBoxes`, `ShowSparseImageTiles`, and `ShowBackgroundImages` is absent. `MainWindow.ApplySettingsToUi` does not apply a persisted background-image toggle, and `SaveSettings` omits the corresponding checkbox value. This contradicts the layer-visibility invariant in `docs/requirements/functional-requirements-and-invariants.md` and the completion claim in ICW-073.
- Risk: The user's background-image visibility choice is silently lost across application restarts.
- Action: ICW-082. Add the setting, wire load/save, and add a round-trip test.

### ICW-305: Tile-cache eviction policy lacks an explicit contract

- Severity: P2
- Confidence: High
- Evidence: `TileCacheBudget.TryReserve` selects `_trackedTiles.Values.FirstOrDefault(...)` from a dictionary. It does not record access recency, and dictionary enumeration is not a documented LRU contract. The existing `ICW-305-tilecache-eviction-policy.md` captures this finding but was absent from both live trackers.
- Risk: Cache eviction and refetch behavior can be surprising and difficult to test, especially when panning across a budget boundary.
- Action: Register ICW-305 in both trackers; choose and test LRU or explicitly document the intended non-LRU policy.

## Corroborations and Corrections

- ICW-014: Global exception hooks are now present in `App.xaml.cs`. Keep the task In Progress for selected async-void hardening and close-time stress validation; do not report the original “no handlers exist” claim as current.
- ICW-029: Shutdown still cancels and disposes `_generationGate` without awaiting an in-flight `RegenerateSceneAsync`; retain as an existing high-risk lifecycle task and validate with close stress.
- ICW-073: Downgraded from Done to In Review because its independent-toggle persistence acceptance is not met; ICW-082 owns the missing background-image setting.
- ICW-033, ICW-031, ICW-035, ICW-078, ICW-079, and ICW-080 remain existing backlog items and were not duplicated.

## Rejected or Already-Resolved Claims

- STRtree query mutability: current `StrTreeSpatialIndexService.Query` copies results to an array; treat related orphaned tickets as reconciliation/closure candidates.
- GenerateSet validation attribution and explicit rows/imageCount consistency: current code and tests cover these corrections.
- Coalescing render fault containment: current `CoalescingAsyncAction` reports non-cancellation faults and preserves follow-up work; ICW-034 is Done.
- CameraTransform's widened default scale range: a design/documentation question, not a demonstrated defect under the current viewport zoom policy; do not create a task without a product constraint or failing test.
- SampleAnnotation record equality: latent API ambiguity with no current consumer or failing behavior; keep out of the active backlog unless equality becomes part of a contract.
- Stale handoff prose: valid documentation drift, but lower priority than tracker reconciliation and can be handled within ICW-081.

## Priority Order

1. ICW-081 (P1): restore task-tracker integrity before further audit-driven expansion.
2. ICW-014 / ICW-029 (existing high-risk lifecycle work): complete exception-handler coverage and shutdown stress validation.
3. ICW-082 (P2): close the settings persistence regression.
4. ICW-305 (P2): define deterministic cache eviction semantics.
5. Existing ICW-031, ICW-033, ICW-035, ICW-078, ICW-079, and ICW-080.

## Validation Gaps

- No runtime close-stress test has reproduced the suspected shutdown race.
- No deterministic cache-eviction test currently establishes a recency contract.
- The settings gap is source-verifiable and should receive a focused round-trip test when implemented.
- Full ticket normalization remains open under ICW-081.
