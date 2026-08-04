---
id: ICW-099
author: External Audit (Integration-1)
key: ICW-099
title: Harden Serilog EventLog sink initialization (STALE - see description)
status: Archived
type: Task
priority: P3
tags:
  - logging
  - startup
  - stale
dependsOn: []
related:
  - ICW-014
links:
  - src/InfiniteCanvas.App/Logging/SerilogHost.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-26
updated: 2026-07-30
---

# ICW-099 — Harden Serilog EventLog sink initialization (STALE)

## Status: Deprecated

**This ticket is no longer accurate at HEAD.** The specific defect described has been fixed.

## What was claimed

`SerilogHost.CreateLogger()` constructs the EventLog sink with `manageEventSource: true` synchronously during `App.OnStartup` before global exception handlers are registered. On a non-admin machine this can throw `SecurityException`, crashing startup before the safety net exists.

## What the audit found

**External audit (75% confidence):** `SerilogHost.CreateLogger()` already wraps the `WriteTo.EventLog(...)` call in try/catch with a file-only fallback. The fallback was verified at `SerilogHost.cs:34-41`.

**Residual uncertainty (25%):** The audit did not trace whether `Serilog.Sinks.EventLog`'s admin-rights failure can occur outside this try block, e.g., lazily on first write. This should be verified before the ticket is fully closed.

## Verification Steps

1. Confirm (via spike on a non-admin test machine, or reading `Serilog.Sinks.EventLog` source) that `WriteTo.EventLog(...)`'s admin-rights check happens synchronously inside the fluent call (covered by existing try/catch) and not lazily on first log write outside the try block.
2. If the check is synchronous and caught, close this ticket. If lazy, expand the try/catch to cover the first-write path.

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter SerilogHostInitializationTests
```
