---
id: ICW-129
key: ICW-129
title: Migrate background noise generation to FastNoise2
status: In Progress
type: Improvement
priority: P1
tags:
  - rendering
  - noise
  - fastnoise2
  - sampling
related: []
created: 2026-07-26
updated: 2026-07-26
---

# Migrate background noise generation to FastNoise2

Replace the custom fractal-Brownian-motion background generator with FastNoise2 while preserving deterministic tile generation and seamless worldspace sampling.

## Status Divergence Note (2026-08-04)

Audit synthesis finding F-022: active-tasks.md marks this ticket Done, the ticket file says In Progress, and task-tracker.md has no row. The status binds to the un-met "seamless worldspace sampling" acceptance criterion. Do not close or set status until the requirement decision in ICW-324 resolves seamless vs per-tile variance. ICW-324 also owns the task tracker row.

## Execution Plan
- ICW-001: Capture the migration requirement and register the work in the task tracker.
- ICW-002: Add the FastNoise2 C# binding source and native DLL to the rendering/runtime project graph with output-copy settings.
- ICW-003: Add a regression test proving that tile generation uses worldspace offsets and remains deterministic for the same global seed.
- ICW-004: Replace the custom FBM path in SampleImageGenerator with FastNoise2 grid generation using tile worldspace origin offsets.
- ICW-005: Run focused tests and a Release build, then record the evidence in the tracker.

## Scope
- Integrate the FastNoise2 bindings from the submodule into the rendering/runtime build.
- Ensure the native FastNoise.dll is copied to runtime output directories.
- Generate per-tile noise from worldspace X/Y coordinates instead of local tile pixel coordinates.
- Preserve deterministic tile generation across tiles and across repeated runs for a fixed seed.

## Validation
- Run: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter SampleImageGeneratorTests`
- Run: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: 24/24 SampleImageGeneratorTests passed; Release app build succeeded with 12 existing FastNoise2 binding warnings.

