---
id: ICW-147
author: Copilot
key: ICW-147
title: Create audit synthesis skill and evidence-first report format
status: Done
type: Docs
priority: P2
tags:
  - audit
  - customization
  - documentation
  - evidence
  - reporting
dependsOn: []
related:
  - ICW-038
  - ICW-081
links:
  - .github/skills/audit-synthesis/SKILL.md
  - docs/formats/report-FORMAT.md
  - .github/skills/codebase-audit/SKILL.md
  - docs/tasks/TASK_SCHEMA.md
created: 2026-08-02
updated: 2026-08-03
---

## Summary

Create a reusable `audit-synthesis` skill for skeptical, source-verified synthesis of external audits. Refine the audit report format so findings, provenance, confidence, assumptions, open questions, requests, and task disposition remain traceable.

## Scope

- Add `.github/skills/audit-synthesis/SKILL.md`.
- Update `docs/formats/report-FORMAT.md`.
- Register the task in `docs/tasks/active-tasks.md` and `docs/tasks/task-tracker.md`.

## Acceptance Criteria

- The skill defines inputs, source inventory, claim verification, deduplication, disposition, report generation, and completion checks.
- The skill references `docs/formats/report-FORMAT.md` as the required output contract.
- The report format uses clear headings and includes source IDs, evidence status, severity, confidence, provenance, task disposition, task linkage, assumptions, open questions, and requests.
- The report format preserves independent analysis and does not treat external audit claims as verified facts.
- Tracker entries identify validation commands and the next step.
- The audit workflow creates a master findings list before council review.
- The council workflow uses three independent seats by default.
- Delegated work writes recovery artifacts under the user-supplied directory, or `D:\Temp\Subagents\<run-id>\` by default.

## Validation

- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`, which reports unrelated legacy ticket errors and the existing ICW-148 type error.
- Targeted diagnostics for the new skill, report format, and ICW-147 ticket report no errors.
- PowerShell structural checks confirm skill frontmatter, report reference, fill-out guidance, provenance, task disposition, source ledger, and requests sections.
- Targeted diagnostics pass for the audit, council, and subagent-swarm skills after the workflow update.

The tracker command still fails on pre-existing legacy ticket errors, including missing frontmatter fields, unsupported status values, and the existing ICW-148 type error. The ICW-147 ticket passes the required frontmatter shape.

## Notes

The user explicitly requested documentation edits. Existing untracked audit reports and unrelated submodule changes remain untouched.

## Related Tasks

- ICW-038 tracks durable audit artifact capture.
- ICW-081 tracks duplicate and orphaned audit ticket reconciliation.

## Follow-up

Use the master findings list as the common evidence pack for the default three-seat council.
Keep delegated prompts, notes, and results in the recovery workspace until the final report passes validation.

