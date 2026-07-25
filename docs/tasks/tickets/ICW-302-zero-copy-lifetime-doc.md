---
status: proposed
title: Document and enforce bitmap lifetime semantics for ZeroCopyBitmapFactory
repo-area: src/InfiniteCanvas.Rendering
severity: high
assignee: rendering-team
---

Summary:
The `ZeroCopyBitmapFactory` API currently returns `InteropBitmap` instances whose validity depends on the lifetime of the factory's file-mapping. This coupling is undocumented and risky.

Scope:
- `src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs`
- Public call sites in App layer

Acceptance criteria:
- Add XML docs and README entries describing lifetime coupling, or change API to return wrapper objects that keep mapping alive.
- Add tests to validate behavior (bitmap survive or explicit invalidation behavior documented).

Validation commands:
- `git grep -n "ZeroCopyBitmapFactory" || true`
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
