---
id: ICW-100
key: ICW-100
status: Proposed
title: Reconcile duplicate and orphaned ticket IDs in docs/tasks/tickets
type: Task
priority: P2
tags:
  - process
  - backlog
  - docs
assignee: TBD
summary: Reconcile duplicate and orphaned ticket IDs in `docs/tasks/tickets/`
validation: pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks && git status --porcelain | Out-String
---

## Problem
Inventory found duplicated ticket numeric IDs across multiple files (ICW-061..ICW-065 duplicated), causing tracker divergence and invisible ticket files in `docs/tasks/JIRA.md`.

## Evidence
`ls docs/tasks/tickets/ | grep -oE "ICW-[0-9]+" | sort | uniq -c` shows duplicates for ICW-061..ICW-065; the `JIRA.md` references a different file for some of these IDs, leaving the other copy effectively orphaned.

## Recommendation
- Identify canonical file per numeric ID and either merge or supersede duplicates.
- Update front-matter `id:` fields to unique canonical IDs.
- Update `docs/tasks/JIRA.md` mapping rows to reference the canonical files.
- Extend `scripts/Validate-TaskTracker.ps1` to fail when duplicate `id:` values are detected.

## Estimate
- 1d to triage and merge duplicates, 1d to update scripts and validations, 2-3h to run reconciliation and commit.

## Risks
- Merging could accidentally change historical context or lose references; keep redundant copies as archived superseded files with `superseded-by:` front-matter if necessary.
