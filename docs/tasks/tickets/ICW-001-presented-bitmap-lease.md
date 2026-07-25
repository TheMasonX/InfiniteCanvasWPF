---
id: ICW-001-presented-bitmap-lease
key: ICW-001
title: Icw 001 Presented Bitmap Lease
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

Background

Returning a frozen `InteropBitmap` backed by a memory-mapped section without a programmatic ownership/lease allows callers to dispose the factory while WPF still references the mapping. This ticket adds a lease/refcount wrapper so the factory can reliably detect active presented bitmaps and avoid unmapping while in use.

Acceptance criteria

- `GenerateFrozenBitmap` (or new API) returns a disposable wrapper that owns the presented bitmap lease.
- Disposing the factory while leases exist logs or fails deterministically in DEBUG and gracefully in Release.
- Unit tests exercising present/dispose races are added.
