---
id: ICW-085
status: To Do
title: Clean up nullable suppression operators and duplicate handlers in `MainWindow.xaml.cs`
type: Task
priority: P2
tags: [mainwindow, nullable, maintainability]
---

Summary
- SonarQube flagged multiple uses of the null-forgiving operator (`!`) and several duplicate or identical method bodies in `src/InfiniteCanvas.App/MainWindow.xaml.cs`.

Scope
- Remove unnecessary `!` usages by initializing fields or adding proper nullability checks. Consolidate duplicate event handlers (e.g., `OnShowBoxesChanged` duplicates) and consider extracting shared logic into helpers.

Validation
- Solution builds with nullable reference types warnings enabled and no `!` suppressions remain in `MainWindow.xaml.cs`. Duplicate handlers are merged or delegated to shared methods.

Next step
- Create a PR that initializes non-null fields earlier and replaces duplicate handlers with calls to a single helper method. Run solution build and unit tests.
