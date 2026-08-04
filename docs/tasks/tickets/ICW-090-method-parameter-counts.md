---
id: ICW-090
key: ICW-090
status: To Do
title: Audit and reduce methods with too many parameters
type: Task
priority: P3
tags: [refactor, maintainability, brain-overload]
---

Summary
- Several methods across the codebase exceed the preferred parameter count (7), including methods in `MainWindow.xaml.cs`, `TileGridIndexLookup`, and `SampleImageGenerator`.

Scope
- Identify high-parameter methods, group related parameters into small option/config objects, and introduce overloads or builder helpers to simplify call sites.

Validation
- Reduce method parameter counts to 7 or fewer for selected hotspots and update call sites accordingly; run `dotnet build` and tests.

Next step
- Create PRs for highest-impact methods first (e.g., `SampleImageGenerator` and `BuildFrameVisual`), leaving low-risk converters for follow-ups.
