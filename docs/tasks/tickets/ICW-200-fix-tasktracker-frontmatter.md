---
id: ICW-200
key: ICW-200
title: Normalize task frontmatter to satisfy validator
status: Done
type: Task
priority: P2
tags: [process, tooling]
created: 2026-08-03
updated: 2026-08-03
---

Summary:
- Apply minimal frontmatter fixes across `docs/tasks/tickets/` so `scripts/Validate-TaskTracker.ps1` validates all ticket files.

Scope:
- Add missing `key:` fields where `id:` existed.
- Normalize `status` values to the allowed set (e.g., `todo` -> `To Do`, `Deprecated` -> `Archived`).
- Replace `labels:` with `tags:` and add missing `title`, `type`, or `priority` where required.

Validation:
- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks` passes with zero reported issues.

Findings:
- Several legacy ticket files used inconsistent frontmatter keys and values.
- A duplicated numeric ID (ICW-098) remains; a separate reconciliation ticket exists: ICW-100.

Next Steps:
- Consider extending `scripts/Validate-TaskTracker.ps1` to detect duplicate `id:` values.
- If desired, review each updated ticket for more precise `type`/`priority` values.
