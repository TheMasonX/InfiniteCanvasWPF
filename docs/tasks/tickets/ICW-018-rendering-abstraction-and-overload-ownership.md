# ICW-018: Rendering Abstraction and Point-Overload Ownership

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Resolve ownership and lifecycle for dormant rendering abstractions and benchmark-only rendering overloads.

## Scope

- src/InfiniteCanvas.Rendering/IRenderer.cs
- src/InfiniteCanvas.Rendering/ViewportRenderRequest.cs
- src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs
- tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs
- benchmarks/InfiniteCanvas.Benchmarks/ProjectionAndBitmapBenchmarks.Windows.cs

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - `dotnet test .\tests\InfiniteCanvas.Windows.Tests\InfiniteCanvas.Windows.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: `IRenderer` and `ViewportRenderRequest` are not consumed by the app.
- Point-only overload paths are currently exercised by tests and benchmarks.

## Next Step

- Either remove dead surfaces or document and enforce benchmark-only ownership boundaries.
