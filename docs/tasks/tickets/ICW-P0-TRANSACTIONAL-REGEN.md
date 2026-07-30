---
id: ICW-P0-TRANSACTIONAL-REGEN
author: External Audit (Integration-1)
key: ICW-P0-TRANSACTIONAL-REGEN
title: Add transactional guard for RegenerateSceneAsync with fallback
status: Proposed
type: Bug
priority: P0
tags:
  - lifecycle
  - safety
  - regen
  - rollback
dependsOn:
  - ICW-102
related:
  - ICW-029
  - ICW-P0-SPATIAL-INDEX-SAFETY
links:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-07-30
---

# ICW-P0-TRANSACTIONAL-REGEN — Add transactional guard for `RegenerateSceneAsync` with fallback to previous scene

## Summary

**Critical gap:** `RegenerateSceneAsync` (`MainWindow.xaml.cs:163-244`) mutates `_spatialIndex`, `_camera`, `_tileCacheBudget`, `_tiles`, and `_annotations` — all before the only `try`/`finally` in the method, which merely re-enables UI and releases the semaphore. If `GenerateSet` throws (e.g., `ObjectsPerTile` out of range from a corrupted settings file), or the lifetime token fires mid-await, the method exits with partially-initialized state: `_tiles` unassigned relative to `_spatialIndex`/`_sceneBounds`, or a cleared spatial index with no annotations published.

**Confidence:** 85% (mechanism fully traced, no try/catch around the mutation body beyond the outer `finally`).

## Root Cause

`RegenerateSceneAsync` has this structure (simplified):

```
await _generationGate.WaitAsync();
try {
    CancelAll coordinator work
    InitializeSpatialState()  // creates new _spatialIndex, clears _sceneBounds
    GenerateSet(...)           // can throw
    PublishSnapshotAsync(...)  // can throw
    Assign _tiles, _annotations
    Update UI state
} finally {
    _generationGate.Release();
    Re-enable UI controls
}
```

If `GenerateSet` throws `ArgumentOutOfRangeException` (e.g., `ObjectsPerTile = 500`), the method exits with:
- A new (empty) `_spatialIndex` already swapped in.
- `_sceneBounds` set to default/empty.
- `_tiles` still pointing at the old tiles (from before `InitializeSpatialState`), OR partially assigned — depends on exact call order at line 163-244.
- `_annotations` unassigned.
- UI re-enabled but showing a broken scene.

If `_lifetime.Token` fires mid-await, `OperationCanceledException` propagates out of the `try`/`finally` and the same partially-initialized state is left behind.

## Scope

### Required Changes

1. **Snapshot previous scene state** before any mutation:
   ```csharp
   var previousTiles = _tiles;
   var previousAnnotations = _annotations;
   var previousSceneBounds = _sceneBounds;
   var previousSpatialIndex = _spatialIndex;  // snapshot reference, not deep copy
   var previousViewModel = _mainViewModel;
   ```
   Note: `_spatialIndex` is a `LiveSpatialIndexService` which uses immutable snapshots internally — snapshotting the reference is sufficient since the old tree remains valid for readers.

2. **Wrap generation + publish in try/catch** — on any exception other than `OperationCanceledException` during shutdown:
   - Restore `_tiles`, `_annotations`, `_sceneBounds`, `_spatialIndex`, `_mainViewModel` to their pre-regenerate values.
   - Set `StatusText.Text = "Regeneration failed: {exception.Message}"` instead of leaving the UI in a half-initialized state.
   - Log the full exception.

3. **On `OperationCanceledException` during active shutdown** (`_lifetime.IsCancellationRequested`):
   - Do not attempt rollback (disposed objects). Let the exception propagate to `OnClosed`.

4. **Integration test:** Inject a `GenerateSet` that throws on the second call (use a test seam or mock), call `RegenerateSceneAsync` twice, assert:
   - The second call leaves `_tiles`/`_annotations` at the *first* call's values (not empty, not null, not partially applied).
   - `StatusText` contains "failed" or equivalent error signal.

### Dependency on ICW-102

The rollback path needs the old defect template pool to not have been disposed prematurely. ICW-102 (defect bitmap pool disposal fencing) must be implemented first, because:
- `DisposeDefectTemplatePools(_tiles)` currently runs before the try/catch in `RegenerateSceneAsync`.
- If rollback tries to restore old tiles whose defect bitmaps were already disposed, the scene will be broken even after "successful" rollback.
- **Fix:** Move `DisposeDefectTemplatePools` into a post-rollback cleanup step, or snapshot the old pools before disposal so they can be re-attached on rollback.

### Acceptance Criteria

- If `RegenerateSceneAsync` throws or is canceled mid-flight, the previous scene (tiles, annotations, spatial index, scene bounds, view model) remains intact.
- A user-visible error message is displayed on failure.
- The app does not enter a partially-initialized state from which no subsequent `RegenerateSceneAsync` call can recover.
- Integration test covers the throw-and-rollback scenario.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.App/MainWindow.xaml.cs` | Add snapshot/rollback around `RegenerateSceneAsync`, move `DisposeDefectTemplatePools` to safe location |
| `tests/InfiniteCanvas.Tests/MainWindowTests.cs` (or equivalent) | Add integration test for regen failure rollback |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "RegenRollback|RegenerateFailure"
```

## Notes

This ticket is causally linked to ICW-102: defect bitmap pool disposal must be fenced before rollback can restore old tiles safely. The dependency is real — do not implement this before ICW-102 lands.

## Related Tasks

- ICW-102: defect bitmap pool disposal fencing (prerequisite)
- ICW-029: shutdown lifecycle race (related but separate — this covers mid-flight regen failure, not shutdown)
- ICW-P1-SETTINGS-VALIDATION: one concrete trigger for this bug (ObjectsPerTile = 500 crashes GenerateSet)
