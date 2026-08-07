---
id: ICW-018-rendering-abstraction-and-overload-ownership
author: External Audit (Integration-1)
key: ICW-018
title: Resolve dormant rendering abstractions and point-render overload ownership
status: Done
type: Task
priority: P2
tags:
  - rendering
  - cleanup
  - dead-code
  - interfaces
dependsOn: []
related:
  - ICW-076
links:
  - src/InfiniteCanvas.Rendering/IRenderer.cs
  - src/InfiniteCanvas.Rendering/ViewportRenderRequest.cs
  - src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-25
updated: 2026-08-07
---

# ICW-018 — Resolve dormant rendering abstractions and point-render overload ownership

## Summary

**External audit (99% confidence) identified dormant abstractions:** Several rendering interfaces and types have zero references anywhere in production code or tests. These represent either scaffolding for future work or pure dead code.

**Precise inventory (correcting the ticket's original count):**

### Fully dead (zero references, zero test coverage, safe to delete):
- `IRenderer<TScene,TOutput>` — zero references anywhere.
- `ViewportRenderRequest` — zero references anywhere.
- `MipOptions` — zero references anywhere (new finding, no existing ticket names this).

### Intentional scaffolding (referenced by ADRs or tests):
- `IBackgroundTileSource` — referenced by ADR-0005 and ADR-0007 as the future source-neutral tile boundary. Keep it until ICW-076 connects it to materialization.
- `BackgroundTileDescriptor`, `BackgroundTileRequest`, `BackgroundTilePayload` — referenced **only by `SampleImageGeneratorTests.cs`** (3 call sites). Not dead, but not wired into any production render path either. These represent a richer, source-agnostic contract that `SampleImageTile`/`ZeroCopyBitmapFactory` do not currently use (they use the leaner `BackgroundTileCacheKey` struct instead).

### Private dead code:
- `SampleImageGenerator.GenerateAnnotations` (lines 574-622) — private, unreachable duplicate of `AnnotationGenerator.GenerateAnnotations`. The real call site at line 190 calls the public `AnnotationGenerator.GenerateAnnotations` instead, leaving ~50 lines of dead, byte-for-byte-duplicated logic.

## Scope

### Recommended disposition (from external audit):

1. **Delete outright** (genuinely zero-consumer and zero test coverage):
   - `IRenderer<TScene,TOutput>`
   - `ViewportRenderRequest`
   - `MipOptions`

2. **Keep with doc comments** referencing ICW-076's future direction:
  - `IBackgroundTileSource`
   - `BackgroundTileDescriptor`
   - `BackgroundTileRequest`
   - `BackgroundTilePayload`
   - Add a doc comment tying them to ICW-076's source-agnostic mip work, since tests already depend on them and ADR-0005 (source-agnostic mip strategy) is "In Progress."

3. **Confirm the dead private `SampleImageGenerator.GenerateAnnotations` is absent:**
   - Confirm no reflection-based test depends on it (quick grep for `"GenerateAnnotations"` in test files — likely none beyond line 190).
   - This also removes a second live copy of the `Confidence`/`Severity` feature-dictionary construction that would otherwise need to be kept in sync with `AnnotationGenerator.cs` if ICW-031's typed-metrics migration proceeds.

### Acceptance Criteria

- Deleting `IRenderer`, `ViewportRenderRequest`, and `MipOptions` does not break any build or test.
- `IBackgroundTileSource` remains available for the ADR-0005 materializer boundary.
- Remaining types (`BackgroundTileDescriptor`/`Request`/`Payload`) have doc comments linking to ICW-076.
- Private dead `GenerateAnnotations` is absent from `SampleImageGenerator.cs`.
- Build and all tests pass after cleanup.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/IRenderer.cs` | Delete file |
| `src/InfiniteCanvas.Rendering/ViewportRenderRequest.cs` | Delete file |
| `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs` | Remove `IBackgroundTileSource`; add doc comments to remaining types |
| `src/InfiniteCanvas.Rendering/MipOptions.cs` | Delete file |
| `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs` | Remove dead `GenerateAnnotations` (lines 574-622) |

## Wave AD Update, 2026-08-07

The review confirmed that `IBackgroundTileSource` is an intentional ADR boundary, not dead code.
The wave deletes only three unreferenced types and adds source-neutral contract documentation.
The duplicate private generator is already absent at HEAD.

## Validation

```
dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
dotnet test tests/InfiniteCanvas.Tests --configuration Release
```
