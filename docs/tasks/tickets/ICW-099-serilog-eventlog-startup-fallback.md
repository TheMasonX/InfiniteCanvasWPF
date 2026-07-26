---
id: ICW-099
status: Proposed
summary: Harden Serilog EventLog sink initialization to avoid startup crash on non-admin machines
assignee: TBD
priority: Critical
labels:
  - logging
  - startup
  - reliability
validation: pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks && dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter SerilogHostInitializationTests
---

## Problem
`SerilogHost.CreateLogger()` constructs the EventLog sink with `manageEventSource: true` synchronously during `App.OnStartup` before global exception handlers are registered. On a non-admin machine this can throw `SecurityException`, crashing startup before the safety net exists.

## Evidence
- `src/InfiniteCanvas.App/Logging/SerilogHost.cs` uses `.WriteTo.EventLog("InfiniteCanvas", manageEventSource: true, ...)`.
- `src/InfiniteCanvas.App/App.xaml.cs` sets `Log.Logger = SerilogHost.Logger` before registering dispatcher/AppDomain/TaskScheduler exception handlers.

## Recommendation
- Set `manageEventSource: false` and document that EventLog source creation is a manual admin step, OR
- Wrap logger construction in try/catch, and on failure fall back to file-only sinks and continue startup. Ensure the fallback occurs before any code that depends on `Log.Logger` runs.

## Estimate
- 2-4 hours to implement and test fallback; 1 day to add Windows-specific first-run smoke test harness.

## Risks
- Swallowing EventLog failures silently may hide an environment misconfiguration; ensure fallback emits a local file trace and a developer-visible startup log entry.
