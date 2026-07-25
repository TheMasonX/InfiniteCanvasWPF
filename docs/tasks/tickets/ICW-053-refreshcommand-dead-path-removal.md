---
status: done
summary: Remove dead `RefreshCommand` in `CanvasViewportViewModel`
scope:
  - src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs
  - tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs
validation_command: |
  dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter FullyQualifiedName~CanvasViewportViewModelTests
findings_evidence: |
  - Removed `RefreshCommand` and `RefreshAsync` from `CanvasViewportViewModel`.
  - Updated `CanvasViewportViewModelTests` to validate canonical `ApplyFrame` behavior for live and non-live index services.
next_steps:
  - None. Keep closed unless a concrete UI refresh requirement is introduced.
