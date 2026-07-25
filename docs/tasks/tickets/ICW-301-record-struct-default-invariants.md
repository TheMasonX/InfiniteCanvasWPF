---
status: proposed
title: Prevent default/invalid `record struct` states for CameraSnapshot and enforce scale invariants
repo-area: src/InfiniteCanvas.Core
severity: high
assignee: core-team
---

Summary:
Public `record struct` types such as `CameraSnapshot` can be default-initialized, producing invalid state (e.g., zero scale) that causes division-by-zero or NaN propagation in coordinate math.

Scope:
- `src/InfiniteCanvas.Core/CameraTransform.cs`
- Audit other `record struct` types in `src/InfiniteCanvas.Core` and `src/InfiniteCanvas.Rendering`.

Acceptance criteria:
- Prevent silent `default(T)` invalid states by providing validated constructors, factory methods, or converting to non-defaultable types.
- Add unit tests ensuring invalid/default snapshots are rejected or handled gracefully with explicit errors.

Validation commands:
- `git grep -n "record struct" -- src/InfiniteCanvas.Core src/InfiniteCanvas.Rendering`
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Estimated effort: Small
Risk: Medium
Suggested owner: @core-team
