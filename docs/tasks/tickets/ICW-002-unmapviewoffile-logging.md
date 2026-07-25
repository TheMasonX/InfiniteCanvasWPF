---
status: draft
summary: Surface and log UnmapViewOfFile failures in ZeroCopyBitmapFactory.Dispose
scope: |
  - Check return value of `UnmapViewOfFile` and call `Marshal.GetLastWin32Error()` on failure.
  - In DEBUG builds throw a `Win32Exception` to catch failures during development; in Release trace a `Trace.TraceError` line.
  - Add unit test or instrumentation to verify error path is exercised.
files_to_change:
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
validation_command: |
  dotnet build src/InfiniteCanvas.Rendering/InfiniteCanvas.Rendering.csproj -c Release
  dotnet test tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs -c Release --filter "GenerateFrozenBitmap_RejectsUseAfterDispose"
next_step: |
  - Apply patch adding error handling + trace; run tests and manual Dispose stress to observe logging.
---

Background

UnmapViewOfFile returns a boolean that is currently ignored. On rare platform failures this can hide resource leaks. This ticket ensures failures are surfaced during development and logged in production.

Acceptance criteria

- `Dispose(bool)` checks the return value and logs `Marshal.GetLastWin32Error()` on failure.
- DEBUG builds throw `Win32Exception` to surface the failure during development runs.
- Unit/integration test added to cover logging path.
