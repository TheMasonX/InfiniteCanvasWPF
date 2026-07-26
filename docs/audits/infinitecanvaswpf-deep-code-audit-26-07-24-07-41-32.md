# InfiniteCanvasWPF Deep Code Audit

- Repository: `TheMasonX/InfiniteCanvasWPF`
- Audit baseline: commit `43bfd55bbae7e14a590784f7831e5261eecfd69b`
- Report hash: `icw-audit-7895f86e1a6a`
- Date: 2026-07-24

## Executive summary

The codebase is in strong greenfield shape overall: the core math, spatial indexing, rendering interop, and test scaffolding are coherent and mostly well-covered. The main risks are not low-level correctness failures; they are architectural drift, stale documentation/task metadata, and a few implicit contracts that will become expensive once more scene types or data sources are added. [Sources: README.md L5-L20; docs/tasks/JIRA.md L7-L19; docs/tasks/active-tasks.md L10-L15]

The largest maintainability problem is that `MainWindow.xaml.cs` has become an orchestration monolith. It owns scene generation, view-model updates, render scheduling, camera policy, input handling, selection state, resize debounce, buffer lifecycle, and UI composition in one file. That is workable today, but it is the clearest place where future technical debt will compound. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L19-L349; src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs L10-L59]

The second risk is consistency drift: the README and task logs describe older scene behavior and validation counts that no longer match the current code and test inventory. That is not cosmetic; it makes the repo harder to trust as a source of truth. [Sources: README.md L5-L26; docs/tasks/JIRA.md L25-L38; docs/tasks/active-tasks.md L10-L15]

The third risk is implicit contracts. Several APIs assume data shape, key presence, or calling conventions without encoding them in types or guardrails. Those assumptions are fine while the demo is small, but they are exactly the kind of hidden coupling that turns into brittle edge cases later. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L445-L452; src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L87; src/InfiniteCanvas.Rendering/SampleImageTile.cs L21-L44, L205-L242]

## Findings

### 1) High — `MainWindow.xaml.cs` is doing too much

`MainWindow.xaml.cs` now contains generation, render scheduling, spatial queries, camera math, input event handling, frame composition, selection animation, resize debounce, busy-state tracking, and resource cleanup. The file is over 1,000 lines and spans nearly every subsystem the app has. That is a classic "God Object" / Large Class smell and the clearest architectural refactor target. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L19-L349; L389-L349]

Why it matters: any future change in one area (camera policy, tile generation, render buffering, UI behavior) risks side effects in another. It also makes focused testing harder because orchestration and policy are interleaved. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L104-L156, L187-L223, L265-L349]

Recommendation: split the window into at least three services/facades:
`SceneGenerationService`, `ViewportRenderCoordinator`, and `ViewportInteractionController` (names are suggestions, not mandates). Keep `MainWindow` as a thin binding shell. [Sources: src/InfiniteCanvas.ViewModels/CanvasViewportViewModel.cs L31-L58; docs/tasks/README.md L9-L19]

Confidence: 96%

### 2) High — Task log / README drift is already visible

The README still describes the earlier point-cloud style demo and says the sample scene generates eight deterministic `8192x2048` Gray8 images and ingests 250 more points every 500 ms, publishing a snapshot every two seconds. The current code instead defaults to a 64-tile inspection scene (`2 x 32`), uses annotation patches, and does not have the periodic ingestion loop described there. [Sources: README.md L5-L26; src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L87; src/InfiniteCanvas.App/MainWindow.xaml.cs L104-L156]

The task tracker also appears stale on validation counts: `docs/tasks/JIRA.md` records ICW-013 as "22/22", but the visible test files in this commit cover 24 named tests across the fetched suites (`CameraTransformTests`, `LiveSpatialIndexServiceTests`, `StrTreeSpatialIndexServiceTests`, `CoalescingAsyncActionTests`, `SampleImageGeneratorTests`, `CanvasViewportViewModelTests`, and `ZeroCopyBitmapFactoryTests`). [Sources: docs/tasks/JIRA.md L31-L38; tests/InfiniteCanvas.Tests/CameraTransformTests.cs L10-L95; tests/InfiniteCanvas.Tests/LiveSpatialIndexServiceTests.cs L11-L119; tests/InfiniteCanvas.Tests/StrTreeSpatialIndexServiceTests.cs L11-L26; tests/InfiniteCanvas.Tests/CoalescingAsyncActionTests.cs L10-L59; tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs L10-L114; tests/InfiniteCanvas.Tests/CanvasViewportViewModelTests.cs L12-L77; tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs L13-L70]

