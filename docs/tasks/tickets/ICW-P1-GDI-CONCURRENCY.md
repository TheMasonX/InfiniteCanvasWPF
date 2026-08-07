---
id: ICW-P1-GDI-CONCURRENCY
author: External Audit (Integration-1)
key: ICW-P1-GDI-CONCURRENCY
title: Add explicit GDI+ concurrency management for tile generation factories
status: In Review
type: Bug
priority: P1
tags:
  - gdi
  - concurrency
  - threading
  - rendering
  - safety
dependsOn:
  - ICW-P0-ACTIVECOUNT
  - ICW-P1-COOPERATIVE-CANCEL
related:
  - ICW-P0-ACTIVECOUNT-residuals
links:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-30
updated: 2026-08-06
---

# ICW-P1-GDI-CONCURRENCY — Add explicit GDI+ concurrency management for tile generation factories

## Summary

**Critical gap:** `ApplyDetailsWithGdiPlus` (in `SampleImageGenerator.cs:333-396`) constructs `System.Drawing.Bitmap` and `Graphics.FromImage` per call, invoked concurrently from up to `DefaultMaxConcurrency = 4` `Task.Run` workers. .NET's GDI+ wrapper has known historical thread-affinity issues — concurrent `Graphics` creation from multiple threads can cause native heap corruption, access violations, or silent data corruption.

Additionally, the ICW-P0-ACTIVECOUNT residual B (duplicate concurrent admission for the same cache key during cancel races) creates a second, independent source of concurrent `ApplyDetailsWithGdiPlus` calls beyond the normal max-concurrency ceiling.

**Confidence:** 65% (mechanism is real and documented in .NET ecosystem; actual crash/corruption not reproduced in this review since it requires a live repro).

Wave K review confirmed that cooperative cancellation now reaches running tile factories, but the Windows GDI+ path remains unsynchronized. Core tests do not compile this path because they target `net10.0`, so this wave adds Windows-only stress coverage.

## Root Cause

`ApplyDetailsWithGdiPlus` does:
```csharp
using var bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
using var g = Graphics.FromImage(bitmap);
// ... rasterize circles into bitmap ...
// ... copy back to byte[] ...
```

This runs inside `TileWorkCoordinator`'s concurrent `Task.Run` factories. There is:
- No serialization of GDI+ calls (each worker creates its own `Bitmap`/`Graphics`).
- No ceiling on concurrent GDI+ operations beyond `_maxConcurrency` (which is itself not a real ceiling during cancel bursts — see ICW-P0-ACTIVECOUNT).
- Duplicate workers for the same tile key during cancel races (ICW-P0-ACTIVECOUNT residual B) can cause `ApplyDetailsWithGdiPlus` to run more than `_maxConcurrency` times concurrently.

## Scope

### Fix Options

**Option A — Serialization (recommended for first implementation):**
- Serialize `ApplyDetailsWithGdiPlus` calls behind a dedicated `SemaphoreSlim(1,1)` in `SampleImageGenerator`.
- Accept the throughput hit — circle rasterization is a small fraction of total generation time per profiling notes (ICW-097/ICW-131).
- Simple, safe, immediately testable.

**Option B — Dedicated GDI+ worker thread (more complex, higher throughput):**
- Create a dedicated thread with a work queue for GDI+ operations.
- The generation factory packages its parameters into a work item, posts it to the queue, and awaits completion.
- Avoids any GDI+ concurrency by running all GDI+ on one thread.
- Significantly more code, higher risk, but preserves throughput.

**Recommendation:** Start with Option A. The throughput impact is bounded and the safety benefit is immediate. Option B can be pursued if profiling shows circle rasterization is a meaningful bottleneck.

### Concurrent GDI+ stress test

Add a stress test that runs `maxConcurrency` concurrent `ApplyDetailsWithGdiPlus` calls in a tight loop under a debug build with GDI+ debug assertions enabled. Run this many times in CI. This is the only way to get real evidence of whether the current unbounded concurrency has ever caused a real GDI+ fault.

### Interaction with ICW-P0-ACTIVECOUNT residual B

The duplicate-admission race (§2.1 Residual B in Audit 2) is a second trigger for concurrent GDI+ calls. Document this interaction in a code comment at the race site: "Duplicate admission can cause >maxConcurrency concurrent GDI+ operations — mitigated by ICW-P1-GDI-CONCURRENCY serialization."

### Acceptance Criteria

- Peak concurrent `ApplyDetailsWithGdiPlus` calls never exceeds 1 (if Option A) or `_maxConcurrency` (if Option B).
- Stress test runs without `AccessViolationException`, `SEHException`, or silent corruption for 1000+ iterations with concurrent cancellation.
- No regression in generation throughput for the common case (single visible tile).

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs` | Add `SemaphoreSlim(1,1)` for serializing GDI+ calls, or implement dedicated worker thread |
| `tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs` | Add GDI+ concurrency stress test |
| `src/InfiniteCanvas.Rendering/TileWorkCoordinator.cs` | Add code comment at duplicate-admission race site (residual B) |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "GdiConcurrency|GdiStress"
dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release --filter "GdiConcurrency|GdiStress"
```

## Notes

- The 65% confidence reflects that GDI+ thread-safety failures are timing-dependent and not reproducible via static review. The risk is real and well-documented in .NET ecosystem; the decision is whether to mitigate preemptively or wait for a user-reported crash.
- ICW-P1-COOPERATIVE-CANCEL should land first — shorter GDI+ call duration reduces the window for overlap.

## Current Validation

- Wave K commit `2ea0b74` is pushed and local `main` matches `origin/main`.
- The working tree contains only unrelated untracked workflow and ticket files.
- `SampleImageGeneratorConcurrencyTests` completed 1,000 concurrent GDI+ generations without native failure.
- Core tests pass 189/189.
- Windows tests pass 23/23.
- The App Release build succeeds with the existing `_frameClaimantId` warning.
- `git diff --check` passes.

## Review Status

The implementation serializes the complete GDI+ bitmap and pixel-readback section with a private `SemaphoreSlim`. The wait observes cancellation before native work starts. The task remains In Review because the stress test did not reproduce a native failure and does not yet combine long-running native work with cancellation storms.

## Related Tasks

- ICW-P0-ACTIVECOUNT: original ceiling fix (prerequisite for understanding peak concurrency)
- ICW-P1-COOPERATIVE-CANCEL: should land first to reduce GDI+ call duration
- ICW-P0-ACTIVECOUNT-residuals: duplicate admission race adds a second trigger
