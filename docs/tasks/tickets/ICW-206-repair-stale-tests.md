---
id: ICW-206
author: Copilot
key: ICW-206
title: Repair three stale tests that failed at baseline
status: Done
type: Bug
priority: P2
tags:
  - tests
  - annotations
  - scrollbars
  - stale-tests
dependsOn: []
related:
  - ICW-101
  - ICW-094
  - ICW-204
links:
  - src/InfiniteCanvas.Rendering/AnnotationFeaturePresenter.cs
  - tests/InfiniteCanvas.Tests/AnnotationFeaturePresenterTests.cs
  - tests/InfiniteCanvas.Tests/CanvasScrollbarWiringTests.cs
  - tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
created: 2026-08-04
updated: 2026-08-04
---

## Summary

Three core tests failed at baseline (before and independent of ICW-204). All three encoded pre-refactor behavior that no longer matched the shipped code.

## Failures

1. `BuildFeatureRows_UsesTypedFeatureValuesAndStableOrdering` — expected Confidence and Severity formatted as percents (`"80.0 %"`, `"25.0 %"`). The feature dictionary has no per-key schema, so the presenter cannot know which values are percents. Commit `9cbbe2c` changed `FormatFeatureValue` from `P1` to a plain double formatter but left the test assertions stale.

2. `AnnotationFeatureDisplayItems_ExposeReadableRows` — expected only `Confidence` and `Severity` rows with a `%` value. The generator now populates 12 features (`ID`, `Class`, `Area`, `Width`, `Height`, `AspectRatio`, `Left`, `Top`, `Right`, `Bottom`, `Confidence`, `Severity`), and the presenter returns all of them.

3. `MainWindow_PreservesScrollbarOverlayAndRenderUpdateHook` — asserted the scrollbar overlay, tracks, thumbs, and `UpdateViewportScrollbars(camera, width, height)` call lived in `MainWindow`. The canvas control extraction (`a308f1d`, `18b3c33`) moved the overlay and update logic to `CanvasControl`, and the render hook became `CanvasSurface.RefreshScrollbars()`.

## Decision

Percent encoding is out of scope. The feature dictionary cannot encode which values are percents, and other values (for example `AspectRatio`) can legitimately fall in the 0..1 range. Plain double formatting is the accepted behavior.

## Changes

- `AnnotationFeaturePresenter.FormatFeatureValue` formats doubles with `ToString(CultureInfo.InvariantCulture)` instead of `F1`. This keeps plain double display and avoids the `F1` lossy rounding (0.25 would display as "0.2").
- Updated the two annotation tests to assert plain double values and the full feature row set.
- Updated `CanvasScrollbarWiringTests` to guard the scrollbar invariant at its new home: overlay/tracks/thumbs/handlers in `CanvasControl.xaml`, metrics and `UpdateViewportScrollbars` in `CanvasControl.xaml.cs`, `RefreshScrollbars()` exposed and invoked by the window, and the native-scrollbar/padding layout guard still in `MainWindow.xaml`.

## Validation

Commands:
`dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`

Outcome: All three previously failing tests pass. Full core suite 149/150, with the only remaining failure being `PriorityQueue_MipSuitabilityBreaksDistanceTie`, which belongs to the in-flight ICW-205 priority queue work (uncommitted, separate workstream).

## Notes

- The scrollbar invariant in `docs/requirements/functional-requirements-and-invariants.md` (overlay, handlers, policy, and render update hook must remain present together) is preserved; the test now guards the post-extraction locations.
- `MainWindow.xaml.cs` still contains an orphaned `UpdateViewportScrollbars(CameraSnapshot, double, double)` from the extraction. Removing it is a follow-up for ICW-098, not this task.

## Next step

Keep complete. Follow up on ICW-098 (remove orphaned MainWindow scrollbar methods) and ICW-205 (priority queue) in their own workstreams.
