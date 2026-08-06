---
name: codebase-audit
description: 'Run an exhaustive codebase audit focused on bugs, brittle paths, primitive obsession, code smells, unclear assumptions, and legacy-risk reduction. Use when asked for deep reviews, council-style peer review, backlog reconciliation, or findings-only audit updates with durable ICW task capture.'
argument-hint: 'Audit focus area or depth (e.g., full repo, rendering pipeline, follow-up net-new only)'
---

# Codebase Audit

## Outcome

Produce a high-signal, evidence-backed audit for InfiniteCanvasWPF that:

- finds defects, inconsistencies, weak abstractions, and risky assumptions in the rendering, spatial, view-model, and WPF lifecycle layers
- avoids duplicate backlog noise by cross-referencing existing ICW tasks, ADRs, and the repository task trackers
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

- Audit scope: full repository or specific modules such as rendering, spatial indexing, bitmap lifecycle, or WPF app startup/shutdown
- Baseline artifacts: existing audits, DesignDoc.md, README.md, docs/tasks/active-tasks.md, docs/tasks/task-tracker.md, ticket files, ADRs
- Constraint mode: net-new findings only vs full re-evaluation
- (Optional) Fixed point for diff-based review: a commit SHA, branch name, tag, or other git reference to compare HEAD against

## Two-Axis Framework

This skill runs two independent review axes in parallel (via sub-agents) so that one concern never masks the other:

- **Standards** — does the code conform to this repo’s documented coding conventions, plus a universal code-smell baseline?
- **Spec** — does the code faithfully implement the originating issue, PRD, spec, or requirement?

A change can pass one axis and fail the other:
- Code that follows every standard but implements the wrong thing → Standards pass, Spec fail.
- Code that does exactly what the issue asked but breaks the project’s conventions → Spec pass, Standards fail.

Report findings under separate headings. Do not merge or rerank findings across axes.

## Procedure

### 0. Pin the fixed point (diff-based review only)

If the audit compares code against a specific git reference:
- Capture the diff: `git diff <fixed-point>...HEAD` (three-dot, merge-base comparison).
- Capture the commit list: `git log <fixed-point>..HEAD --oneline`.
- Confirm the ref resolves (`git rev-parse <fixed-point>`) and the diff is non-empty before proceeding.

If the audit is scope-based (e.g., “full rendering pipeline”) rather than diff-based, skip this step.

### 1. Establish audit baseline

- Read existing audits in docs/audits.
- Read docs/tasks/task-tracker.md and docs/tasks/active-tasks.md.
- Enumerate existing tickets under docs/tasks/tickets.
- Classify current backlog coverage so new findings can be de-duplicated.

### 2. Identify the spec source

Look for the originating requirement or specification, in this order:
1. Issue references in commit messages (`#123`, `Closes #45`, etc.) — fetch via the issue tracker.
2. A path the user passes as an argument.
3. A PRD, spec, or requirement file under `docs/`, `docs/requirements/`, `specs/`, or `.scratch/` matching the branch name or feature.
4. If nothing is found, ask the user where the spec is. If there is no spec, the Spec axis reports “no spec available”.

### 3. Identify the standards sources

Collect all files in the repo that document how code should be written. Look for:
- `CODING_STANDARDS.md`, `CONTRIBUTING.md`, `.editorconfig`, `Directory.Build.props` (analysis rules)
- DesignDoc.md for architectural conventions
- ADRs for decision-level constraints
- The agent file (`.github/agents/infinitecanvas.agent.md`) for documented style policies

The Standards axis always carries the **code-smell baseline** from step 5, even when the repo documents no explicit standards. A documented repo standard overrides the baseline. Skip anything that tooling already enforces.

### 4. Gather code evidence by subsystem

- Review src/InfiniteCanvas.Core, src/InfiniteCanvas.Spatial, src/InfiniteCanvas.Rendering, src/InfiniteCanvas.App, and src/InfiniteCanvas.ViewModels.
- Review tests and benchmarks for coverage gaps and representativeness, especially where behavior is tied to zero-copy bitmap handling, spatial queries, or asynchronous view-model updates.
- Track exact file and line references for each candidate finding.

### 5. Run council-style challenge pass against both axes

For each candidate finding, pressure-test with adversarial checks:
- **Standards axis**: does this violate a documented standard or a smell from the baseline below?
- **Spec axis**: does this diverge from the spec, implement something not asked for, or miss a requirement?

#### Code-Smell Baseline (Fowler, *Refactoring* ch.3)

Each smell is a labelled heuristic (“possible Feature Envy”), never a hard violation. Where a documented repo standard endorses something the baseline would flag, suppress the smell. Skip anything tooling already enforces.

