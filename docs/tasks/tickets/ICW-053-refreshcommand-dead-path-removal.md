---
status: proposed
summary: Remove or rewire dead `RefreshCommand` in `CanvasViewportViewModel`
scope:
  - src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
  - tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter FullyQualifiedName~CanvasViewportViewModelTests
findings_evidence: |
  - `RefreshCommand` calls `_spatialIndexService.Query(viewport)` but `MainWindow` uses `ApplyFrame` (audit evidence, docs/audits).
  - `RefreshCommand` is invoked only by tests, making it dead in production and duplicative of `ApplyFrame` semantics.
next_steps:
  - Decide canonical behavior: delete `RefreshCommand` and tests, or keep but wire a UI Refresh affordance. Owner: @maintainer
  - If deleting, update tests to remove reliance on the command and add a unit test for `ApplyFrame` timestamp behavior. Owner: @maintainer
  - If wiring, add a UI button bound to `RefreshCommand` and document intended usage. Owner: @maintainer
