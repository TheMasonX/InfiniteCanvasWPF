# 2026-07-24 Handoff: Uniform Zoom Recovery and Custom Entry

## Status

- Branch: `main`
- Completed: ICW-045 and ICW-046.
- Newly captured follow-up work: ICW-047 and ICW-048.

## Handoff Review

Reviewed `2026-07-24-canvas-layers-settings-zoom-handoff.md`. Its claimed next item, ICW-045, was valid and is now complete. The prior zoom policy correctly handled continued zoom-out with independent floors but did not restore uniform scale while zooming in from a prior clamp. That correction is captured and complete as ICW-046.

## Implemented

### Uniform zoom recovery

- `ViewportZoomPolicy.ComputeWheelDeltas` retains a clamped axis at its fit floor during zoom-in while the free axis cannot yet produce a legal uniform target.
- Once that free-axis target is at or above both floor values, both axes converge to the shared target.
- Existing independent zoom-out clamp behavior remains unchanged.

### Custom zoom entry

- The zoom preset selector now includes `Custom...`.
- Selecting it reveals an inline percentage textbox and Apply button below the selector.
- Enter and Apply use one validation/update path; invalid values retain focus and report a concise status message.

## Validation

```powershell
dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release --filter "FullyQualifiedName~ViewportZoomPolicyTests"
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
```

- Focused policy suite: 5 passed, 0 failed.
- Release app build: succeeded.

## Remaining User Requirements

### ICW-047: Taller sparse background tiles and cache diagnostics

- Double default background-tile height.
- Generate noisy image tiles lazily as the viewport approaches them.
- Use pixel cost rather than item count for cache capacity.
- Add `Show Image Tiles` and visible per-cache debugging status.

This needs a cache-ownership design before renderer changes: preserve non-blocking placeholders, track cache byte/pixel budgets independently for backgrounds and sparse generated image tiles, and avoid changing unmanaged bitmap lifetime semantics.

### ICW-048: Annotation feature sidebar

- Bind the selected annotation's existing `Features` map to a side-panel DataGrid.
- Clear the grid on regeneration or selection clear.

## Next Step

Start ICW-047 with a pure pixel-budget cache contract and tests, then wire status and display controls into the existing panel. ICW-048 can proceed independently once the selection-to-detail view-model shape is chosen.