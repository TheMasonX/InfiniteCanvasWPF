# MainWindow view decomposition audit

Date: 2026-07-25
Scope: MainWindow XAML/code-behind review focused on view extraction, style reuse, and the current decomposition backlog.

## Executive Summary

The current WPF shell still works, but the view layer is now acting as the app shell, viewport controller, render presenter, settings editor, and inspection surface. The result is one large window with inline XAML and a large code-behind that is harder to maintain, harder to test, and harder to evolve. The best next step is to extend the existing decomposition work in ICW-022 around view extraction and style reuse rather than add a separate backlog item for the same area.

## New Findings

### 1. MainWindow remains a single monolithic composition
- Evidence: [src/InfiniteCanvas.App/MainWindow.xaml](src/InfiniteCanvas.App/MainWindow.xaml#L31-L251) defines the header, viewport host, splitter, settings panel, and footer in one window tree. The viewport host, overlay chrome, and feature-grid panel are embedded directly in the same visual tree rather than as separately owned subviews.
- Risk: every layout change forces the whole window to be edited, and visual regressions are harder to isolate.
- Recommendation: extract the viewport pane, display-options sidebar, feature-inspector panel, and status/footer region into dedicated user controls or custom controls. Keep MainWindow as the shell that hosts those subviews and binds to a view-model.

### 2. Code-behind mixes UI composition with interaction, render, settings, and lifecycle concerns
- Evidence: [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L394-L482) builds frame visuals and tile-grid layers inline; [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L992-L1099) contains zoom preset handling; [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L1284-L1330) reads generation options; and [src/InfiniteCanvas.App/MainWindow.xaml.cs](src/InfiniteCanvas.App/MainWindow.xaml.cs#L1392-L1430) owns pixelometer sampling and display.
- Risk: the window class now carries view orchestration, viewport math, settings parsing, and shutdown logic together, which makes the UI harder to reason about and harder to test.
- Recommendation: introduce a small controller or presenter layer for viewport interaction, render-state updates, and settings editing. Keep the window code-behind thin and delegate to those collaborators.

### 3. The XAML is under-styled and repeats visual patterns inline
- Evidence: [src/InfiniteCanvas.App/MainWindow.xaml](src/InfiniteCanvas.App/MainWindow.xaml#L1-L17) defines only generic button/textbox/checkbox styles, while the main window still repeats panel headers, spacing, labels, button groups, and section separators inline throughout the large window tree.
- Risk: future polish work will continue to add more local styling and make the window harder to evolve.
- Recommendation: define reusable styles and control templates for panel headers, slider label blocks, button groups, and section dividers; consider using DataTemplates for the options sections and a small collection of view-model-driven subcontrols.

## Corrections/Extensions to Existing Tasks

- Extend ICW-022 to explicitly include: subcontrol extraction for the viewport shell, display-options panel, feature inspector, and footer/status region; resource/style consolidation for repeated panel UI; and the current unit-test goal for zoom/pixelometer/generation helpers.
- Keep ICW-080 as the narrow presentation-model home for feature-grid and tooltip formatting; no new backlog item is needed for that scope.
- Keep ICW-037 as the accessibility follow-up for the extracted views rather than treating keyboard and automation work as a separate concern embedded in the monolithic window.

## Priority Order

1. P2 – Extend ICW-022 first to split the shell into subviews and make the MainWindow code-behind thinner.
2. P2 – Consolidate repeated visual patterns into styles/templates after the view boundaries are extracted.
3. P3 – Add a small view-model/controller layer for viewport interaction and settings editing.

## Open Questions and Validation Gaps

- Which sections should be first-class user controls versus simple DataTemplates?
- Should the viewport interaction controller and the render-state presenter share one view-model or remain separate?
- Which extracted pieces need their own tests first: zoom/pixelometer logic, settings view state, or feature-inspector formatting?
