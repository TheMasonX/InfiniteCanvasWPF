---
id: ICW-331
author: Copilot
key: ICW-331
title: Clean audit reports and durable planning records of product-specific references
status: Done
type: Docs
priority: P1
tags:
  - documentation
  - audit
  - secret-safe
  - requirements
dependsOn: []
related:
  - ICW-147
  - REQ-001
links:
  - docs/audits/
  - docs/requirements/functional-requirements-and-invariants.md
  - docs/tasks/active-tasks.md
  - docs/tasks/JIRA.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-331 - Clean audit reports and durable planning records of product-specific references

## Summary

Remove product-specific names, private provenance, and internal source references from the supplied audit reports. Preserve the general engineering findings, use cases, requirements, and validation needs through neutral wording.

## Scope

- Rename the 11 supplied audit reports to generic audit filenames.
- Rewrite report metadata, findings, requirements, and examples with neutral viewport terminology.
- Remove private author, repository, source-snapshot, product, and internal-service references.
- Sanitize the historical audit corpus of external audit document titles, assistant product names, and repository archive/API host references.
- Preserve generic findings for frame ownership, scene atomicity, source identity, layers, thresholds, live state, caching, cancellation, input, diagnostics, and runtime validation.
- Update the requirements registry and both task trackers with the neutral requirement.

## Acceptance Criteria

- No supplied report contains the removed product names, private author names, private source locations, or internal service names.
- No historical audit file contains the external audit document titles, assistant product names, or repository archive/API host references.
- Report filenames and internal links use generic audit terminology.
- General findings and use cases remain available for future implementation planning.
- Requirements and task records contain only neutral names and repository-local evidence.
- The task validator reports no new errors attributable to this task.

## Validation

- Command: `rg -n -i "product-specific|private-source|internal-service" docs/audits docs/requirements docs/tasks`
- Result: Cleaned report set has no protected product or provenance terms. Full audit corpus (101 files) passed the protected-term scan, including the historical audit files. The full tracker validator passes with 218 task files validated and 5 legacy markdown files skipped.

## Notes

- This task changes documentation and planning records only.
- Product-specific adapter mappings belong outside the reusable repository contracts.
- The cleanup must retain enough context to preserve engineering intent without exposing product internals.

## Related Tasks

- ICW-147
- REQ-001

