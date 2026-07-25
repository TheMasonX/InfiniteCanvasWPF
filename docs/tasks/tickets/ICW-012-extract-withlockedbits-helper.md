---
status: draft
summary: Extract `WithLockedBits` helper to centralize LockBits/UnlockBits usage
scope: |
  - Add helper `BitmapHelpers.WithLockedBits(Bitmap bmp, ImageLockMode mode, PixelFormat pf, Action<IntPtr,int> action)`.
  - Replace duplicated LockBits/UnlockBits code paths in `SampleImageGenerator`, `SampleImageTile`, and `ZeroCopyBitmapFactory.DrawDefectPatch` with the helper.
  - Add unit test ensuring UnlockBits is called even when action throws.
files_to_change:
  - src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
  - src/InfiniteCanvas.Rendering/BitmapHelpers.cs (new)
validation_command: |
  dotnet test --filter GenerateCenteredDefectBitmap
next_step: |
  - Implement helper and update call sites; add exception-safety unit test.
---

Background

Multiple codepaths lock and iterate bitmap bits manually. Extracting a helper reduces duplication and ensures correct UnlockBits usage on exceptions.

Acceptance criteria

- Helper exists and duplicated codepaths are replaced.
- Unit test validates UnlockBits is called on exception.
