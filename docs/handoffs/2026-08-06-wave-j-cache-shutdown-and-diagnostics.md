---
id: wave-j-cache-shutdown-and-diagnostics
status: Complete
created: 2026-08-06
updated: 2026-08-06
---

# Wave J Cache, Shutdown, And Diagnostics Handoff

## Status

Wave J cache accounting and shutdown work is complete. The diagnostics export follow-up is complete. The remaining async event handlers use the safe wrapper boundary.

## Critical Review

The prior implementation correctly removed the coordinator release counter and transferred reservation ownership to `ICacheReservation.Dispose`. Native and mip payload bytes remain in the budget. Variant diagnostics preserve complete cache-key identity.

The prior implementation had two gaps. It did not provide a diagnostics export command. It migrated only window shutdown while other async event handlers remained unchecked. The current change closes both gaps.

The `SafeAsyncEventHandler` method remains `async void` because WPF event delegates require a void return type. Every MainWindow async event entry point calls this wrapper. The wrapper reports failures in the status bar. The dispatcher handler uses the same status surface.

## Delivered

- Added JSON serialization and asynchronous file export for cache diagnostics.
- Added a debug-panel export button with a one-second throttle.
- Migrated MainWindow async event entry points to Task methods behind the safe wrapper.
- Made dispatcher exceptions visible in the main window status surface.
- Added exporter serialization and file-write tests.
- Updated ICW-110, ICW-112, and ICW-134 task records.

## Validation

- `dotnet test tests\\InfiniteCanvas.Tests\\InfiniteCanvas.Tests.csproj --configuration Release --no-restore --filter TileCacheDiagnosticsExporterTests`, 2 tests passed.
- `dotnet build src\\InfiniteCanvas.App\\InfiniteCanvas.App.csproj --configuration Release --no-restore`, passed with the existing unused `_frameClaimantId` warning.
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`, 218 task files validated.
- `git diff --check`, passed.

## Next Wave

Prioritize ICW-P1-COOPERATIVE-CANCEL. Add cancellation checks around each expensive tile-generation phase and test mid-generation cancellation. Then evaluate ICW-P1-GDI-CONCURRENCY with a focused stress test. Do not claim either item complete without runtime or regression evidence.
