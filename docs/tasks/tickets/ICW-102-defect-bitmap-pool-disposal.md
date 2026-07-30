---
id: ICW-102
author: External Audit (Integration-1)
key: ICW-102
title: Implement owned disposal of bitmap pool on tile eviction/regeneration with concurrency guard
status: To Do
type: Bug
priority: P1
tags:
  - rendering
  - disposal
  - concurrency
  - lifecycle
  - safety
dependsOn: []
related:
  - ICW-P0-TRANSACTIONAL-REGEN
  - ICW-103
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-102 — Implement owned disposal of bitmap pool on tile eviction/regeneration with concurrency guard

## Summary

**Audit finding (80% confidence):** Defect-template bitmaps (`System.Drawing.Bitmap` from `DefectTemplateFactory`) are never disposed during tile eviction or scene regeneration. Additionally, `DisposeDefectTemplatePools` is not fenced against concurrent in-flight render — `_tileCoordinator.CancelAll()` alone is insufficient because it cancels coordinator work, not the render pipeline's own `Task.Run` in `RenderFrameAsync`.

## Root Cause

Two separate issues:

1. **Missing disposal:** `SampleImageTile.DisposeDefectTemplatePools` exists (called from `RegenerateSceneAsync`) but it only clears the dictionary — it does not dispose the `System.Drawing.Bitmap` instances held as values. `ICW-103` assumed a `Dispose` existed, but the audit confirmed the bitmaps are never disposed.

2. **Concurrency race:** `RegenerateSceneAsync` calls `DisposeDefectTemplatePools(_tiles)` right after `_tileCoordinator.CancelAll()`, with no guarantee that `RenderFrameAsync`'s background `Task.Run` (which reads `annotation.DefectBitmap` inside `DrawDefectPatch`) has actually finished. `CancelAll()` cancels *coordinator* work, not the render pipeline's own `Task.Run`. This is `CoalescingAsyncAction` — a separate execution path.

## Scope

### Required Changes

1. **Implement `Bitmap.Dispose()` in `DisposeDefectTemplatePools`:**
   ```csharp
   foreach (var bitmap in pool.Values)
       bitmap.Dispose();
   pool.Clear();
   ```

2. **Add non-destructive `WaitForIdleAsync()` to `CoalescingAsyncAction`:**
   ```csharp
   public async Task WaitForIdleAsync()
   {
       Task? processingTask;
       lock (_gate) { processingTask = _processingTask; }
       if (processingTask is not null)
       {
           try { await processingTask.ConfigureAwait(false); }
           catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
       }
   }
   ```
   This reuses `DisposeAsync`'s capture-and-await pattern without setting `_disposed`.

3. **Call `await _renderAction.WaitForIdleAsync()` in `RegenerateSceneAsync`** before `DisposeDefectTemplatePools(_tiles)`.

4. **Add concurrency test:** Start a render, immediately trigger regenerate, assert no `ObjectDisposedException`/`ArgumentException` (the managed `Bitmap.Dispose()` will throw `ArgumentException`/`InvalidOperationException` on use-after-dispose).

### Dependency on ICW-P0-TRANSACTIONAL-REGEN

This ticket is a prerequisite for ICW-P0-TRANSACTIONAL-REGEN's rollback path. The rollback needs old defect template pools to not have been disposed prematurely. If `DisposeDefectTemplatePools` runs before rollback can snapshot the old pools, the rollback scene will be broken.

**Recommendation:** Move `DisposeDefectTemplatePools` into a post-rollback cleanup step, or snapshot the old pools before disposal so they can be re-attached on rollback. Coordinate with ICW-P0-TRANSACTIONAL-REGEN.

### Acceptance Criteria

- Defect template bitmaps are disposed on tile eviction and scene regeneration.
- `DisposeDefectTemplatePools` is fenced against concurrent in-flight render via `WaitForIdleAsync`.
- Concurrency test passes without `ObjectDisposedException`/`ArgumentException`.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | Implement `Bitmap.Dispose()` in `DisposeDefectTemplatePools` |
| `src/InfiniteCanvas.Core/CoalescingAsyncAction.cs` | Add `WaitForIdleAsync()` method |
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Call `await _renderAction.WaitForIdleAsync()` before `DisposeDefectTemplatePools` in `RegenerateSceneAsync` |
| `tests/InfiniteCanvas.Tests/CoalescingAsyncActionTests.cs` | Add `WaitForIdleAsync` test |
| `tests/InfiniteCanvas.Tests/SampleImageTileTests.cs` | Add dispose-concurrency test |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "DefectBitmap|DefectPool|WaitForIdle"
```

## Related Tasks

- ICW-P0-TRANSACTIONAL-REGEN: depends on this ticket (rollback needs undisposed pools)
- ICW-103: original defect-template work (assumed Dispose existed)
