---
id: ICW-319-canvascontrol-boundary-api
author: InfiniteCanvas Agent
key: ICW-319
title: Replace the CanvasControl raw-element surface with a method-based API
status: Done
type: Improvement
priority: P2
tags:
  - canvas
  - boundary
  - library-extraction
dependsOn:
  - ICW-312
  - ICW-315
related:
  - ICW-316A
  - ICW-316
links:
  - src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs
  - src/InfiniteCanvas.App/MainWindow.xaml.cs
  - docs/audits/audit-synthesis-reconciliation-26-08-04-22-15-00.md
created: 2026-08-04
updated: 2026-08-04
---

# ICW-319 — Replace the CanvasControl raw-element surface with a method-based API

## Summary

Audit synthesis finding F-005. `CanvasControl` exposes seven public element properties (`SurfaceHost`, `FrameHost`, `LoadingText`, `WorldReadout`, `TileReadout`, `ValueReadout`, `BusyBar`) and two overlay canvases (`TileGridLayer`, `AnnotationLayer`) that `MainWindow` mutates directly. `LoadingOverlay` has a hardcoded `Margin="0,446,0,0"`. This blocks a clean ICW-316 assembly extraction.

## Scope

- Replace the public aliases with methods: `SetLoadingState`, `SetBusyIndicatorVisible`, `SetPixelometerReadout`, `ClearFrame`, `SetViewportSize`.
- Route all `MainWindow` mutation sites behind the methods.
- Pass the tile-grid and annotation overlay canvases through an internal or explicit overlay-host contract, not public properties.
- Center the loading overlay (`VerticalAlignment="Center"` or layout-derived position) instead of the hardcoded margin.

## Acceptance Criteria

- Zero remaining references to `CanvasSurface.TileGridLayer`, `AnnotationLayer`, `SurfaceHost`, `FrameHost`, `LoadingText`, `WorldReadout`, `TileReadout`, `ValueReadout`, `BusyBar` outside the control.
- `FrameShellWiringTests` and `CanvasScrollbarWiringTests` stay green.
- `LoadingOverlay` position no longer depends on a fixed pixel margin.

## Validation

- Command: source scan of `src/` for the removed member names
- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release --filter "FrameShellWiringTests|CanvasScrollbarWiringTests"`
- Command: `dotnet build src/InfiniteCanvas.App --configuration Release`

## Notes

- Must complete before ICW-316B. It defines the library public face.
- One ticket for the whole public-surface work (C1-004, C2-005, C2-007, C2-010).

## Related Tasks

- ICW-316A (harden contracts)
- ICW-316 (physical move)
