# InfiniteCanvasWPF Latest Commits Audit

- Repository: `TheMasonX/InfiniteCanvasWPF`
- Audit scope: latest `main` commits since `43bfd55bbae7e14a590784f7831e5261eecfd69b`
- Report hash: `3cb7aecea209eb85`
- Date: 2026-07-25

## Executive summary

The latest commit set is a large forward step in product direction, not just a patch release. It adds persistent user settings, viewport policy objects, task-tracker infrastructure, logging/about UI, new benchmark coverage, and a substantial expansion of docs/task metadata alongside code changes in the app shell, rendering, spatial, and view-model layers. That is a healthy sign for a greenfield project, but it also increases the cost of hidden contracts and orchestration coupling very quickly.

The core technical risk remains the same as in the prior audit, but with more urgency: `MainWindow.xaml.cs` is still the central composition point for generation, camera policy, input handling, render scheduling, overlay construction, state persistence, and shutdown. The new policy classes improve structure, but they do not yet remove the monolith pressure. [Sources: `src/InfiniteCanvas.App/MainWindow.xaml.cs` (previously fetched: `turn40file0`–`turn46file0`), `src/InfiniteCanvas.Core/ViewportZoomPolicy.cs` and `src/InfiniteCanvas.Core/ViewportScrollbarPolicy.cs` (new in latest diff), `docs/tasks/README.md` `turn78file0`]

The highest-value issues to address next are: implicit parameter and metadata contracts, policy/value-object bloat, stale task/doc drift, and fault containment around async/UI orchestration. None of these are catastrophic today; all of them are the kinds of seams that become expensive once more scenes, more settings, or more input paths are added.

## What changed in the latest commits

The latest delta introduces:
- persisted user settings and viewport policy abstractions
- new UI surface for settings/about/logging behavior
- a new tile-grid lookup helper
- expanded benchmark coverage for Windows tile materialization
- richer task-tracker and ADR scaffolding, plus many new tickets and audit docs
- continued work in the rendering and spatial layers

This is a meaningful maturation step, but it also means the repo now has multiple sources of truth: code, ADRs, task tracker, handoffs, benchmark docs, and audit corpus. That makes drift and duplicate planning more likely unless task hygiene stays tight. [Sources: compare result for latest main delta; `docs/tasks/README.md` `turn78file0`; `docs/tasks/JIRA.md` `turn60file0`; `docs/tasks/active-tasks.md` `turn61file0`]

## Actionable findings

### 1) High — orchestration is still concentrated in `MainWindow`

`MainWindow.xaml.cs` remains the coordination hub for scene generation, camera control, render scheduling, selection behavior, resize debounce, pixelometer updates, busy indicator state, and lifecycle cleanup. In the previous snapshot this was already large; the latest delta adds even more policy and persistence surface around it. That is a classic large-class / god-object smell and the strongest architectural refactor candidate. [Sources: `turn40file0`–`turn46file0`; latest diff summary]

Why this matters: every future feature lands in the same file, so the chance of unrelated regressions climbs. The more policy classes and settings you add, the more that file becomes a de facto application service locator.

Recommendation: split orchestration into explicit services with narrow ownership boundaries: one for scene generation/persistence, one for render orchestration, one for input/pan/zoom policy, and one for overlay composition. Keep the window thin.

Confidence: 96%

### 2) High — `SampleImageGenerator` still exposes an ambiguous shape contract

The generator API still accepts both `imageCount` and `rows`, and when `rows` is supplied it computes `tileCount = columns * rowCount`, effectively overriding `imageCount`. That behavior is deterministic, but it is not obvious from the signature and is easy to misuse. [Source: `turn31file0` L23-L47]

Why this matters: the method looks like a “count-driven” factory, but it is actually a “grid-driven” factory when `rows` is supplied. That is a hidden contract and a future source of off-by-one or silent shape mismatch bugs.

Recommendation: split the API into explicit count-based and grid-based entry points, or rename the current API to make shape precedence impossible to miss. Add a regression test for precedence semantics.

Confidence: 92%

### 3) High — annotation metadata is still dictionary-shaped and key-sensitive

`CreateAnnotationToolTip` reads `annotation.Features["Confidence"]` and `["Severity"]` directly. That is a runtime contract that will throw if the generator or any future data source changes keys, omits them, or makes them non-numeric. [Source: `turn45file0` L32-L40; `turn34file0` L205-L242]

Why this matters: this is fine while the demo is closed-world, but it is brittle once different annotation sources or export/import flows are added.

Recommendation: make the known annotation metrics typed properties, or add a safe `TryGetFeature` layer with fallback text in the tooltip builder.

Confidence: 91%

### 4) Medium — parameter/policy objects help, but they also add hidden complexity

The new policy/settings layer is a good direction, but it can easily become “primitive obsession with better names” if the values are not clearly owned and tested. The repo already has many hard-coded policy values in the interaction/render paths: zoom floor, timer cadence, resize debounce, dead zone, gain, and viewport clamp behavior. [Sources: `turn17file0` L13-L27, L128-L187; `turn40file0`–`turn46file0`]

