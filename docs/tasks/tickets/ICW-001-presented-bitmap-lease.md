---
status: draft
summary: Add presented-bitmap reference-tracking to ZeroCopyBitmapFactory to enforce mapping lifetime
scope: |
  - Add `PresentedBitmap : IDisposable` wrapper returned by `GenerateFrozenBitmap(...)` or add `AcquirePresentedBitmap` API that returns both `InteropBitmap` and a lease `IDisposable`.
  - Track `_presentedCount` inside `ZeroCopyBitmapFactory` with `Interlocked` operations.
  - Make `Dispose(bool)` check `_presentedCount` and either wait briefly (configurable timeout) or throw a clear exception in DEBUG; update ADR-0004.
files_to_change:
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - docs/ADR/0004-zero-copy-buffer-lifecycle-and-handoff-policy.md
validation_command: |
  dotnet build src/InfiniteCanvas.Rendering/InfiniteCanvas.Rendering.csproj -c Release
  dotnet test tests/InfiniteCanvas.Windows.Tests/ --filter "ZeroCopyBitmapFactory*" -c Release
next_step: |
  - Implement `PresentedBitmap` wrapper and `_presentedCount` increment/decrement.
  - Add unit tests: Present+Dispose race test and stress test simulating compositor lifetimes.
---

Background

Returning a frozen `InteropBitmap` backed by a memory-mapped section without a programmatic ownership/lease allows callers to dispose the factory while WPF still references the mapping. This ticket adds a lease/refcount wrapper so the factory can reliably detect active presented bitmaps and avoid unmapping while in use.

Acceptance criteria

- `GenerateFrozenBitmap` (or new API) returns a disposable wrapper that owns the presented bitmap lease.
- Disposing the factory while leases exist logs or fails deterministically in DEBUG and gracefully in Release.
- Unit tests exercising present/dispose races are added.
