# InfiniteCanvasWPF Net-New Audit

Date: 2026-07-25
Baseline reviewed:
- docs/audits/infinitecanvaswpf-net-new-audit-26-07-24-17-43-57.md
- docs/audits/infinitecanvaswpf-followup-audit-26-07-24-22-24-24.md
- docs/audits/infinitecanvaswpf-followup-audit-26-07-24-10-38-28.md

## 1. Executive Summary (Net-New Only)

This pass looked for defects and architectural risks that are still not captured sharply enough in the current backlog. Four findings stand out as concrete follow-up work with strong implementation value:

- ICW-077: the viewport scrollbar overlay relies on nullable state and unguarded layout metrics that are vulnerable to initialization timing failures.
- ICW-078: the render pipeline lacks an explicit stale-frame guard, which can permit older frame work to overwrite newer camera or scene state.
- ICW-079: the current busy-state bookkeeping is too granular for rapid input churn and can oscillate or misrepresent render activity.
- ICW-080: annotation feature formatting and selection presentation remain coupled to MainWindow and are hard to test independently.

## 2. New Findings

### NF-01: Scrollbar overlay geometry assumes initialized elements and well-measured layout
- Severity: P2
- Confidence: High
- Evidence:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs uses nullable track/thumb fields and passes them into the update path without a clear initialization contract.
  - Static diagnostics report possible null dereferences around the scrollbar metrics calculations.
- Risk:
  - Initial load, resize, and teardown timing can leave the overlay in a partially initialized state and break scroll interaction or cause layout exceptions.
- Recommendation:
  - Move scrollbar geometry updates behind an explicit initialization guard and add regression coverage for the uninitialized/partially measured case.
- Durable task:
  - ICW-077

### NF-02: Render publication has no explicit stale-frame protection
- Severity: P1
- Confidence: Medium-High
- Evidence:
  - MainWindow uses a coalesced render action and schedules frames from several asynchronous sources, but there is no request epoch or completion guard before frame publication.
  - The current flow publishes whatever frame finishes last without validating whether it still matches the latest viewport or scene state.
- Risk:
  - A slow frame can overwrite newer state after a pan, zoom, or regenerate operation. This is a correctness risk, especially during interactive navigation.
- Recommendation:
  - Attach an epoch to each render request and discard stale completions before they are published.
- Durable task:
  - ICW-078

### NF-03: Busy-state bookkeeping is too eager for rapid input churn
- Severity: P2
- Confidence: Medium-High
- Evidence:
  - MainWindow increments and decrements a busy-operation counter around every render request and regeneration callback.
  - Pointer movement, tile generation events, and selection changes can all queue render work in quick succession.
- Risk:
  - The busy overlay can oscillate rapidly or misrepresent the true state of ongoing work during bursty input.
- Recommendation:
  - Collapse the busy state to a coarser, more stable model tied to the render coalescer and active generation lifecycle.
- Durable task:
  - ICW-079

### NF-04: Annotation feature formatting is still coupled to WPF window logic
- Severity: P2
- Confidence: Medium-High
- Evidence:
  - MainWindow formats tooltip content and builds the feature grid directly from string-keyed annotation metadata.
  - The selection and formatting logic is not isolated from the visual tree, so it is hard to test without UI instantiation.
- Risk:
  - Inspection-panel regressions become more likely as metadata shape evolves, and the UI layer gathers more behavior than it should.
- Recommendation:
  - Extract a small presentation model for feature formatting and selection-state projection.
- Durable task:
  - ICW-080

## 3. Corrections / Extensions to Existing Tasks

- ICW-014 should remain the umbrella for application-level exception safety, but the new stale-frame and busy-state work should explicitly depend on its outcome to avoid overlapping fixes.
- ICW-031 remains the underlying metadata-model task; ICW-080 scopes the presentation-surface extraction only.

## 4. Priority Order (P0-P3)

- P1
  - ICW-078
- P2
  - ICW-077
  - ICW-079
  - ICW-080

## 5. Open Questions and Validation Gaps

- Whether the stale-frame guard should be implemented entirely in the coalescing action or at the MainWindow render publication boundary.
- Whether the busy-state model should be tied to a debounced overlay update or to the active generation/render subscription count.
- Whether the annotation presentation model should be a pure view-model or a formatter helper plus small adapter object.
