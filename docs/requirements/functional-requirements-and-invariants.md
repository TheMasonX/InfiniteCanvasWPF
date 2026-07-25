# Functional Requirements and Invariants Registry

This document is the canonical place for user-visible behavior requirements and invariants that have regressed more than once. It complements ADRs and tickets:

- ADRs capture architectural decisions and ownership boundaries.
- Tickets capture implementation work and delivery scope.
- This registry captures the behavioral contract that must remain true across future changes.

## Maintenance policy

- Update this registry whenever a user requirement, invariant, or regression risk is captured or changed.
- Link each entry to the relevant ticket(s), ADR(s), and any regression tests when they exist.
- If a change would alter one of these invariants, update this document in the same change so the requirement stays discoverable.

## Canonical requirements

| Area | Requirement | Related work | Notes |
| --- | --- | --- | --- |
| Zoom behavior | Uniform zoom is preferred whenever neither axis is clamped. Anisotropic zoom is only acceptable while one axis is constrained by the viewport fit floor. The policy must recover to uniform zoom as soon as the clamping condition no longer applies. | ICW-044, ICW-046 | This is a core interaction invariant and should be treated as a regression-sensitive behavior. |
| Camera viewport sizing | The render target, zoom-floor calculation, and camera clamp must use the fixed visible viewport dimensions only. Scrollbar extent or scaled-scene dimensions must never replace those dimensions. | ICW-065 | Violating this invariant can make zoom-out unreachable and allocate a smeared oversized raster. |
| Zoom UI | The zoom control should provide an integrated custom-value entry with a shared Apply/Enter workflow and clear validation feedback. | ICW-045 | Keep the custom entry compact and aligned with the existing preset flow. |
| Background tiles | The default background tile height should be taller than the baseline tile layout, and tile generation should remain non-blocking when the scene is first presented. | ICW-039, ICW-047 | Preserve first-frame responsiveness even when background imagery is generated lazily. |
| Deterministic tile generation | A scene master seed must derive independent deterministic random streams for each tile and annotation operation. Lazy parallel generation must not share mutable RNG state, and generating a tile concurrently must produce the same pixels as generating it serially. | ICW-050 | Use an isolated per-operation generator; do not use a shared `Random` from tile worker tasks. |
| Sparse image tiles | Sparse image tiles should be generated on demand as the viewport approaches them, and they should follow the same cache-oriented lifecycle as background image generation. | ICW-047 | The generation path should remain computationally acceptable and should not block the main render path. |
| Image tile display | A visible toggle should control whether sparse image tiles are displayed. | ICW-047 | The default should remain predictable and the toggle should be discoverable in the display panel. |
| Cache policy | Cache size should be guided by pixel cost rather than simple item count, the default budget must retain at least one default-size tile, and cache status should be visible for debugging. | ICW-047, ICW-049 | This avoids overfitting cache behavior to tile counts, prevents immediate eviction of every default tile, and makes cache pressure easier to reason about. |
| Lazy tile cache admission | Cache capacity is a byte-based memoization ceiling, not a reason to evict active viewport tiles. Visible-frame tiles are protected during rasterization; requests that cannot be admitted render placeholders without retrying continuously, and failed generation releases its reservation. | ICW-064 | The default ceiling is 4 GiB of Gray8 source bytes and diagnostics must show byte use plus resident tile count. |
| Tile cache capacity and metrics | The tile cache must retain many generated tiles when the configured pixel budget permits it. Runtime and benchmark paths must expose queue depth, generation, conversion/copy, eviction, cache residency, and frame timing before changing image-generation or pixel-transfer technology. | ICW-064 | Candidate 16-pattern stamping, 8bpp indexed/Gray8, SIMD/hardware, or marshalling approaches require comparable benchmark evidence before adoption. |
| Annotation labels | Annotation labels should default to a smaller size than the old baseline, and the label mode should support either class or ID, defaulting to class. | ICW-041 | Keep labels legible without overwhelming the scene. |
| Overlay layering | Tile-grid boundaries should be drawn between the raster image layer and the annotation overlay using the same camera snapshot as the raster frame. | ICW-040 | The overlay should be camera-synchronized and non-hit-testable. |
| Sparse defect imagery | Sparse defect imagery should be rendered as grayscale bitmap content on a separate image layer without tinting or clipping to the logical annotation bounds. | ICW-042 | Preserve the original bitmap intensity and keep the defect layer distinct from annotation styling. |
| Annotation inspection | Selecting an annotation should populate a sidebar DataGrid with the annotation feature values, and the grid should clear on deselection or regeneration. | ICW-048 | This is part of the inspection workflow and should remain available in the UI. |
| Settings persistence | Display and generation settings should persist across app runs and be saved on close. | ICW-043 | Preserve user-adjusted state even after the app restarts. |

## Regression review checklist

When a change touches any of the behaviors above, confirm all of the following:

1. The requirement is still reflected in this registry.
2. The relevant task ticket and any impacted ADR remain aligned.
3. Regression coverage or validation evidence exists for the affected behavior.
4. The UX remains consistent with the documented invariant, even if the implementation approach changes.
