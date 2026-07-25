---
status: open
summary: Extract `WithLockedBits` helper to remove duplicated LockBits/UnlockBits patterns
assignee: TBD
labels: [refactor, medium-risk, test]
---

Problem
-------
Multiple locations duplicate the `LockBits` / pointer-walk / `UnlockBits` pattern (`SampleImageGenerator`, `SampleImageTile`, etc.), increasing maintenance burden and risk of incorrect Unlock on exceptions. See audit notes: `docs/audits/infinitecanvaswpf-code-audit-26-07-24-13-10-55.md`.

Proposed change
---------------
Add a small `BitmapHelpers.WithLockedBits(Bitmap, ImageLockMode, PixelFormat, Action<IntPtr,int,stride>)` helper used by callers to centralize locking, unlocking, and defensive try/finally.

Risk level
----------
Low-medium — localized refactor, behavior-preserving if implemented carefully.

Validation
----------
Run existing tests under `InfiniteCanvas.Windows.Tests` and `InfiniteCanvas.Tests`.

Tests to add
------------
- Unit test verifying helper unlocks even when body throws (use a synthetic exception).
