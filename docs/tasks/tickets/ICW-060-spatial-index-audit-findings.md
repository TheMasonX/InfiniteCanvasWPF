---
id: ICW-060-spatial-index-audit-findings
author: External Audit (Integration-1)
key: ICW-060
title: Audit findings - spatial indexing subsystem (STALE - see description)
status: Archived
type: Task
priority: P2
tags:
  - spatial
  - audit
  - stale
dependsOn: []
related:
  - ICW-P0-SPATIAL-INDEX-SAFETY
links:
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
  - docs/audits/infinitecanvaswpf-icw-followup-audit-26-07-30-22-04-25.md
created: 2026-07-25
updated: 2026-07-30
---

# ICW-060 — Audit findings: spatial indexing subsystem (STALE — see description)

## Status: Deprecated

**This ticket is no longer accurate at HEAD.** The specific defects it describes have been fixed or are not applicable.

## What was claimed

The ticket described invalid returns, mutable lists exposed from STRtree, ambiguous boundary semantics, and publish-state edge cases in the spatial indexing subsystem.

## What the audits found

**External audit (80-90% confidence):**

1. **"Mutable STRtree list exposure"** — **Already fixed at HEAD.** `StrTreeSpatialIndexService.Query` already copies NTS's mutable `IList<T>` to an array with an explicit comment naming this exact concern. `LiveSpatialIndexService` already uses an immutable, lock-free CAS state machine.

2. **"LiveSpatialIndexService.Query mutability"** — **Already fixed in Sprint 1 Wave C.** `LiveSpatialIndexService.Query` now returns `.ToArray()` instead of `List<T>`. ICW-P0-SPATIAL-INDEX-SAFETY (Done) covers this.

3. **Ambiguous boundary semantics** — This is a real concern (`SpatialBounds.Intersects` uses closed-interval semantics while pixel/tile lookups elsewhere use half-open). This should be tracked under **ICW-033 (boundary semantics)**, not ICW-060.

## Recommendation

This ticket should be **closed or rescoped**. If there is a *different* remaining concern (e.g., `LiveSpatialIndexService.Query`'s `AppendMatches` doing an `O(n)` linear scan over `HotItems`/`PublishingItems` rather than the indexed `SnapshotIndex` — this is real but is a *performance* characteristic, not a safety/immutability bug), rewrite the ticket to describe that specific concern rather than leaving stale text.

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "SpatialIndex|LiveSpatialIndex|StrTree"
```
