---
name: codebase-audit
description: 'Run an exhaustive codebase audit focused on bugs, brittle paths, primitive obsession, code smells, unclear assumptions, and legacy-risk reduction. Use when asked for deep reviews, council-style peer review, backlog reconciliation, or findings-only audit updates with durable ICW task capture.'
argument-hint: 'Audit focus area or depth (e.g., full repo, rendering pipeline, follow-up net-new only)'
---

# Codebase Audit

## Outcome

Produce a high-signal, evidence-backed audit that:

- finds defects, inconsistencies, weak abstractions, and risky assumptions
- avoids duplicate backlog noise by cross-referencing existing ICW tasks
- creates or updates durable work items with priority and acceptance direction
- emits a findings-only audit file when prior audits already exist

## When to Use

Use this skill when asked to:

- perform a full deep dive or peer-review style audit
- continue prior audit research and find net-new issues
- evaluate technical debt and legacy migration opportunities
- identify duplication and propose consolidation/refactoring opportunities
- reconcile code/design/docs/backlog for drift and missing coverage

Do not use this skill for simple bug fixes or single-file code edits.

## Inputs

- Audit scope: full repository or specific modules
- Baseline artifacts: existing audits, JIRA/task board, ticket files, ADRs
- Constraint mode: net-new findings only vs full re-evaluation

## Procedure

1. Establish audit baseline
- Read existing audits in docs/audits.
- Read docs/tasks/JIRA.md and docs/tasks/active-tasks.md.
- Enumerate existing tickets under docs/tasks/tickets.
- Classify current backlog coverage so new findings can be de-duplicated.

2. Gather code evidence by subsystem
- Review src/InfiniteCanvas.Core, src/InfiniteCanvas.Spatial, src/InfiniteCanvas.Rendering, src/InfiniteCanvas.App, src/InfiniteCanvas.ViewModels.
- Review tests and benchmarks for coverage gaps and representativeness.
- Track exact file and line references for each candidate finding.

3. Run council-style challenge pass
- For each candidate finding, pressure-test with adversarial checks:
  - Is this already tracked by an ICW key?
  - Is it a true defect, a design tradeoff, or by-design behavior?
  - Can it be reproduced or reasoned from control flow/lifecycle?
  - What is the blast radius and user-visible impact?
- Drop weak findings and keep only defensible items.

4. Classify and prioritize findings
- Assign severity and confidence independently.
- Severity reflects impact/risk.
- Confidence reflects evidentiary certainty.
- Prefer priority mapping:
  - P0: crash/data-loss/security/major correctness
  - P1: high reliability/perf hazards likely to impact users
  - P2: architectural limitations and medium risk debt
  - P3: consistency/maintainability cleanups

5. Decide action path per finding
- If existing ICW task already covers it: update/extend that task with sharper acceptance notes.
- If partially covered: create correction note and dependency linkage.
- If not covered: create a new ICW ticket with scope, evidence, validation plan, and next step.

6. Produce a findings-only audit artifact
- Create a new timestamped file in docs/audits.
- Include only:
  - net-new findings
  - corrections/extensions to existing tasks
  - updated priority order
- Do not repeat unchanged prior findings unless needed for dependency context.

7. Update durable trackers
- Add or refine entries in docs/tasks/active-tasks.md.
- Add matching keys and activity rows in docs/tasks/JIRA.md.
- Ensure each new key has a ticket file under docs/tasks/tickets.

8. Perform consistency check
- Verify all new ICW keys exist in both trackers.
- Verify ticket paths referenced by trackers are real.
- Verify no duplicate keys and no contradictory status fields.

## Decision Points

- If a finding is theoretical and not reproducible:
  - classify as Spike, lower confidence, and require evidence protocol.
- If findings overlap three or more related tasks:
  - create a parent-epic note and dependent linkage to avoid fragmented execution.
- If a defect appears in shutdown, lifecycle, or async-void paths:
  - bias severity upward due to crash risk.

## Completion Criteria

Audit work is complete only when all are true:

- a new audit document is written with net-new findings and task corrections
- every accepted new finding is captured as a durable ICW task/ticket
- tracker updates are synchronized across active-tasks and JIRA
- priorities and rationale are explicit
- open questions and confidence limits are called out

## Quality Bar

- Every finding must cite concrete file and line references.
- Avoid speculative language unless marked as theoretical with confidence.
- No duplicate backlog creation for already-covered items.
- Recommendations must be implementable, scoped, and testable.

## Output Template

Use this structure for audit files:

1. Executive Summary (net-new only)
2. New Findings (severity, confidence, evidence, risk, recommendation)
3. Corrections/Extensions to Existing Tasks
4. Priority Order (P0-P3)
5. Open Questions and Validation Gaps

## Example Prompts

- /codebase-audit full-repo net-new only
- /codebase-audit rendering and lifecycle deep dive
- /codebase-audit reconcile backlog coverage against code and ADRs
