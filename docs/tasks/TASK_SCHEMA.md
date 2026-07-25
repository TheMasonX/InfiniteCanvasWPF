# Task Schema

The repository task tracker uses markdown files with a small YAML-style frontmatter block for machine-readable metadata. This keeps the task files human-readable while still supporting search, graph linking, status filtering, and tag-based queries.

## Why this shape

This format is intentionally inspired by the structured task records used in MemorySmith JSON data, but kept in markdown so it remains easy to edit and review inside the repository.

## Required frontmatter fields

- id: Stable unique identifier for the task.
- key: Short key such as ICW-001 or TSK-0001.
- title: One-line task title.
- status: One of Proposed, To Do, In Progress, In Review, Done, Archived, or Blocked.
- type: One of Task, Story, Bug, Spike, Improvement, Docs, or Epic.
- priority: One of P0, P1, P2, or P3.
- tags: Array of lowercase kebab-case labels for searchability.

## Recommended frontmatter fields

- dependsOn: Array of task ids that must complete first.
- related: Array of adjacent or contextual task ids.
- links: Array of repository-relative files or docs supporting the task.
- created: ISO date string such as 2026-07-25.
- updated: ISO date string such as 2026-07-25.

## Body sections

Every task should include:

- Summary: One or two sentences that describe the problem or opportunity.
- Scope: Bullet list of files, subsystems, or user-facing areas affected.
- Acceptance Criteria: Verifiable outcomes the task should satisfy.
- Validation: The command to run and the observed result.
- Notes: Blockers, implementation considerations, or follow-up questions.
- Related Tasks: Links to dependent or related task ids.

## Example frontmatter

```md
---
id: ICW-999
author: Copilot
key: ICW-999
title: Example task title
status: Proposed
type: Task
priority: P2
tags:
  - rendering
  - ui
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---
```

## Parsing guidance

The frontmatter is designed to be easy to parse with lightweight tooling:

- keep arrays as YAML lists
- use stable lowercase tags
- keep the body sections in a consistent order
- prefer relative links for repository paths
