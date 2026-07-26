---
id: ICW-098
status: Proposed
summary: Finish or remove partially landed viewport scrollbar slice
assignee: TBD
priority: High
labels:
  - ui
  - stability
  - mainwindow
validation: pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks && dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter CanvasScrollbarWiringTests
---

## Problem
`MainWindow.xaml.cs` contains scrollbar update and interaction methods, but `MainWindow.xaml` lacks the named controls. The render/update path does not invoke the scrollbar update, leaving a half-wired slice that can break the build or be inert.

## Evidence
- `src/InfiniteCanvas.App/MainWindow.xaml.cs` defines `UpdateViewportScrollbars`, `UpdateScrollbar`, multiple mouse handlers for scrollbar thumb/track, and references to `_horizontalScrollbarTrack`/`_horizontalScrollbarThumb`/`_verticalScrollbarTrack`/`_verticalScrollbarThumb`.
- `src/InfiniteCanvas.App/MainWindow.xaml` contains no matching `x:Name` for those controls; render update path does not call `UpdateViewportScrollbars`.

## Recommendation
Choose one:
- Finish: add XAML controls with `x:Name` fields, wire them into `OnLoaded` via `FindName` or generated fields, and call `UpdateViewportScrollbars` from the render update path. Add unit tests for wiring and interaction handlers.
- Remove: delete the unused methods and keep `ICW-070` as a design/feature ticket until fully implemented in a single atomic PR.

## Estimate
- Finish: 2-3d (XAML additions, wiring, tests, review)
- Remove: 2-4h (delete methods, run tests, update docs)

## Risks
- Finishing incorrectly may introduce per-frame allocations or null reference exceptions if controls are not present during initial render.
- Removing may discard intended UX work; confirm product intent before deletion.
