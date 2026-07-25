# ICW-065: Viewport scrollbars and zoom-driven viewport navigation

## Status
In Progress

## Summary
Add viewport scrollbars that reflect the current camera pan/zoom state and make the existing zoom control drive viewport navigation in a way that stays visually synchronized with the canvas.

## Scope
- Add scrollbars to the viewport host so panning and viewport size changes are reflected in the visible scroll range.
- Keep the current zoom presets and wheel zoom behavior aligned with the scrollable viewport so the viewport contents remain anchored correctly when zoom changes.
- Add targeted regression tests for the viewport/zoom state math that determines the scroll extent.

## Validation
- dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Debug
- dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release

## Findings
- The app already supports zoom presets, custom zoom, wheel zoom, and camera pan, but it does not expose viewport scrollbars. The camera transform and viewport fit logic already exist, so the missing piece is a thin scroll-state controller plus UI wiring.

## Next Step
Implement a shared viewport-scroll policy and hook it into the main window viewport host.
