---
id: ICW-021-extract-lockedbits-helper
key: ICW-021
title: Icw 021 Extract Lockedbits Helper
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - task-tracker
dependsOn: []
related: []
links:
  - docs/tasks/README.md
created: 2026-07-25
updated: 2026-07-25
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
