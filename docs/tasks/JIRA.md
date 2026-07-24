# Infinite Canvas Jira Task Log

| Key | Status | Type | Summary | Acceptance / Notes |
| --- | --- | --- | --- | --- |
| ICW-001 | Done | Story | Add repeatable performance benchmark harness | BenchmarkDotNet covers STR query selectivity, live snapshot/hot/publishing queries, snapshot rebuild allocation, and Windows projection plus bitmap generation. |
| ICW-002 | Done | Story | Capture an immutable camera state per frame | Viewport query and every projection use the same transform state. |
| ICW-003 | Done | Improvement | Remove duplicate spatial query per rendered frame | Render statistics reuse the viewport query result. |
| ICW-004 | To Do | Spike | Measure zoomed-out pixel overdraw | Results guide deduplication, accumulation, heatmap, or GPU rendering decisions. |
| ICW-005 | To Do | Story | Define DPI-aware resize and maximum surface policy | 4K/5K and per-monitor DPI behavior are explicit and tested. |
| ICW-006 | Done | Story | Model web inspection imagery and annotations | Generate eight configurable monochrome tiles with colored defects, indexed bounding boxes, labels, tooltips, and animated selection. |
| ICW-007 | To Do | Improvement | Pool retained annotation overlay elements | Reuse WPF elements if visible annotation density makes overlay rebuilds measurable. |
| ICW-008 | Done | Story | Correct scene layout to 2x16 and add viewport pixelometer readout | Default tile generation now builds a 2-column by 16-row scene, and viewport overlay reports mouse world coordinates with source pixel value. |
| ICW-009 | Done | Improvement | Keep overlay and raster synchronized during resize debounce | Frame presentation now scales a complete image+overlay visual together while waiting for debounced rerender updates. |
| ICW-010 | Done | Story | Add red annotation modes, defect-detail layer, RMB anchor-pan, and viewport-safe zoom clamp | Added swappable selection animation strategy, per-annotation render mode options, sparse 2x defect raster composition, right-button anchor panning, and zoom-out clamp that keeps viewport coverage within scene bounds. |
| ICW-011 | Done | Story | Use sparse object image patches, global annotation display options, and axis-clamped non-uniform zoom | Moved defect imagery to per-object sparse patches, applied one global annotation display option set (mode, outline width, label sizing/visibility), reversed anchor-scroll direction, and clamped zoom-out per axis while allowing the other axis to continue. |
| ICW-012 | Done | Story | Add side-panel controls, configurable 2x32+ material, regenerate flow, and bitmap-backed generation pools | Added runtime display/generation side panel with regenerate button, moved startup to fit-to-width, defaulted material to 2x32 tiles, generated sparse defect patches from a deterministic 64-template bitmap pool, and switched tile backgrounds to lazy bitmap-backed fetch on Windows. |

## Activity

| Date | Key | Update |
| --- | --- | --- |
| 2026-07-23 | ICW-001 | Added multi-target benchmark project, benchmark suites, documentation, and ADR-0001. |
| 2026-07-23 | ICW-002 | Captured one immutable camera snapshot for viewport query, raster composition, and overlay projection. |
| 2026-07-23 | ICW-003 | Reused render query results for visible statistics instead of querying the index twice. |
| 2026-07-23 | ICW-006 | Added deterministic inspection tiles, defect metadata, layered rendering, hover tooltips, and animated selection; recorded ADR-0002. |
| 2026-07-23 | ICW-008 | Switched default scene orientation to 2x16 and added live world/pixel pixelometer in the viewport overlay; logged deferred resize-overlay repaint as next follow-up. |
| 2026-07-23 | ICW-009 | Updated frame presenter to a Viewbox and fixed frame visual sizing so image and overlay remain aligned during resize debounce. |
| 2026-07-24 | ICW-010 | Started next interaction slice for annotation mode options, red swappable selection animation, defect-detail source raster, RMB anchor-pan, and strict zoom-out viewport clamping. |
| 2026-07-24 | ICW-010 | Completed slice with passing validation: `dotnet test tests/InfiniteCanvas.Tests --configuration Release`, `dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release`, and `dotnet build src/InfiniteCanvas.App --configuration Release`. |
| 2026-07-24 | ICW-011 | Started follow-up correction to move sparse imagery inside annotation bounds, switch annotation styling to global display options, and support axis-clamped non-uniform zoom behavior. |
| 2026-07-24 | ICW-011 | Completed with passing validation: `dotnet test tests/InfiniteCanvas.Tests --configuration Release`, `dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release`, and `dotnet build src/InfiniteCanvas.App --configuration Release`. |
| 2026-07-24 | ICW-012 | Started runtime controls and generation-flow update for side panel options, configurable X by Y material, and bitmap-backed lazy generation. |
| 2026-07-24 | ICW-012 | Completed with passing validation: `dotnet test tests/InfiniteCanvas.Tests --configuration Release`, `dotnet test tests/InfiniteCanvas.Windows.Tests --configuration Release`, and `dotnet build src/InfiniteCanvas.App --configuration Release`. |
