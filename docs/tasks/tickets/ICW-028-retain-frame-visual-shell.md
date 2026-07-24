# ICW-028: Retain Frame Visual Shell to Reduce Per-Frame UI Allocation

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Reduce per-frame UI allocation churn by retaining and updating a persistent frame visual shell instead of reconstructing root visual objects for every render.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- tests/InfiniteCanvas.Windows.Tests

## Validation

- Pending:
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`
  - interaction smoke test under sustained pan/zoom

## Findings

- `BuildFrameVisual` currently allocates new `Grid`, `Image`, and overlay `Canvas` each frame.
- ICW-007 addresses annotation pooling, but root frame container churn remains.

## Next Step

- Prototype persistent shell with source/layer updates and compare allocation profile against current behavior.