| Smell | What to look for | How to fix |
|---|---|---|
| **Mysterious Name** | A function, variable, or type whose name does not reveal what it does or holds. | Rename it. If no honest name comes, the design is murky. |
| **Duplicated Code** | The same logic shape appears in more than one hunk or file in the change. | Extract the shared shape, call it from both. |
| **Feature Envy** | A method that reaches into another object’s data more than its own. | Move the method onto the data it envies. |
| **Data Clumps** | The same few fields or parameters keep travelling together (a type wanting to be born). | Bundle them into one type, pass that. |
| **Primitive Obsession** | A primitive or string standing in for a domain concept that deserves its own type. | Give the concept its own small type. |
| **Repeated Switches** | The same `switch`/`if`-cascade on the same type recurs across the change. | Replace with polymorphism, or one map both sites share. |
| **Shotgun Surgery** | One logical change forces scattered edits across many files in the diff. | Gather what changes together into one module. |
| **Divergent Change** | One file or module is edited for several unrelated reasons. | Split so each module changes for one reason. |
| **Speculative Generality** | Abstraction, parameters, or hooks added for needs the spec does not have. | Delete it; inline back until a real need shows. |
| **Message Chains** | Long `a.b().c().d()` navigation the caller should not depend on. | Hide the walk behind one method on the first object. |
| **Middle Man** | A class or function that mostly just delegates onward. | Cut it, call the real target direct. |
| **Refused Bequest** | A subclass or implementer that ignores or overrides most of what it inherits. | Drop the inheritance, use composition. |

Apply these checks alongside the existing council-style questions:
- Is this already tracked by an ICW key?
- Is it a true defect, a design tradeoff, or by-design behavior?
- Can it be reproduced or reasoned from control flow/lifecycle?
- What is the blast radius and user-visible impact?

Drop weak findings and keep only defensible items.

### 6. Classify and prioritize findings

- Assign severity and confidence independently.
- Severity reflects impact/risk.
- Confidence reflects evidentiary certainty.
- Prefer priority mapping:
  - P0: crash/data-loss/security/major correctness
  - P1: high reliability/perf hazards likely to impact users
  - P2: architectural limitations and medium risk debt
  - P3: consistency/maintainability cleanups
- Tag each finding with its axis (Standards or Spec) so the report preserves the separation.

### 7. Decide action path per finding

- If existing ICW task already covers it: update/extend that task with sharper acceptance notes.
- If partially covered: create correction note and dependency linkage.
- If not covered: create a new ICW ticket with scope, evidence, validation plan, and next step.

### 8. Produce a findings-only audit artifact

- Create a new timestamped file in docs/audits.
- Include only:
  - net-new findings, grouped by axis (Standards / Spec)
  - corrections/extensions to existing tasks
  - updated priority order
- Do not repeat unchanged prior findings unless needed for dependency context.
- End with a one-line summary: total findings per axis, and the worst issue within each axis.

### 9. Update durable trackers

- Add or refine entries in docs/tasks/active-tasks.md.
- Add matching keys and activity rows in docs/tasks/task-tracker.md.
- Ensure each new key has a ticket file under docs/tasks/tickets.
- Prefer the repo’s existing ICW naming and task conventions so work stays consistent with the current backlog.

### 10. Perform consistency check

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
- If the fixed-point diff is empty or the ref does not resolve:
  - fail early and report the problem instead of proceeding into sub-agent work.

## Completion Criteria

Audit work is complete only when all are true:

- a new audit document is written with net-new findings and task corrections
- findings are reported under separate Standards and Spec headings
- every accepted new finding is captured as a durable ICW task/ticket
- tracker updates are synchronized across active-tasks and task tracker
- priorities and rationale are explicit
- open questions and confidence limits are called out

## Quality Bar

- Every finding must cite concrete file and line references.
- Distinguish hard violations (documented standard breached) from judgement calls (baseline smells). A documented repo standard always overrides the baseline.
- Avoid speculative language unless marked as theoretical with confidence.
- No duplicate backlog creation for already-covered items.
- Recommendations must be implementable, scoped, and testable.

## Output Template

Use this structure for audit files:

1. Executive Summary (net-new only, per axis)
2. Standards Findings (violations and smells with evidence)
3. Spec Findings (missing, wrong, or scope-creep behaviour)
4. Corrections/Extensions to Existing Tasks
5. Priority Order (P0-P3)
6. Open Questions and Validation Gaps

## Example Prompts

- /codebase-audit full-repo net-new only
- /codebase-audit rendering and lifecycle deep dive
- /codebase-audit reconcile backlog coverage against code and ADRs
- /codebase-audit review since HEAD~5 -- focus on spec compliance
- /codebase-audit reconcile backlog coverage against code and ADRs

