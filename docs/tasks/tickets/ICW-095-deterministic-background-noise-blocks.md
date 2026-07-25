---
id: ICW-095-deterministic-background-noise-blocks
key: ICW
title: Deterministic Background Noise Blocks
status: Done
type: Task
priority: P1
tags:
  - icw
  - rendering
  - determinism
dependsOn: []
related: []
links:
  - docs/requirements/functional-requirements-and-invariants.md
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
---

# ICW-095-deterministic-background-noise-blocks

## Summary

Generate true per-pixel deterministic background noise from reusable 512x512 blocks, stamp those blocks during Windows rasterization, and apply generation controls only when a new scene is regenerated.

## Scope

- Capture target value, noise, circle count, tile dimensions, and seed in the generated scene.
- Provide explicit seed control for deterministic tests and repeatable scenes.
- Replace sparse noise samples with per-pixel offsets while preserving lazy, non-blocking tile generation.
- Use block stamping in the viewport renderer where the source image is not required.

## Acceptance Criteria

- Identical generation inputs produce identical pixels, including the seed.
- Noise-enabled output gives every pixel a deterministic offset before defect circles are applied.
- Changing generation controls does not alter the displayed scene until regeneration.
- Windows rasterization uses reusable noise blocks and `Graphics.DrawImage` without changing sparse defect layering.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Command: `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Result: Core focused tests 21/21 passed; Windows rendering tests 5/5 passed; Release app build succeeded with one pre-existing CS8602 warning in `ZeroCopyBitmapFactory.Windows.cs`; `git diff --check` passed. Task validation remains blocked by nine older tickets missing `key`.

## Findings and Blockers

- Resolved the bounded-sample noise path with full per-pixel deterministic offsets.
- Resolved immediate slider regeneration and added a persisted explicit seed control.

## Next Step

Keep complete; resolve the unrelated legacy task-ticket metadata gaps before requiring a clean tracker validation.