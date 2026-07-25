---
status: proposed
title: Harden tile pixel buffer contract (pin or copy) for background rendering
repo-area: src/InfiniteCanvas.Spatial & src/InfiniteCanvas.Rendering
severity: high
assignee: spatial-team, rendering-team
---

Summary:
`TryGetPixelsNonBlocking` can return a pointer/array that may be reclaimed or mutated while background rendering reads it. The contract must guarantee pinning/refcounts or the renderer must copy required data before use.

Scope:
- `src/InfiniteCanvas.Spatial/*` (tile buffer providers)
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs` (render path)

Acceptance criteria:
- Either the spatial tile API is updated to return a pinned, ref-counted buffer (with release API), or the renderer copies tile pixel ranges before reading.
- Unit tests simulate tile eviction while render is in-flight and verify no memory corruption or AccessViolationException.

Validation commands:
- `dotnet test ./tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Debug --filter FullyQualifiedName~TileBuffer`
- `dotnet run -c Release -p benchmarks/InfiniteCanvas.Benchmarks/InfiniteCanvas.Benchmarks.csproj -- --filter ProjectAndRender`

Estimated effort: Medium
Risk: Medium
Suggested owner: @spatial-index-owner & @rendering-engine-owner
