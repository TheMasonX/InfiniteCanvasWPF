---
name: subagent-swarm
description: 'Coordinate a focused multi-agent workflow for repository work that needs parallel investigation, evidence gathering, and staged implementation. Use this when a task spans several subsystems or when a single agent would be blocked by too much context.'
argument-hint: 'Task summary and scope, such as audit + implementation + docs or rendering + tests + benchmarks'
---

# Subagent Swarm

## Outcome

Produce a coordinated execution plan that splits a complex repository task into parallel, low-risk workstreams while preserving shared constraints and evidence quality.

## When to Use

Use this skill when a task:

- spans multiple subsystems such as rendering, spatial indexing, view models, and tests
- benefits from parallel investigation before implementation
- needs a staged handoff between research, implementation, verification, and documentation
- would otherwise exceed the context window or dilute focus if handled by one pass

Do not use this skill for a single-file change or a trivial bug fix.

## Inputs

- task summary and likely scope
- target subsystems and expected deliverables
- constraints such as benchmark safety, test scope, or docs updates
- any existing tracker entries, ADRs, or requirements that should guide the work
- optional user-supplied recovery directory for delegated artifacts

## Recovery Workspace

Resolve the recovery directory before creating workstreams.
Use the user-supplied path when present. Otherwise use `D:\Temp\Subagents\<run-id>\`.
Create the directory and record it in the swarm manifest.

Every subagent must write its prompt, working notes, evidence, output, and handoff to its own child directory under the recovery root.
Subagents must not use the operating system temporary directory for intermediate files.
Subagents must not write directly to repository source, task, or report files during investigation.
The coordinator applies verified changes after reconciliation.

If the recovery directory cannot be created or written, stop the swarm and report the filesystem error.

## Procedure

1. Frame the work into parallel tracks
- Split the request into 2-4 workstreams such as:
  - repo context and requirements review
  - implementation and code changes
  - tests/benchmarks/validation
  - docs and task tracker updates
- Keep each track scoped to a concrete deliverable.
- Create one recovery child directory for each track before delegation.

2. Preserve shared repository context
- Before execution, ensure each track uses the same baseline artifacts:
  - DesignDoc.md
  - README.md
  - docs/tasks/active-tasks.md
  - docs/tasks/task-tracker.md
  - relevant ADRs and requirements docs
- Avoid conflicting assumptions between tracks.
- Give every track the same recovery root and baseline manifest.

3. Assign concrete evidence goals
- Each track should end with a verifiable output such as:
  - a code change or refactor
  - a new or updated test
  - a benchmark or build result
  - a task tracker or documentation update
- Do not hand off vague “investigate” work without a finish condition.

4. Sequence the work in stages
- Stage A: gather context and identify the safest implementation boundary.
- Stage B: implement the smallest change that satisfies the task.
- Stage C: validate via the narrowest relevant command.
- Stage D: update docs, tasks, and any related ADRs.

5. Reconcile findings before completion
- Merge results from each stream into a single summary.
- Check for contradictions, duplicate work, or drift from the original requirement.
- Highlight any unresolved risk or follow-up task that should be tracked.
- Preserve each subagent artifact in the recovery workspace for replay and failure recovery.

## Decision Points

- If the task can be implemented in one focused subsystem, use a single-agent path instead of a swarm.
- If the task affects multiple layers that cannot be safely changed in one pass, create a staged plan with explicit handoffs.
- If validation would be expensive, prefer the narrowest relevant build or test target first.

## Completion Criteria

The swarm workflow is complete only when all are true:

- each workstream has a concrete deliverable
- the implementation is consistent with repository architecture and task tracker guidance
- validation evidence is captured
- docs and task records are updated to reflect the outcome

## Quality Bar

- Prefer evidence over assumptions.
- Keep each substream compact and independent.
- Avoid speculative refactors that are unrelated to the task.
- Preserve the repo’s existing architecture and task-tracking conventions.

## Example Prompts

- /subagent-swarm audit the rendering pipeline and produce a focused implementation plan
- /subagent-swarm investigate the spatial index and tests, then implement the smallest safe fix
- /subagent-swarm split the work into research, implementation, and validation tracks for the current backlog item

