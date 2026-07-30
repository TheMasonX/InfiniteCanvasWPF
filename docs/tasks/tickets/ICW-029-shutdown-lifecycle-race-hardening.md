---
id: ICW-029-shutdown-lifecycle-race-hardening
author: External Audit (Integration-1)
key: ICW-029
title: Harden shutdown lifecycle to prevent render/regenerate disposal races
status: To Do
type: Bug
priority: P1
tags:
  - lifecycle
  - shutdown
  - disposal
  - race
  - safety
dependsOn:
  - ICW-P0-TRANSACTIONAL-REGEN
related:
  - ICW-102
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-25
updated: 2026-07-30
---

# ICW-029 — Harden shutdown lifecycle to prevent render/regenerate disposal races

## Summary

**Critical gap (80% confidence):** `OnClosed` (`MainWindow.xaml.cs:1413-1429`) cancels `_lifetime`, awaits `_renderAction.DisposeAsync()`, then immediately disposes `_frontBitmapFactory`/`_backBitmapFactory`/`_tileCoordinator` — **without ever acquiring or waiting on `_generationGate`**. If a user closes the window while `RegenerateSceneAsync` is mid-flight (holding the gate, inside `Task.Run(() => SampleImageGenerator.GenerateSet(...))` or awaiting `_spatialIndex.PublishSnapshotAsync`), that background task can throw `ObjectDisposedException` against the just-disposed coordinator, or race against buffer disposal.

**Evidence:** External audit confirmed exact mechanism at `MainWindow.xaml.cs:1413-1429`. No await on `_generationGate` before disposal.

## Root Cause

The shutdown sequence is:
1. Cancel `_lifetime` token
2. `await _renderAction.DisposeAsync()` (drains in-flight render work)
3. Dispose `_frontBitmapFactory`, `_backBitmapFactory`
4. Dispose `_tileCoordinator`

Missing: `await _generationGate.WaitAsync()` between steps 1 and 3. If `RegenerateSceneAsync` is mid-flight in step 2 (it runs under `_generationGate`, not `_renderAction`), it continues executing after step 4 and hits disposed objects.

## Scope

### Required Changes

1. **In `OnClosed`, before disposing shared resources:**
   ```csharp
   await _generationGate.WaitAsync();  // with short timeout guard
   ```
   - Use a timeout (e.g., 5 seconds) since `_lifetime.Cancel()` should make any well-behaved in-flight generation observe cancellation and release the gate promptly.
   - Only once ICW-P0-TRANSACTIONAL-REGEN's exception handling is in place; today an unhandled exception path could leave the gate held indefinitely.

2. **Ordering:**
   - Cancel `_lifetime` first (already done).
   - *Then* wait for the gate.
   - *Then* dispose the coordinator/buffers.
   - This sequencing prevents the coordinator from being disposed while `GenerateSet` is still running under it.

3. **Add a close-stress test** that triggers `RegenerateSceneAsync` and immediately calls `OnClosed`, repeated N times, asserting no unhandled `ObjectDisposedException` is logged.

### Dependency on ICW-P0-TRANSACTIONAL-REGEN

ICW-P0-TRANSACTIONAL-REGEN must land first because:
- Its rollback logic is the safety net for mid-flight generation failures.
- Without it, an unhandled exception in `RegenerateSceneAsync` could leave `_generationGate` held (released in `finally`, but a truly unhandled exception could skip even that).
- The close-stress test would produce false failures if regen can fail in an uncontrolled way.

### Acceptance Criteria

- Closing the app while `RegenerateSceneAsync` is mid-flight does not produce `ObjectDisposedException`.
- The gate is acquired (with timeout) before disposal of coordinator and buffers.
- Close-stress test passes with no unhandled exceptions logged.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Add `await _generationGate.WaitAsync()` before disposal in `OnClosed` |
| `tests/InfiniteCanvas.Tests/MainWindowCloseTests.cs` (or equivalent) | Add close-stress regression test |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "CloseStress|ShutdownRace"
```

## Related Tasks

- ICW-P0-TRANSACTIONAL-REGEN: prerequisite (exception-safe regen needed before gate-wait is reliable)
- ICW-102: defect bitmap pool disposal (related lifecycle concern)
