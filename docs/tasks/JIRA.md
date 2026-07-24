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

## Activity

| Date | Key | Update |
| --- | --- | --- |
| 2026-07-23 | ICW-001 | Added multi-target benchmark project, benchmark suites, documentation, and ADR-0001. |
| 2026-07-23 | ICW-002 | Captured one immutable camera snapshot for viewport query, raster composition, and overlay projection. |
| 2026-07-23 | ICW-003 | Reused render query results for visible statistics instead of querying the index twice. |
| 2026-07-23 | ICW-006 | Added deterministic inspection tiles, defect metadata, layered rendering, hover tooltips, and animated selection; recorded ADR-0002. |
