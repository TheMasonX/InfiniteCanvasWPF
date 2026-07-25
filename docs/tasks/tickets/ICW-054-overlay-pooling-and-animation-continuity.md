---
status: proposed
summary: Pool annotation overlay elements and preserve selection animation continuity
scope:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - src/InfiniteCanvas.App/MainWindow.xaml
  - docs/tasks/tickets/ICW-019-overlay-animation-continuity.md
validation_command: |
  dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --filter FullyQualifiedName~ZeroCopyBitmapFactoryTests
findings_evidence: |
  - `BuildFrameVisual` allocates brushes, shapes, grids, and tooltips per-annotation per-frame causing allocations and animation restart (audit findings 2.7, ICW-007 evidence).
  - Selected `Rectangle` outline gets recreated each frame causing its animation to restart.
next_steps:
  - Implement simple object pool for overlay UI elements keyed by annotation id; reuse existing Shapes when visible. Owner: @engineer
  - Preserve selection-outline `Shape` instance across frames or transfer animation clock to new instance. Owner: @engineer
  - Add visual regression test harness or manual verification steps documenting continuity. Owner: @engineer
