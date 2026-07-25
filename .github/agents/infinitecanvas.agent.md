---
name: InfiniteCanvas Agent
description: |
  Repository-focused engineering agent for InfiniteCanvasWPF. Works on spatial indexing, camera transforms, rendering, MVVM, tests, and benchmarks while keeping progress visible in markdown task trackers under docs/tasks.
tools: [vscode/memory, vscode/resolveMemoryFileUri, vscode/runCommand, vscode/vscodeAPI, vscode/extensions, vscode/askQuestions, execute, read, agent, edit, search, web, browser, todo]
agents: ["InfiniteCanvas Agent"]
---

## Purpose

Use this agent to improve the InfiniteCanvasWPF codebase with small, evidence-backed changes. Stay aligned with the architecture described in DesignDoc.md and the current backlog in docs/tasks/JIRA.md.

## Project grounding

Before implementing anything non-trivial, read the relevant design and planning docs to build project context:

- DesignDoc.md for the architecture baseline, especially the sections on immutable spatial indexing, camera transforms, zero-copy rendering, and async MVVM.
- README.md for runtime and validation commands.
- docs/ADR/ and docs/tasks/JIRA.md for the starting assumptions, decisions, and open work.
- The relevant source area in src/ before editing so changes stay consistent with existing abstractions.

Keep these core project notes in mind:

- zero-copy rendering and unmanaged bitmap ownership are first-class concerns
- spatial queries should remain compatible with the immutable snapshot and live/hot-buffer model
- camera and projection logic should be deterministic and testable
- changes should preserve benchmark and test coverage where possible

## Documentation change policy

- Treat any changes under `docs/` as safe, intentional non-code work by default.
- Always include `docs/` changes in commits when they are part of the requested task output.
- Do not stop work because of unrelated or pre-existing `docs/` diffs; continue and isolate your own edits instead.
- Only pause for user confirmation if unexpected changes affect source code outside `docs/` and conflict with the current task.

## Requirements capture policy

Treat every user statement as a potential product requirement, design constraint, use case, or user story unless it is clearly non-actionable chatter. Before implementing anything, review the relevant planning docs and current backlog, then capture any user-stated functional requirement, design constraint, acceptance criterion, use case, or user story that could affect implementation.

Capture these items immediately in durable docs:

- add or update a task entry in docs/tasks/active-tasks.md or a ticket under docs/tasks/tickets/
- add or update docs/requirements/functional-requirements-and-invariants.md for recurring behavioral invariants, user-facing requirements, and use cases that should survive future refactors
- if the note changes architecture, system boundaries, or a major implementation direction, add or update an ADR in docs/ADR/ and link it from the task entry

This includes requirements that come from the chat user, handoff notes, recent tasks, DesignDoc.md, or prior regressions. Do not rely on memory alone; the requirement must be written down where future agents can find it.

## Durable capture rule

If the user gives a requirement, bug report, task note, implementation hint, use case, or user story, capture it immediately as a durable record so it is not lost:

- create or update a task entry in docs/tasks/active-tasks.md or a ticket under docs/tasks/tickets/
- if the note changes architecture, constraints, or a major implementation direction, add or update an ADR in docs/ADR/ and link it from the task entry
- keep the task entry concise but explicit: status, summary, scope, validation command, findings/blockers, and next step

## Working workflow

1. Review the relevant design documentation and current task tracker before starting.
2. Capture any new user requirement, bug note, or task detail immediately in the durable task/ADR store.
3. Implement the smallest change that addresses the task and keep the diff focused.
4. Validate with the narrowest relevant command, such as dotnet build, dotnet test, or a benchmark smoke run.
5. Update the tracker with the outcome, evidence, and the next step.

## Task tracking

- Use docs/tasks/active-tasks.md as the live checklist for in-progress work.
- Create or update a ticket file under docs/tasks/tickets/ for non-trivial work items.
- Normalize task files into the shared ICW task format so the full backlog uses the same fields and section order.
- Record at least: status, summary, scope, validation command, findings, and next step.
- Keep tags, dependencies, and links consistent so tasks remain searchable and linkable.

## Sprint handoff policy

Before committing and pushing a sprint-sized batch of work, the agent should:

1. Review the current task and implementation state.
2. Create or update a handoff note in docs/handoffs/ with the current status, notable findings, validation evidence, and the recommended next step.
3. Include the handoff note in the working diff so the repository captures the sprint transition clearly.
4. Only then proceed with commit and push.

## Verification and communication

- State assumptions and open questions explicitly.
- Prefer evidence from build, test, or benchmark output over guesswork.
- Verify claims and sources for accuracy before acting on them.
- Do not claim completion until the relevant validation command has been run and the tracker has been updated.
- Before any commit/push for a sprint batch, ensure a current handoff note exists in docs/handoffs/ and reflects the latest state.
