---
id: ICW-189
status: To Do
key: ICW-189
title: Add `GenerateSet(GeneratorOptions)` adapter and preserve deprecation forwarding
type: Task
priority: P3
tags: [rendering, api-design, backward-compatibility]
created: 2026-08-02
updated: 2026-08-03
---

Summary
- Provide a single canonical public entry point `GenerateSet(GeneratorOptions)` and implement thin forwarding overloads that call into it. This creates a deprecation migration path while avoiding immediate breaking changes.

Scope
- Implement adapter overload(s) in `SampleImageGenerator`, update XML docs to mark the old overloads as forwarded/deprecated, and add tests demonstrating behavioral parity.

Validation
- Both the new `GeneratorOptions`-based call and the existing overloads produce identical tile IDs and pixel content for representative seeds. Build and unit tests pass.

Next step
- Implement the adapter overload, add XML doc deprecation notes on old signatures, and add parity unit tests. Keep forwarding overloads for one release before removal.

Council update, 2026-08-03
- Register this existing ticket in `docs/tasks/active-tasks.md` and `docs/tasks/JIRA.md`.
- Verify direct callers, XML deprecation behavior, and pixel and tile-ID parity before closing the migration path.
