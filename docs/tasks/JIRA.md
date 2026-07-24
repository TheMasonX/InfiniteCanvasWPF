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

## Activity

| Date | Key | Update |
| --- | --- | --- |
| 2026-07-23 | ICW-001 | Added multi-target benchmark project, benchmark suites, documentation, and ADR-0001. |
| 2026-07-23 | ICW-002 | Captured one immutable camera snapshot for viewport query, raster composition, and overlay projection. |
| 2026-07-23 | ICW-003 | Reused render query results for visible statistics instead of querying the index twice. |
| 2026-07-23 | ICW-006 | Added deterministic inspection tiles, defect metadata, layered rendering, hover tooltips, and animated selection; recorded ADR-0002. |
| 2026-07-23 | ICW-008 | Switched default scene orientation to 2x16 and added live world/pixel pixelometer in the viewport overlay; logged deferred resize-overlay repaint as next follow-up. |
| 2026-07-23 | ICW-009 | Updated frame presenter to a Viewbox and fixed frame visual sizing so image and overlay remain aligned during resize debounce. |
