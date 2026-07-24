# ICW-031: Typed Annotation Metrics Instead Of String-Keyed Feature Map

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Replace brittle `Dictionary<string,double>` feature plumbing with typed annotation metrics to reduce runtime key-risk and improve refactor safety.

## Scope

- src/InfiniteCanvas.Rendering/SampleImageTile.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Tests
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- Generator emits metadata as `Dictionary<string,double>` and UI reads string keys directly.
- Contract is implicit and not compiler-checked.

## Next Step

- Introduce a typed metrics value object and migrate UI/tooltips/tests to strong members.
