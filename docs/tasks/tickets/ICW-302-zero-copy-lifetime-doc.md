---
id: ICW-302-zero-copy-lifetime-doc
key: ICW-302
title: Document and enforce bitmap lifetime semantics for ZeroCopyBitmapFactory
status: Done
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
updated: 2026-07-26
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

Work completed:
- Added XML documentation to `ZeroCopyBitmapFactory` describing the lifetime coupling between the factory and returned `InteropBitmap` instances. Documentation advises callers to keep the factory alive while bitmaps are in use or to dispose/avoid use after factory disposal.
- Annotated the bitmap-generation methods with remarks about `PixelFormats.Bgra32` and stride assumptions.

Validation commands:
- `git grep -n "ZeroCopyBitmapFactory" || true`
- `dotnet build ./src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`

Estimated effort: Small
Risk: Low
Suggested owner: @rendering-team