Why it matters: docs and task trackers are part of the repo’s memory. When they drift, future work gets duplicated, and validation evidence becomes less trustworthy.

Recommendation: refresh README and JIRA/active-tasks to reflect current behavior, current defaults, and actual test counts. Treat validation records as per-commit evidence, not permanent claims. [Sources: docs/tasks/README.md L9-L19; docs/tasks/JIRA.md L21-L38]

Confidence: 99%

### 3) High — `SampleImageGenerator.GenerateSet` has an implicit API contract that is easy to misuse

`GenerateSet` accepts both `imageCount` and `rows`, but when `rows` is supplied the method silently ignores `imageCount` and returns `columns * rows` tiles. That is fine for the current call sites, but the method name and parameter list suggest `imageCount` is authoritative. [Sources: src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L47]

Why it matters: this is the kind of hidden contract that creates surprising bugs as soon as another caller uses the API differently. It is especially brittle in a greenfield codebase where the method may be copied into new demos or test fixtures. [Sources: src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L47; tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs L10-L28, L30-L50]

Recommendation: rename the API to make the grid shape explicit, or split into overloads such as `GenerateSetByCount(...)` and `GenerateSetByGrid(columns, rows, ...)`. Add a unit test that asserts the intended precedence rule. [Sources: tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs L10-L28]

Confidence: 90%

### 4) Medium — `CreateAnnotationToolTip` depends on untyped feature keys

`CreateAnnotationToolTip` reads `annotation.Features["Confidence"]` and `["Severity"]` directly. That will throw if any future annotation source omits either key, changes naming, or carries non-numeric metadata. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L445-L452; src/InfiniteCanvas.Rendering/SampleImageTile.cs L205-L242]

Why it matters: `SampleAnnotation` is a public record, so this is an implicit contract rather than a compiler-enforced invariant. The current generator always supplies those keys, but the API shape invites accidental misuse later. [Sources: src/InfiniteCanvas.Rendering/SampleImageTile.cs L205-L215; src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L125-L165]

Recommendation: model the known features as typed properties on `SampleAnnotation`, or add a small `TryGetFeature`/fallback path in the tooltip builder. [Sources: src/InfiniteCanvas.Rendering/SampleImageTile.cs L205-L242]

Confidence: 92%

### 5) Medium — Magic numbers are everywhere and several are policy, not implementation detail

The codebase is full of hard-coded policy values: camera clamps (`0.01`, `50`), resize debounce (`150 ms`), anchor-pan tick rate (`16 ms`), viewport clamp (`4096`), dead zone (`6`), anchor gain (`0.12`), animation duration (`420 ms`), and tile/layout defaults (`2 x 32`, `64-template pool`). Most of these are valid choices, but they are scattered and unnamed. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L23-L29, L55-L62, L194-L196, L140-L155, L311-L349; src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L33, L182-L200]

Why it matters: primitive obsession makes behavior harder to reason about, harder to tune, and easier to break when the next feature slice arrives. In a greenfield project, these should become named policies/configuration values before they fossilize. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L23-L29, L55-L62, L140-L155; src/InfiniteCanvas.Rendering/SampleImageGenerator.cs L23-L33]

Recommendation: centralize them into a `ViewportPolicy`, `RenderPolicy`, and `SceneGenerationPolicy` record or options class. Keep the values visible and test them directly. [Sources: docs/tasks/README.md L13-L19]

Confidence: 89%

### 6) Medium — The overlay rebuild path is the next scale bottleneck, and the backlog already knows it

