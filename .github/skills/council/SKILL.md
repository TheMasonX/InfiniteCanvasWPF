---
name: council
description: >-
  Run a multi-seat peer review for high-impact decisions in InfiniteCanvasWPF.
  Use when evaluating requirement-to-task coverage, architecture plans, audit
  reconciliation, sprint scope, or migration strategy. Default 4 seats plus a
  synthesizer. Produces a structured report with seat-level findings,
  confidence percentages, dissent, acceptance criteria, and evidence gates.
argument-hint: 'Decision topic and scope — such as viewport requirements alignment or audit reconciliation'
user-invocable: true
disable-model-invocation: false
---

# LLM Council Review for InfiniteCanvasWPF

Inherits patterns from MemorySmith LLM Council and the Subagent Swarm skill.

## Outcome

Produce a council report that includes:

- One-sentence decision statement
- Seat-by-seat findings with confidence percentages and blocking concerns
- Explicit disagreement and dissent (do not flatten to consensus)
- Risks, assumptions, and open questions
- Acceptance criteria and evidence gates before implementation
- Concrete task additions, modifications, or scope changes

## When to Use

Use this workflow when a decision affects:

- **Requirement-to-task alignment**: Do the listed tasks fully support the stated requirements? Are there requirements with no task coverage?
- **Architecture or migration plans**: Does the plan's sequencing respect hard dependencies? Are there phase paradoxes?
- **Audit reconciliation**: Are external audit findings correctly validated and tracked? Are there gaps in coverage?
- **Sprint or epic scoping**: Are the acceptance criteria complete? Are there missing dependency links?
- **Cross-subsystem decisions**: Changes that span rendering, spatial indexing, view models, settings, and UI layers

Do not use this workflow for single-file changes, trivial bug fixes, or quick lookups.

## Inputs

Collect these before running the workflow:

- Decision topic and one-sentence question
- Scope of impact: which subsystems are affected (rendering, spatial, view models, settings, UI, docs)
- Primary evidence documents: requirements registry, task tracker, ADRs, handoff notes, audit files
- Any known stale docs, assumptions, or unresolved constraints

## Procedure

### 1. Build an Evidence Pack

Minimum pack should include:

- `docs/requirements/functional-requirements-and-invariants.md`
- `docs/tasks/active-tasks.md`
- `docs/tasks/JIRA.md`
- `DesignDoc.md`
- Relevant ADRs under `docs/ADR/`
- Relevant handoff notes under `docs/handoffs/`
- Relevant audit files under `docs/audits/`
- Source-linked code evidence when claims depend on implementation

### 2. Select Council Seats

Default to 4 seats plus a Synthesizer. Adjust the seat list based on the decision scope:

| Seat | Focus | Best for |
|---|---|---|
| **Viewport Architecture Reviewer** | Viewport requirements, tile scheduling, render pipeline, mip/cache contract | Viewport-aware scheduling, rendering architecture |
| **Coordinator/Concurrency Reviewer** | TileWorkCoordinator, cancellation, claimant tokens, _activeCount, GDI+ safety | Concurrency defects, cancellation correctness |
| **Settings/Persistence/MVVM Reviewer** | CanvasUserSettings, MainViewModel lifecycle, UI invariants, async-void safety | Settings bugs, Phase 1 scope, UI layer |
| **Implementation Sequencing Reviewer** | Task dependencies, phase ordering, parallelism, duplicate IDs, status correctness | Sprint sequencing, epic restructure, tracker hygiene |
| **Spatial Indexing Reviewer** | ISpatialIndexService, STRtree, LiveSpatialIndexService, ADR-0003 conformance | Spatial index safety, publish semantics |
| **Rendering Performance Reviewer** | Benchmark baselines, stage instrumentation, Gray8/mip optimization | Performance claims, benchmark evidence |

Always include at least 4 seats for a full council. For narrow-scope decisions, use 3 seats minimum. For major architecture decisions, use all 6.

### 3. Run Independent Seat Reviews

Each seat receives the same evidence pack and must return:

- **Findings**: Concrete observations supported by evidence
- **Risks**: What could go wrong if the finding is not addressed
- **Recommendations**: Specific actions to take
- **Assumptions**: What the seat assumes to be true
- **Open questions**: What evidence is missing
- **Confidence percentage**: 0.0–1.0 expressing certainty in the finding

