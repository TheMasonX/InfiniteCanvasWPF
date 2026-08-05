---
id: ICW-323-epoch-wiring-regression-test
author: InfiniteCanvas Agent
key: ICW-323
title: Add an epoch-wiring behavioral regression test
status: Done
type: Task
priority: P3
tags:
  - tests
  - regression
  - epoch-guard
related:
  - ICW-078
  - ICW-100
links:
  - src/InfiniteCanvas.Core/RenderRequestTracker.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-323 — Add an epoch-wiring behavioral regression test

## Summary

Audit synthesis finding F-013. `RenderRequestTrackerTests` test the primitive, not the wiring. Nothing fails if `MainWindow` stops calling `BeginRequest`/`IsCurrent`/`Advance` in `RenderFrameAsync`. The 2026-07-26 epoch-guard revert slipped exactly this way.

## Scope

- Add a wiring assertion in the style of `FrameShellWiringTests`: reflection over `RenderFrameAsync` or a source-text guard asserting the three calls remain wired.

## Acceptance Criteria

- The test fails when the three calls are removed or rewired incorrectly.
- The test fails on the 2026-07-26 revert shape.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "RenderRequestTracker|EpochWiring"`

## Notes

- Test-only, no production risk.
- Can batch with ICW-316A or the Wave-F work. Cheapest permanent guard against the epoch-wiring regression class.

## Related Tasks

- ICW-078 (stale-frame epoch guarding)
- ICW-100 (re-apply RenderRequestTracker wiring)
