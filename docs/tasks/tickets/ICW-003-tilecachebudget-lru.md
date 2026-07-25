---
status: draft
summary: Replace Dictionary-only TileCacheBudget with deterministic LRU bookkeeping
scope: |
  - Add `LinkedList<string>` and `Dictionary<string, LinkedListNode<string>>` to `TileCacheBudget` for deterministic LRU eviction.
  - Update `TrackTile`, `Remove`, and `Clear` to maintain the linked list and remove nodes in O(1).
  - Update unit tests to assert eviction order and update cache description text if needed.
files_to_change:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
validation_command: |
  dotnet build src/InfiniteCanvas.Rendering/InfiniteCanvas.Rendering.csproj -c Release
  dotnet test tests/InfiniteCanvas.Tests/ --filter "TileCacheBudget*" -c Release
next_step: |
  - Implement LRU data structures, add explicit tests asserting LRU eviction, and run benchmarks to confirm memory usage.
---

Background

Eviction currently relies on `Dictionary.Values.FirstOrDefault()` which yields an implementation-dependent choice. Implementing an explicit LRU gives deterministic eviction and O(1) operations.

Acceptance criteria

- `TileCacheBudget` maintains LRU order; eviction removes least-recently-used tile.
- Unit tests validate eviction order and performance under pressure.
