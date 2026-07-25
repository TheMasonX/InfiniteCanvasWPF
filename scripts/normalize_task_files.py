from pathlib import Path
import re

root = Path('docs/tasks/tickets')
for path in sorted(root.glob('*.md')):
    text = path.read_text(encoding='utf-8')
    if text.startswith('---\n'):
        continue

    name = path.name
    stem = path.stem
    title = stem.replace('-', ' ').replace('_', ' ').strip()
    if title.lower().startswith('icw'):
        title = title
    key = stem.split('-', 1)[0] if stem.startswith('ICW-') else 'ICW-999'

    body = text.strip()
    if body.startswith('#'):
        body = re.sub(r'^#\s*.*?\n+', '', body, count=1)
    body = body.strip()
    if not body:
        body = 'Describe the task scope and implementation intent.'

    summary_line = body.splitlines()[0].strip()
    if len(summary_line) > 160:
        summary_line = summary_line[:157] + '...'

    frontmatter = f'''---
id: {stem}
author: Copilot
key: {key}
title: {title.title()}
status: Proposed
type: Task
priority: P2
tags:
  - task-tracker
  - icw
  - backlog
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# {stem}

## Summary

{summary_line}

## Scope

- Review and update the relevant implementation area.
- Capture the acceptance criteria and validation path.

## Acceptance Criteria

- The task has a clear implementation goal.
- The task is linked to the relevant files or design notes.
- The validation command and outcome are recorded.

## Validation

- Command: dotnet test tests/InfiniteCanvas.Tests --configuration Release
- Result: To be completed when implemented.

## Notes

- Add implementation details, blockers, or follow-up questions here.

## Related Tasks

- ICW-000
'''
    path.write_text(frontmatter, encoding='utf-8')
