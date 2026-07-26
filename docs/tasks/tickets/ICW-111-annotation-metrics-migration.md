---
id: ICW-111
title: Migrate annotation feature dictionary to typed AnnotationMetrics
status: To Do
type: Task
priority: P2
tags:
  - ui
  - refactor
dependsOn:
  - ICW-080
related:
  - ICW-101
created: 2026-07-26
updated: 2026-07-26
owner: unassigned
---

# ICW-111 - Migrate annotation feature dictionary to typed AnnotationMetrics

## Summary

Call sites read annotation feature values using string keys (e.g., `annotation.Features["Confidence"]`), which is brittle and error-prone. Introduce a typed `AnnotationMetrics` value object exposing known metrics (`Confidence`, `Severity`, etc.) and migrate tooltip and DataGrid usage to the typed API with a compatibility shim for older persisted or third-party annotations.

## Scope

- Add `AnnotationMetrics` type in `src/InfiniteCanvas.Rendering`.
- Replace direct string-key access in `MainWindow`, `AnnotationFeaturePresenter`, and tests.
- Add `TryGetFeature` compatibility adapter for unknown keys.
- Add unit tests for migration and for round-trip compatibility with existing persisted annotation blobs.

## Acceptance Criteria

- All production call sites use `AnnotationMetrics` fields or `TryGetFeature` instead of literal string keys.
- Tooltip builder uses `AnnotationFeaturePresenter` and typed metrics.
- Tests `AnnotationMetricsMigrationTests` pass in CI.

## Validation

- Command: `dotnet test --filter AnnotationMetricsMigrationTests`

## Notes

- Crosslink: ICW-080 (annotation feature presentation model) provides a natural owner for the presenter migration.
