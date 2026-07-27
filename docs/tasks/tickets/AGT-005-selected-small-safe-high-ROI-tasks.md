---
id: AGT-005
author: Copilot
key: AGT-005
title: Agent review — five small, safe, high-ROI ICW tasks
status: Proposed
type: Task
priority: P2
tags:
  - agent
  - review
  - small-changes
created: 2026-07-26
updated: 2026-07-26
---

# AGT-005 — Five small, safe, high-ROI ICW tasks

Summary
- Selected five small, low-risk tasks from the ICW backlog that are quick to implement, verifiable, and reduce immediate risk or developer friction.

Selected tasks

- **ICW-014 — Add global unhandled-exception safety net**
  - Short: register `DispatcherUnhandledException`, `AppDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException` in `App.xaml.cs` and log via Serilog; decide and document `args.Handled` policy.
  - Why: tiny change, high ROI — prevents app process crashes from unobserved `async void` handlers.

- **ICW-015 — Fix `SampleImageGenerator.GenerateSet` parameter validation attribution**
  - Short: throw correctly attributed `ArgumentOutOfRangeException` per parameter (replace single OR'd check with per-parameter guards).
  - Why: trivial, improves debugging and reduces mistaken troubleshooting time.

- **ICW-020 / ICW-055 — Pixelometer: O(1) tile lookup**
  - Short: compute tile column/row from world coords and index into flat tile array instead of linear scanning on every mouse-move.
  - Why: small perf improvement with clear validation (mouse-hover responsiveness) and negligible risk.

- **ICW-XXX — Debounce background-noise and circle-count sliders** (new small task)
  - Short: reuse the existing resize debounce pattern (150ms DispatcherTimer) to avoid queuing `RegenerateSceneAsync` on every slider tick.
  - Why: avoids wasted full-scene regenerations during slider drags; small, reversible UI change with clear evidence.

- **ICW-103 / Test — Add both-axes-clamped unit test for `ViewportZoomPolicy`**
  - Short: add a dedicated unit asserting the both-axes-clamped branch behavior (prevents XOR regression guarded by comment in source).
  - Why: very small test, prevents subtle future regressions and preserves a critical invariant.

Acceptance / validation
- Each item should be a single small PR with a focused unit test or smoke validation command in its ticket body.
- Suggested validation commands are cited inline in the original tickets and are short (unit tests or manual quick-run smoke checks).

Notes
- I selected items that are low-risk, quick to review, and provide outsized reliability or UX benefit. If you want these implemented, tell me which one(s) to pick first and I will open focused PRs.
