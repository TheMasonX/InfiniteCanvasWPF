# InfiniteCanvasWPF Net-New Audit

Date: 2026-07-24
Baseline reviewed:
- docs/audits/infinitecanvaswpf-code-audit-26-07-24-13-10-55.md
- docs/audits/infinitecanvaswpf-code-audit-addendum-26-07-24-22-24-24.md
- docs/audits/infinitecanvaswpf-deep-dive-audit-pass2-26-07-24-22-24-22.md

## 1. Executive Summary (Net-New Only)

This pass reconciled prior audits against current HEAD and backlog coverage. Most previously reported technical issues are already captured in ICW-014 through ICW-033. Four net-new backlog gaps were identified and promoted into durable tasks:

- ICW-034 (P1): coalesced render scheduler fault handling and follow-up request preservation.
- ICW-035 (P1): renderer and pixelometer blend-contract divergence.
- ICW-036 (P2): missing CI and nullable-enforcement baseline.
- ICW-037 (P3): MainWindow accessibility baseline.

In addition, existing tasks ICW-014, ICW-023, ICW-029, and ICW-030 were extended with sharper acceptance direction and concrete evidence.

## 2. New Findings

### NF-01: Coalesced render scheduler can fault-leak and drop queued follow-up intent
- Severity: P1
- Confidence: High
- Evidence:
  - src/InfiniteCanvas.Core/CoalescingAsyncAction.cs:65 defines `ProcessAsync` loop.
  - src/InfiniteCanvas.Core/CoalescingAsyncAction.cs:79 awaits `_action(_lifetime.Token)` without local exception policy.
  - src/InfiniteCanvas.Core/CoalescingAsyncAction.cs:55 awaits `processingTask` in `DisposeAsync`, allowing stale non-cancellation faults to surface during teardown.
- Risk:
  - A thrown action fault can terminate the shared processing task and lose a coalesced `_requested` follow-up before the loop reevaluates pending work.
  - Fault propagation increases async-void escalation risk in the UI render request chain.
- Recommendation:
  - Implement explicit coalescer fault policy and queued-follow-up preservation logic, then verify dispose-time behavior under injected fault tests.
- Durable task:
  - ICW-034

### NF-02: Pixelometer blend computation diverges from renderer output contract
- Severity: P1
- Confidence: High
- Evidence:
  - src/InfiniteCanvas.App/MainWindow.xaml.cs:921 computes pixelometer value via `BlendDefect`.
  - src/InfiniteCanvas.App/MainWindow.xaml.cs:957 uses legacy grayscale subtraction blend.
  - src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs:223-225 applies class-tinted channel blend via `BlendChannel`.
- Risk:
  - User-visible mismatch between rendered defect pixel color and reported pixelometer value.
  - Duplicate formulas create ongoing drift risk.
- Recommendation:
  - Centralize blend and sampling helpers used by both renderer and pixelometer, then add parity tests.
- Durable task:
  - ICW-035

### NF-03: Repository lacks CI and centralized nullable-enforcement policy
- Severity: P2
- Confidence: High
- Evidence:
  - `.github` currently contains only `agents/` and `skills/`; no `.github/workflows` files.
  - No root `Directory.Build.props` and no `.editorconfig`/`global.json` found.
- Risk:
  - Build/test and warning policy enforcement depends entirely on manual runs and task-log attestation.
  - Nullable and analyzer regressions can accumulate without branch-level fail-fast gates.
- Recommendation:
  - Add Windows CI workflow for build and tests plus centralized warning policy.
- Durable task:
  - ICW-036

### NF-04: MainWindow control surface has no accessibility baseline metadata
- Severity: P3
- Confidence: High
- Evidence:
  - src/InfiniteCanvas.App/MainWindow.xaml:99 (`ZoomPresetComboBox`), 116 (`ApplyCustomZoomButton`), 158 (`RegenerateButton`), 160 (`DebugDumpCacheButton`) show interactive controls.
  - No `AutomationProperties` or `KeyBinding` usage found in MainWindow XAML/code-behind.
- Risk:
  - Reduced keyboard and assistive-technology operability for primary workflows.
- Recommendation:
  - Add automation names and keyboard affordances for key controls without changing visual behavior.
- Durable task:
  - ICW-037

## 3. Corrections/Extensions to Existing Tasks

- ICW-014 extended:
  - Added explicit linkage to coalescer fault surfacing risk and dependency on ICW-034.
- ICW-023 extended:
  - Added low-priority nits: unchecked `UnmapViewOfFile` return handling, parse-culture consistency, degenerate render-bounds guards, and argument-attribution cleanup in `Bgra32BufferLayout.GetPixelOffset`.
- ICW-029 extended:
  - Added busy-indicator dispatcher teardown risk (`Dispatcher.Invoke` during close-time race paths).
- ICW-030 extended:
  - Added amplification note for current defect-raster multipliers (`2.4x` to `4.5x`) increasing impact of unbounded object counts.

## 4. Priority Order (P0-P3)

- P0
  - None newly identified in this pass.
- P1
  - ICW-034
  - ICW-035
- P2
  - ICW-036
- P3
  - ICW-037
  - ICW-023 extension items

## 5. Open Questions and Validation Gaps

- Coalescer policy direction:
  - Should action faults be swallowed and logged, or surfaced via callback/event while still preserving queued follow-up intent?
- Pixelometer parity verification:
  - Which invariant should be asserted in tests: exact byte parity with rendered blend output or bounded tolerance after rounding?
- CI baseline scope:
  - Should warning enforcement begin with nullable-only (`WarningsAsErrors=nullable`) or full warnings-as-errors from day one?
- Accessibility baseline scope:
  - Is current target limited to keyboard and automation names, or should screen-reader narration and tab-order tests be included now?
