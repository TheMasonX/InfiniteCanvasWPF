---
id: ICW-136
key: ICW-136
title: Debounce background-noise and circle-count sliders to avoid full-regenerate on every tick
status: Proposed
type: Task
priority: P2
tags:
  - ui
  - performance
  - rendering
dependsOn: []
related: [ICW-039, ICW-012]
links:
  - docs/tasks/README.md
created: 2026-07-26
updated: 2026-07-26
---

# ICW-136 — Debounce heavy slider callbacks

Summary
- The background-noise and background-circle-count slider `ValueChanged` handlers currently call `RegenerateSceneAsync` on every tick while dragging, queuing many expensive full-scene regenerations.

Scope
- Files: `src/InfiniteCanvas.App/MainWindow.xaml.cs`
- Behavior: replace immediate `RegenerateSceneAsync` calls with a debounced pattern (reuse the existing 150ms `DispatcherTimer` debounce used for resize) so UI slider drags queue a single regeneration when the user pauses or releases.

Acceptance Criteria
- Dragging either slider does not queue repeated full regenerations; a single regeneration runs after the user stops changing the slider for ~150ms.
- No behavioral change when using keyboard/step changes (immediate apply acceptable for discrete changes if desired).
- Add a small test or manual validation instruction documenting the expected behavior.

Validation
- Manual: open the app, drag `BackgroundNoiseSlider` aggressively and confirm only one regeneration occurs after pause (use log messages or performance counter).
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter "MainWindow*"` (supplemental test may be added to cover debounce invocation).

Notes
- Reuse `_resizeTimer` pattern to minimize new code and keep consistent UX.
