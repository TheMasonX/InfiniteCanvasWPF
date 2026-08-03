---
id: ICW-WAVE-F-VIEWPORT-CANCELLATION
author: Copilot
key: ICW-WAVE-F-VIEWPORT-CANCELLATION
title: Wave F viewport cancellation safety slice
status: Done
type: Task
priority: P1
tags:
  - wave-f
  - viewport
  - cancellation
  - coordinator
  - rendering
dependsOn:
  - ICW-P1-CLAIMANT-TOKENS
related:
  - ICW-P0-ACTIVECOUNT-residuals
  - ICW-P1-COOPERATIVE-CANCEL
  - ICW-P1-GDI-CONCURRENCY
  - ICW-144
links:
  - src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - tests/InfiniteCanvas.Tests/TileWorkCoordinatorTests.cs
  - tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
created: 2026-08-03
updated: 2026-08-03
---

## Summary

Wave F prioritizes fast viewport navigation. Running tile work keeps ownership until the worker exits, and expensive generation phases observe the live claimant token.

## Scope

- Remove running-item reservation cleanup from the cancellation-request path.
- Document the bounded duplicate-admission window during cancel and re-request.
- Pass cancellation tokens from coordinator-backed tile factories into synthetic generation.
- Check cancellation before and after noise, detail, and pixel-transfer phases.
- Add focused coordinator and generator cancellation coverage.

## Acceptance Criteria

- Running cancellation releases coordinator ownership only from the worker termination path.
- Queued cancellation releases its ownership exactly once.
- Coordinator-backed native and mip generation observes claimant cancellation.
- Existing pixel output remains unchanged when the token is not canceled.
- Focused and full Release test suites pass.

## Validation

Commands:

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "Cancel|CooperativeCancel|Generator"`
- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Outcome: Passed. Core tests 95/95, Windows tests 10/10, and Release app build succeeded with 0 errors. Two new cooperative-cancellation regression tests pass.

## Notes

This slice does not implement cache lease accounting or mip-aware budget accounting. Those remain ICW-P0-LEASE-RELEASE and ICW-P1-PIXELCOST-MIPS.

## Related Tasks

## Related Tasks

- ICW-P0-ACTIVECOUNT-residuals
- ICW-P1-COOPERATIVE-CANCEL
- ICW-144
