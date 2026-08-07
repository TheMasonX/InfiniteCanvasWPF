---
id: ICW-335-ci-submodule-checkout
key: ICW-335
title: Restore FastNoise2 Submodules During CI Checkout
status: Done
type: Bug
priority: P1
tags:
  - ci
  - submodule
  - fastnoise
dependsOn: []
related:
  - ICW-148
links: []
created: 2026-08-07
updated: 2026-08-07
---

## Summary

GitHub Actions does not compile the solution because the checkout step omits the `FastNoise2Bindings` submodule.

## Scope

- Configure the checkout action to initialize recursive submodules.
- Keep the existing repository submodule boundary and project source links.
- Verify the solution build, both test projects, benchmark build, task validation, and whitespace checks.

## Acceptance Criteria

- CI checkout initializes `submodules/FastNoise2Bindings` before restore and build.
- The solution build no longer reports missing `FastNoise2.cs` or `FastNoiseNodeEditorIpc.cs`.
- The existing local CI command sequence passes.

## Validation

- Command: `dotnet restore InfiniteCanvasWPF.slnx`
- Command: `dotnet build InfiniteCanvasWPF.slnx --configuration Release --no-restore`
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --no-build --no-restore`
- Command: `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release --no-build --no-restore`
- Command: `dotnet build benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj --configuration Release --no-restore`
- Command: `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`
- Command: `git diff --check`

## Findings

Run 31150308684 failed in `Build solution` with CS2001 for two FastNoise2 binding files in both Rendering target frameworks. The checkout step now uses `submodules: recursive`. The complete local CI command sequence passes.

## Next Step

Push the workflow change and verify the next GitHub Actions run.