For parallel execution, delegate seat reviews to subagents using the [subagent-swarm](../subagent-swarm/SKILL.md) skill. Each seat runs as an independent subagent with the same evidence pack but a different perspective prompt.

### 4. Branch on Disagreement

If seats materially disagree, do not flatten to consensus. Record the disagreement explicitly and identify what missing evidence would change the outcome. Common sources of disagreement:

- Different interpretations of the same requirement
- Different assumptions about task completeness
- Different risk tolerance for deferred work

### 5. Synthesize a Decision

The Synthesizer (the main agent running the council) must separate:

- **What changes now**: Tasks to create, modify, or promote. Scope expansions. Dependency updates.
- **What is deferred**: Items that can wait, with rationale and trigger conditions.
- **What evidence gates must be passed**: Specific validation criteria that must be met before implementation is considered complete.

### 6. Apply Findings

Apply the council's recommendations to the repository:

- Update `docs/tasks/active-tasks.md` with new tasks, status changes, scope expansions, and dependency links
- Update `docs/requirements/functional-requirements-and-invariants.md` with new or refined requirements
- Update relevant ADRs if the council identified architecture-level changes
- Create or update handoff note under `docs/handoffs/`

### 7. Record the Result

Write the council report to `docs/audits/` with a descriptive filename. The report must include all seats' findings, dissent, acceptance criteria, and open questions.

## Decision Branches

- **Branch A: Coverage gap found**. If a requirement has no supporting task, the council must either create a new task, expand an existing task's scope, or explicitly defer with documented rationale and trigger conditions.

- **Branch B: Phase paradox found**. If a task in an earlier phase depends on a task in a later phase, the council must either split the task across phases, promote the later-phase task, or document the intentional ordering.

- **Branch C: Duplicate or conflicting task found**. If the same ICW ID appears with different descriptions, the council must flag it for deduplication under ICW-081 before proceeding.

- **Branch D: Evidence weakness**. If evidence is thin or stale, defer implementation and define what evidence must be gathered before the task can proceed.

## Completion Checks

A council review is complete only when all are true:

- Decision statement is explicit (one sentence)
- Evidence links are listed and source-grounded
- Each seat includes findings, confidence, and blocking concerns
- Dissent is visible, not merged away
- Acceptance criteria are testable or reviewable
- If tests or benchmarks are omitted, exception rationale and follow-up validation gates are explicit
- Open questions are documented with next-step owners or gates
- Task tracker is updated to reflect the council's recommendations

## Report Template

Use this structure in the final output:

```markdown
# Council Review: <Decision>

## Decision
<one sentence>

## Evidence Reviewed
- <document links>

## Findings
| Seat | Recommendation | Confidence | Blocking concern |
|---|---|---|---|
| <Seat Name> | ... | 0.85 | ... |
| <Seat Name> | ... | 0.80 | ... |

## Synthesis
<what changes now vs later>

## Dissent
<unresolved disagreement>

## Acceptance Criteria
- <gate 1>
- <gate 2>

## Open Questions
- <question 1>
- <question 2>
```

## Quality Bar

- Prefer evidence over assumptions. Each finding must trace to a specific line, section, or document.
- Keep each seat review compact and independent. Do not let seat reviews influence each other before the synthesis phase.
- Avoid speculative recommendations that are unrelated to the evidence. If the evidence is insufficient, say so and define what is needed.
- Preserve the repo's existing architecture and task-tracking conventions.

## Example Prompts

- `/council verify the viewport-aware tile work requirements are fully covered by the listed tasks`
- `/council review the Phase 0/1 sequencing for correctness and dependency completeness`
- `/council assess whether the external audit findings are correctly reflected in the task tracker`
- `/council evaluate the settings persistence requirements against current task coverage`

## References

- [Subagent Swarm](../subagent-swarm/SKILL.md) — parallel seat execution
- [Task Tracker](../../tasks/active-tasks.md) — live task corpus
- [Requirements Registry](../../requirements/functional-requirements-and-invariants.md) — canonical requirements
- [Example council report](../../audits/viewport-requirements-council-review-26-07-30.md) — first council run output
