---
id: ICW-328-canvasframe-revision-wiring
author: InfiniteCanvas Agent
key: ICW-328
title: Wire CanvasFrame.Revision as a real stale-frame guard
status: Done
type: Bug
priority: P2
tags:
  - canvas
  - boundary
  - frame
  - library-extraction
  - stale-frame
dependsOn: []
related:
  - ICW-316A
  - ICW-315
  - ADR-0007
  - ICW-100
links:
  - src/InfiniteCanvas.Controls/CanvasFrame.cs
  - src/InfiniteCanvas.Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - tests/InfiniteCanvas.Windows.Tests/CanvasControlConsumerHostTests.cs
  - docs/audits/icw-wave-e-audit-delta-8.md
created: 2026-08-06
updated: 2026-08-06
---

# ICW-328 — Wire CanvasFrame.Revision as a real stale-frame guard

## Summary

Audit synthesis finding (Wave E delta-8, verified at HEAD c552830). `CanvasFrame.Revision` is a scaffolded no-op: it is documented as the stale-frame revision identity (ICW-316A), never assigned a real value at its only construction site (`MainWindow.PublishFrame`), and never consumed anywhere in `src/` (zero `.Revision` readers; `CanvasControl.PublishFrame` does not reference it). ICW-316A is marked Done and lists revision identity as delivered, but the protection does not exist.

## Scope

Option A implemented: wire it.

- Thread a monotonic revision into the `CanvasFrame` constructor: `MainWindow.PublishFrame` passes the `RenderRequestTracker` request version.
- Add the missing consumer half: `CanvasControl.PublishFrame` discards a frame whose revision is older than the last one displayed.
- Add a wiring regression test.

## Acceptance Criteria

- Every published frame carries a strictly increasing revision (the app serializes `BeginRequest`/`Advance` on the UI thread).
- The control rejects an out-of-order frame and newer state survives.
- A test proves both halves.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- New test: `ConsumerHost_StaleFrameRevision_IsDiscarded` (Windows test project) publishes revision 7 then 5 and asserts the older frame is discarded and newer state survives.
- Result: Windows 22/22, core 183/183, solution Release build 0 errors.

## Notes

- Delivered in Wave I (2026-08-06).
- The guard discards only strictly older revisions (`frame.Revision < _lastPublishedRevision`). Equal revisions are accepted as an idempotent republish; `_lastPublishedRevision` starts at `int.MinValue` so a host's first frame with the default revision of zero is accepted.
- Completes the ICW-316A "revision identity" acceptance claim.

## Related Tasks

- ICW-316A (harden reusable canvas contracts, Done)
- ICW-315 (render-pipeline frame boundary, Done)
- ICW-078 (stale-frame epoch guarding, Done)