`BuildFrameVisual` recreates the overlay visual tree on every frame, including new `Border`, `Grid`, `Rectangle`, `TextBlock`, and brush instances for each visible annotation. That is acceptable for a demo, but it will become expensive as visible annotation density rises. The backlog already tracks overlay pooling as ICW-007, so this is not a new duplicate task; it is the next obvious scale pressure point. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L265-L349; docs/tasks/JIRA.md L13-L16]

Why it matters: retained WPF element churn will dominate frame time long before the spatial index or bitmap composition do, especially on resize and drag. [Sources: src/InfiniteCanvas.App/MainWindow.xaml.cs L265-L349; docs/tasks/JIRA.md L13-L16]

Recommendation: keep the current behavior for now, but move element pooling or a custom retained visual layer ahead of feature work that increases annotation density. [Sources: docs/tasks/JIRA.md L13-L16]

Confidence: 84%

### 7) Low — `ZeroCopyBitmapFactory` is solid, but the render lock is a serialization point

The Windows interop path is generally well handled: the bitmap is created from a memory-mapped section, the returned `InteropBitmap` is frozen, and the factory lifecycle is validated by tests. The tradeoff is that `GenerateFrozenBitmap` serializes all callers on `_lifetimeGate` and clears the entire buffer each call. [Sources: src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs L14-L18, L62-L108, L119-L145; tests/InfiniteCanvas.Windows.Tests/ZeroCopyBitmapFactoryTests.cs L13-L70]

Why it matters: this is not a correctness bug, but it is a throughput ceiling if frame generation becomes more frequent or if multiple render paths are added later. [Sources: src/InfiniteCanvas.Rendering/ZeroCopyBitmapFactory.Windows.cs L62-L145]

Recommendation: keep the current implementation for correctness, but treat it as a bounded single-writer surface. If throughput becomes a problem, the next step is partial invalidation or a different presentation pipeline, not more lock contention. [Sources: README.md L57-L64; docs/ADR/0001-benchmark-project-targeting-and-baselines.md L10-L24]

Confidence: 82%

## Additional notes

The core math and spatial pieces are in good shape. `CameraTransform` has dedicated tests for pan/zoom, capture stability, clamp behavior, and non-uniform scaling, and `LiveSpatialIndexService` has good coverage around snapshot publication and concurrent queries. That is the strongest part of the repo right now. [Sources: tests/InfiniteCanvas.Tests/CameraTransformTests.cs L10-L95; tests/InfiniteCanvas.Tests/LiveSpatialIndexServiceTests.cs L11-L94]

`ICW-007` is still the one explicitly tracked follow-up I would not duplicate here: overlay pooling is already on the backlog in `docs/tasks/JIRA.md`. I would keep this audit focused on the architectural debt above instead of creating a second ticket for the same work. [Sources: docs/tasks/JIRA.md L13-L16]

## Assumptions

- I treated commit `43bfd55bbae7e14a590784f7831e5261eecfd69b` as the audit baseline.
- I treated the repo task logs as intended source-of-truth for planned work, and used them to avoid duplicating already-tracked items.
- I counted tests from the visible fetched test files in this commit, not from any external runner output. [Sources: docs/tasks/README.md L9-L19; docs/tasks/JIRA.md L21-L38]

## Open questions

- Should `GenerateSet` treat `imageCount` or `(columns, rows)` as the authoritative shape input when both are supplied?
- Should annotation features remain dictionary-based, or should the known keys become typed properties?
- Should the README describe the current inspection scene, or is there still a planned return to the older point-cloud demo?
- Is overlay pooling still intentionally deferred under ICW-007, or should it be promoted now that frame composition is fully interactive? [Sources: docs/tasks/JIRA.md L13-L16; docs/tasks/active-tasks.md L10-L15]

## Suggested next implementation slice

1. Extract `MainWindow` orchestration into small services with testable seams.
2. Replace magic-number policy values with named options/records.
3. Make annotation metadata strongly typed.
4. Refresh README, JIRA, and active-tasks so validation counts and runtime behavior match the current commit.
5. Add tests for `GenerateSet` shape precedence and tooltip key fallback. [Sources: docs/tasks/README.md L9-L19; docs/tasks/JIRA.md L31-L38; tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs L10-L114]
