---
id: ICW-188
status: To Do
key: ICW-188
title: Introduce `GeneratorOptions` and `MipOptions` records for generator API
type: Task
priority: P3
tags: [rendering, api-design, refactor]
created: 2026-08-02
updated: 2026-08-03
---

Summary
- Reduce long parameter lists in `SampleImageGenerator` by introducing small immutable option/value records: `GeneratorOptions` for image-level settings and `MipOptions` for per-mip generation inputs.

Scope
- Add `GeneratorOptions` and `MipOptions` records (with sensible defaults and XML docs), wire the primary `GenerateSet` and `GenerateMipPixels` paths to accept these option types, and provide minimal forwarding overloads to preserve existing public signatures.

Validation
- New option records exist and are used in the primary generator entry points. Forwarding overloads compile and behave identically for representative seeds (add parity unit tests).

Next step
- Implement the records in `src/InfiniteCanvas.Rendering` and add a small adapter overload to `SampleImageGenerator.GenerateSet(GeneratorOptions)`. Add two unit tests that compare representative pixel outputs before/after.

Council update, 2026-08-03
- Register this existing ticket in `docs/tasks/active-tasks.md` and `docs/tasks/JIRA.md`.
- Keep the option records and the adapter as separate acceptance surfaces. Confirm that `MipOptions` has direct production callers before marking the task complete.
