---
id: AGT-006
author: InfiniteCanvas Agent
key: AGT-006
title: Create a profiling evidence skill for runtime diagnostics and user captures
status: Done
type: Docs
priority: P2
tags:
  - agent
  - customization
  - profiling
  - diagnostics
dependsOn: []
related:
  - ICW-004
  - ICW-132
  - ICW-133
  - ICW-144
  - ICW-007
links:
  - .github/skills/profiling-evidence/SKILL.md
  - .github/agents/infinitecanvas.agent.md
  - src/InfiniteCanvas.App/Logging/SerilogHost.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/benchmarks/BENCHMARKS.md
created: 2026-08-08
updated: 2026-08-08
---

# AGT-006 - Create a profiling evidence skill for runtime diagnostics and user captures

## Summary

Create a workspace skill that teaches agents to inspect local Serilog logs before requesting user artifacts.
Document structured profiling instrumentation, controlled A/B capture, Visual Studio Profiler requests, and export formats.

## Scope

- Add `.github/skills/profiling-evidence/SKILL.md`.
- Reference the skill from `.github/agents/infinitecanvas.agent.md`.
- Record the workflow in both task trackers.
- Keep the guidance aligned with the current `FrameDiag`, `AnnotationDiag`, `RenderingDiagnostics`, BenchmarkDotNet, and Serilog paths.

## Acceptance Criteria

- The skill includes a log-first investigation procedure.
- The skill explains how to add bounded, named, secret-safe profiling diagnostics.
- The skill provides an exact Visual Studio Profiler capture request.
- The skill explains JSON Lines, CSV, BenchmarkDotNet, `.diagsession`, and ETW export choices.
- The agent file references the skill for debugging and profiling work.
- Tracker validation and whitespace validation pass.

## Validation

- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Command: `git diff --check`
- Result: Tracker validation passed with 232 task files validated and 5 legacy markdown files skipped. `git diff --check` passed. Changed-file diagnostics reported no errors.

## Notes

- Existing application logs use daily text files under `%LOCALAPPDATA%\\InfiniteCanvas\\logs`.
- Repeated export is better served by a parallel Serilog JSON Lines sink than by parsing padded human-readable text.
- A `.diagsession` preserves Visual Studio call-tree context. An exported top-method table helps review without transferring the full artifact.

## Related Tasks

- ICW-004
- ICW-007
- ICW-132
- ICW-133
- ICW-144
