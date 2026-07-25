---
id: ICW-065
key: ICW-065
title: Viewport scrollbars and zoom navigation
status: Done
type: Story
priority: P1
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

## Summary

Provide custom, camera-native horizontal and vertical scrollbars for scene navigation. They must behave like standard scrollbars while remaining independent from WPF layout measurement, so zoom and raster dimensions continue to use the fixed visible viewport.

## Scope

- Add a pure camera-to-scrollbar metrics policy in `InfiniteCanvas.Core`.
- Render custom overlay tracks and thumbs without a `ScrollViewer` or layout-affecting extent.
- Support thumb dragging and track clicks to pan the camera; refresh thumb geometry after pan, zoom, resize, and scene regeneration.
- Make mouse-wheel and preset/custom zoom changes immediately update the navigation controls.

## Acceptance Criteria

- Horizontal and vertical thumbs express the visible world fraction and camera location within scene bounds.
- Dragging a thumb and clicking its track pans through the same bounded camera path as direct panning.
- Zooming changes thumb size and position without changing `ViewportHost` measurement, zoom-floor calculations, or raster dimensions.
- The controls hide their thumb interaction when an axis is not scrollable.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`; `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: `ViewportScrollbarPolicyTests` passed 3/3 previously, and the restored Release WPF app build succeeds. The current build has one pre-existing nullable warning in `ZeroCopyBitmapFactory.Windows.cs`; the scrollbar overlay and handlers compile successfully.

## Notes

- Native `ScrollViewer` integration was removed because its content extent replaced the fixed camera viewport and produced unreachable zoom-out plus oversized smeared frames. This implementation owns only an overlay visual and maps input directly to `CameraTransform`.
- Delivered camera-native tracks and thumbs with click-to-position and drag navigation. The geometry is derived from `CameraSnapshot`, scene bounds, and fixed viewport dimensions after every rendered frame, including wheel and preset/custom zoom changes.

## Related Tasks

- ICW-070
