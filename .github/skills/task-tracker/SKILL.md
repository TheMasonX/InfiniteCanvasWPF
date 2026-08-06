---
name: task-tracker
description: 'Create and maintain markdown task records for InfiniteCanvasWPF with a parser-friendly schema inspired by MemorySmith JSON task metadata. Use for backlog grooming, ticket creation, status updates, and task searchability.'
argument-hint: 'Describe the task, scope, status, and any related files or tickets'
---

# Task Tracker

## Outcome

Create or update repository tasks that are:

- easy for humans to read in markdown
- easy for tools to parse for search, graph links, tag filters, and status queries
- consistent with the repository task conventions in docs/tasks/README.md
- linked to the right backlog artifacts such as tickets, ADRs, requirements, or implementation files
- normalized into the same ICW task format across the repository task corpus
- ready to support sprint planning, execution, and handoff review

## When to Use

Use this skill when asked to:

- create a new task or ticket
- update an existing task's status, scope, or validation notes
- add or refine tags, dependencies, and related task links
- turn a bug report or handoff note into a durable tracker entry
- improve the task schema or task template

## Inputs

- task intent: summary, scope, acceptance criteria, validation command
- repository context: docs/tasks/README.md, docs/tasks/active-tasks.md, docs/tasks/task-tracker.md, docs/tasks/tickets/
- optional references: ADRs, requirements registry, implementation files, related tasks
- optional schema preference: keep it markdown-first and parser-friendly

## Procedure

1. Review the task conventions
- Read docs/tasks/README.md and docs/tasks/TASK_SCHEMA.md.
- Check the existing backlog in docs/tasks/active-tasks.md and any relevant ticket in docs/tasks/tickets/.
- Follow the repository's naming pattern and keep the task metadata consistent.

2. Capture core task identity
- Choose a task id and key that match repository naming conventions.
- Write a short, specific title.
- Pick a status, type, and priority.
- If the task is an ICW item, use an ICW-style id and key.

3. Fill the parser-friendly metadata
- Use the markdown frontmatter block at the top of the task.
- Include fields such as id, key, title, status, type, priority, tags, dependsOn, related, and links.
- Keep values stable and lowercase-kebab-case for tags where possible.
- Use the same section order for every task file.

4. Write the human-readable body
- Add a concise summary.
- List the scope and any acceptance criteria.
- Include the validation command and current outcome.
- Add notes, blockers, and related task references.
- Keep the content factual and implementation-oriented.

5. Link to the right artifacts
- Link the task to implementation files, ADRs, requirements, or sibling tickets when useful.
- If the task is a follow-up to another task, place it in dependsOn or related.
- If the task represents a sprint outcome, note the relevant handoff artifact or next-step review.

6. Normalize the task corpus when needed
- If multiple legacy task files use different formats, convert them to the same ICW task structure.
- Do not leave a mix of old and new task layouts in the same backlog area.
- Preserve the useful content from older files while bringing the format into alignment.

7. Validate the task
- Run the task validation script with the relevant markdown file or the docs/tasks folder.
- Fix any schema or formatting issues before finishing.

8. Prepare for handoff
- If the work is part of a sprint or multi-step implementation batch, create or update a handoff note under docs/handoffs/ before commit/push.
- Capture the current state, important findings, and next recommended step in the handoff note.

## Suggested Schema Fields

Use these fields in the task frontmatter:

- id: stable unique identifier
- key: short human-readable key such as ICW-001 or TSK-0001
- title: concise task title
- status: Proposed, To Do, In Progress, In Review, Done, Archived, or Blocked
- type: Task, Story, Bug, Spike, Improvement, Docs, or Epic
- priority: P0, P1, P2, or P3
- tags: machine-searchable labels
- dependsOn: task ids that must happen first
- related: task ids or references that are adjacent or informative
- links: repository files or docs that provide context

## Completion Criteria

A task is complete only when all are true:

- the task exists in the repository task tracker or a ticket file
- the markdown frontmatter is present and valid
- the task records status, scope, validation, and next step clearly
- related links and tags make the task searchable and linkable
- the task follows the same ICW task structure as the rest of the backlog
- if relevant to a sprint boundary, a handoff note has been created or updated

## Quality Bar

- Prefer explicit metadata over free-form prose where searchability matters.
- Keep the markdown body readable for humans and the frontmatter stable for parsers.
- Do not duplicate information across multiple trackers; keep one canonical task record.
- If the schema needs to evolve, update docs/tasks/TASK_SCHEMA.md and the template together.

## Reference Files

- docs/tasks/README.md
- docs/tasks/TASK_SCHEMA.md
- docs/tasks/templates/task-template.md
- scripts/Validate-TaskTracker.ps1
- docs/handoffs/

## Example Prompts

- /task-tracker create a task for the README refresh work with status To Do and tags docs,readme
- /task-tracker update ICW-064 with the latest validation evidence and link the benchmark file
- /task-tracker add a ticket for a new rendering bug with related tasks and a validation command