Why this matters: policy values are now spread across code, policies, and UI. That makes it harder to tell which values are hard requirements, which are tunables, and which are demo defaults.

Recommendation: centralize policy values into a small number of named option records and keep the defaults in one place. Make policy tests assert behavior, not just object state.

Confidence: 84%

### 5) Medium — async error handling and fire-and-forget paths remain a risk area

The codebase still relies on `async void` event handlers and UI-driven command execution patterns. `CanvasViewportViewModel.RefreshAsync` runs its query off-thread without local error recovery, and the orchestration layer depends on `RequestRenderAsync`/`CoalescingAsyncAction` for scheduling. [Source: `turn48file0` L31-L58; `turn18file0` L20-L83; `turn53file0` L10-L58]

Why this matters: faults in the render/query path can surface through event handlers or task completion paths in inconsistent ways, especially under cancellation, disposal, or rapid user input. The latest task corpus explicitly contains several tickets around async event handlers, shutdown order, and view-model command errors, which is a good sign that this is already felt in the codebase. [Sources: latest diff task-file additions; `docs/tasks/README.md` `turn78file0`]

Recommendation: add a deliberate fault boundary at the top of the UI orchestration path and keep `async void` handlers minimal, with all real work delegated to awaitable methods that log and translate errors consistently.

Confidence: 80%

### 6) Medium — task/doc drift is now a maintainability issue, not a nit

The repo’s task tracker and docs are no longer a single concise backlog; they now include a large corpus of tickets, handoffs, ADRs, benchmark docs, and audit artifacts. That can be useful, but it raises the duplication and stale-state risk sharply. The previous README/task snapshot already contained older behavior descriptions and fixed validation counts, and the latest work expands the documentation surface further. [Sources: `turn6file0` L5-L26; `turn60file0` L21-L38; `turn61file0` L7-L15; `turn69file0` L3-L10]

Why this matters: if docs and task files are not actively reconciled with the current code path, future work will be planned twice, or planned against assumptions that no longer hold.

Recommendation: keep one authoritative backlog file plus ADRs for decisions; archive or link the rest from there. Add a small “doc freshness” check to the task tracker workflow.

Confidence: 94%

### 7) Low — zero-copy rendering is still solid, but it is a serialized critical section

`ZeroCopyBitmapFactory` remains a decent Windows-specific implementation: it creates a memory-mapped section, returns a frozen `InteropBitmap`, and is covered by tests. The tradeoff is that bitmap generation is serialized under one gate and the whole buffer is cleared each pass. [Source: `turn29file0` L14-L108, L119-L145; `turn56file0` L13-L70]

Why this matters: this is not a correctness bug today, but it is a throughput ceiling if frame rate or visible-annotation density grows.

Recommendation: keep it for now, but treat it as a single-writer surface. If performance becomes a problem, the next step is partial invalidation or a different presentation strategy, not more locking.

Confidence: 83%

## Good signs worth preserving

`CameraTransform` has good coverage for pan/zoom behavior, snapshot stability, and clamp behavior. `LiveSpatialIndexService` also has useful tests for publication, concurrent queries, and no-duplication behavior. Those are exactly the kinds of tests that keep a greenfield codebase from drifting. [Sources: `turn50file0` L10-L95; `turn51file0` L11-L94]

The benchmark setup is also sensible in principle: separate query, snapshot-build, and Windows projection/bitmap paths are measured independently, and benchmark timing is explicitly kept out of unit-test thresholds. [Sources: `turn65file0` L9-L34; `turn66file0` L9-L113; `turn67file0` L8-L29; `turn70file0` L17-L31]

## Assumptions

- This audit covers the 13 commits currently ahead of `43bfd55bbae7e14a590784f7831e5261eecfd69b` on `main`.
- I treated the task tracker and ADR corpus as planning metadata and used them to avoid duplicating work already tracked there.
- I did not run the build or test suite in this pass; the confidence values below reflect static review only.

## Open questions

- Should `GenerateSet` be count-driven, grid-driven, or both, and which parameter should be authoritative?
- Should the known annotation metrics become typed properties instead of dictionary lookups?
- Should `MainWindow` remain the render/input orchestrator, or is this the right moment to extract application services?
- Which docs are intended to be authoritative: the task tracker, handoffs, ADRs, or the new audit corpus?
- Is `CoalescingAsyncAction` meant to fail fast on action exceptions, or should it contain faults and keep the pipeline alive?

## Implementation guidance

The next implementation slice should be small and structural:
1. Extract one service out of `MainWindow` rather than trying to decompose it all at once.
2. Fix the ambiguous `GenerateSet` shape contract.
3. Replace the feature dictionary contract with typed annotation metrics.
4. Add a top-level UI fault boundary and standardize logging for async handlers.
5. Reconcile README/task metadata with the current code path before adding more feature tickets.

### Confidence summary
- Architectural monolith risk: 96%
- Generator contract ambiguity: 92%
- Annotation feature contract brittleness: 91%
- Policy bloat / primitive obsession risk: 84%
- Async error boundary risk: 80%
- Doc/task drift risk: 94%
- Zero-copy throughput ceiling risk: 83%
