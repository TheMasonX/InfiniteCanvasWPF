from pathlib import Path
import re

root = Path('docs/tasks/tickets')
valid_statuses = {'Proposed', 'To Do', 'In Progress', 'In Review', 'Done', 'Archived', 'Blocked', 'Reverted'}
valid_types = {'Task', 'Story', 'Bug', 'Spike', 'Improvement', 'Docs', 'Epic'}
valid_priorities = {'P0', 'P1', 'P2', 'P3'}


def parse_frontmatter(text: str):
    if not text.startswith('---\n') and not text.startswith('---\r\n'):
        return None, text
    lines = text.splitlines()
    if not lines or lines[0].strip() != '---':
        return None, text

    frontmatter = {}
    i = 1
    body_start = None
    while i < len(lines):
        line = lines[i]
        if line.strip() == '---':
            body_start = i + 1
            break
        if ':' in line and not line.startswith(' '):
            key, value = line.split(':', 1)
            frontmatter[key.strip()] = value.strip()
        i += 1
    if body_start is None:
        return frontmatter, ''
    body = '\n'.join(lines[body_start:]).lstrip('\n')
    return frontmatter, body


def derive_title(stem: str, existing: str | None) -> str:
    if existing:
        return existing
    title = re.sub(r'[-_]+', ' ', stem).strip()
    title = title.replace('Icw', 'ICW')
    return title.title()


def derive_key(stem: str, existing: str | None) -> str:
    if existing:
        return existing
    match = re.search(r'(ICW|REQ|TESTS|TSK)-\d+', stem, re.IGNORECASE)
    if match:
        return match.group(0).upper()
    return 'ICW-999'


def derive_id(stem: str, existing: str | None) -> str:
    if existing:
        return existing
    return stem


def derive_status(existing: str | None) -> str:
    if existing and existing in valid_statuses:
        return existing
    return 'Proposed'


def derive_type(existing: str | None) -> str:
    if existing and existing in valid_types:
        return existing
    return 'Task'


def derive_priority(existing: str | None) -> str:
    if existing and existing in valid_priorities:
        return existing
    return 'P2'


def derive_tags(stem: str, existing: str | None):
    if existing:
        return existing
    prefix = stem.split('-', 1)[0].lower()
    if prefix == 'icw':
        return ['icw', 'task-tracker']
    if prefix == 'tests':
        return ['tests', 'task-tracker']
    return ['backlog', 'task-tracker']


updated = 0
for path in sorted(root.glob('*.md')):
    text = path.read_text(encoding='utf-8')
    frontmatter, body = parse_frontmatter(text)
    stem = path.stem

    if frontmatter is not None:
        existing_title = frontmatter.get('title')
        existing_key = frontmatter.get('key')
        existing_id = frontmatter.get('id')
        existing_status = frontmatter.get('status')
        existing_type = frontmatter.get('type')
        existing_priority = frontmatter.get('priority')
        existing_tags = frontmatter.get('tags')
    else:
        existing_title = None
        existing_key = None
        existing_id = None
        existing_status = None
        existing_type = None
        existing_priority = None
        existing_tags = None

    title = derive_title(stem, existing_title)
    key = derive_key(stem, existing_key)
    task_id = derive_id(stem, existing_id)
    status = derive_status(existing_status)
    task_type = derive_type(existing_type)
    priority = derive_priority(existing_priority)
    tags = derive_tags(stem, existing_tags)

    if isinstance(tags, str):
        tags = [tags]
    if not tags:
        tags = ['task-tracker']

    tag_lines = '\n'.join(f'  - {tag}' for tag in tags)
    frontmatter_text = f'''---
id: {task_id}
key: {key}
title: {title}
status: {status}
type: {task_type}
priority: {priority}
tags:
{tag_lines}
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

'''
    if body.strip():
        new_text = frontmatter_text + body.strip() + '\n'
    else:
        new_text = frontmatter_text + 'Describe the task scope and implementation intent.\n'

    path.write_text(new_text, encoding='utf-8')
    updated += 1

print(f'Updated {updated} task file(s).')
