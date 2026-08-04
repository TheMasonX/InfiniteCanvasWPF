---
id: ICW-112
key: ICW-112
title: Expose structured TileCache diagnostics snapshot API
status: To Do
type: Task
priority: P2
tags:
  - diagnostics
  - rendering
dependsOn:
  - ICW-064
related:
  - ICW-098
created: 2026-07-26
updated: 2026-07-26
owner: unassigned
---

# ICW-112 - Expose structured TileCache diagnostics snapshot API

## Summary

Current tile cache diagnostics are textual and insufficient for debugging variant, reservation, and queued work state. Add a structured `TileCacheDiagnosticsSnapshot` providing active cache id, resident variant identities, queued work counts, reservation counts, and reset state, plus a throttled UI export option.

## Scope

- Add `TileCacheDiagnosticsSnapshot` in `src/InfiniteCanvas.Rendering` and a `GetDiagnosticsSnapshot()` API.
- Populate snapshot during cache updates; keep it lock-free or snapshot-copy to avoid contention.
- Expose a throttled `ExportDiagnostics` button in the debug panel that writes JSON to a user-specified file.
- Add unit tests for snapshot contents and export path.

## Acceptance Criteria

- `TileCacheDiagnosticsSnapshot` contains: `ActiveCacheId`, `ResidentCount`, `ResidentVariants`, `QueuedWorkCount`, `ReservationCount`, `EvictionCount`, `LastResetAtUtc`.
- Tests `TileCacheDiagnosticsSnapshotTests` validate fields under simulated workloads.

## Validation

- Command: `dotnet test --filter TileCacheDiagnosticsSnapshotTests`

## Notes

- Keep snapshot generation lightweight and avoid capturing full tile payloads in the JSON export.
