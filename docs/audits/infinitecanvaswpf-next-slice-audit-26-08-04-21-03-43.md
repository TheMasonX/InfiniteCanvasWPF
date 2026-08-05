# InfiniteCanvasWPF — Next-Slice Audit and Implementation Strategy

**Report Hash ID:** `ICW-NEXT-F6F854D63C32`  
**Audit timestamp:** 2026-08-04 21:03:43 America/Chicago  
**Repository:** `TheMasonX/InfiniteCanvasWPF`  
**HEAD reviewed:** `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`  
**Prior report:** `ICW-AUD-F2D7D66DDA5B`  
**Nominal next ticket:** `ICW-316 — Extract the canvas component into its own assembly`  
**Recommended next slice:** `ICW-316A — Harden the reusable canvas boundary before physical extraction`  

---

## 1. Executive Decision

Do **not** implement `ICW-316` as currently written.

The repository has no commit newer than `84a0cdb5`, so there is no implementation delta to audit. The next planned work is `ICW-316`, but its ticket is presently framed as a project/file move:

- create a WPF control library,
- move `CanvasControl`,
- update project references,
- update solution and path-based tests,
- prove another host can reference it.

That sequence would move transitional coupling into a new assembly and make it harder to correct later.

The correct next slice is a bounded **contract-hardening slice before extraction**. It should resolve the duplicate visible-query authority, define frame immutability and revision semantics, eliminate public access to control internals, and add deterministic control lifecycle handling. Only then should the physical assembly move occur.

### Recommendation

Split `ICW-316` into two deliverables:

1. **ICW-316A — Harden reusable canvas contracts and lifecycle**
2. **ICW-316B — Physically extract the hardened component**

`ICW-316A` should be the next implementation slice.

---

## 2. Why the Current `ICW-316` Plan Is Unsafe

The existing ticket says:

> Move `CanvasControl` and `CanvasViewModel` into their own library so another application can reference it.

Its acceptance criteria are primarily dependency and build checks:

- no references to app, rendering, or spatial projects,
- another host can reference the library,
- builds and tests pass,
- no behavior change.

Those checks are necessary but insufficient. A component can satisfy all four and still have a poor public API.

At current HEAD:

- `ICanvasSceneSource` and `ICanvasSpatialQuerySource` both expose `QueryVisible`.
- `CanvasFrame.Items` is a third visible-item source.
- both source dependency properties are nullable and have no lifecycle callback.
- `CanvasControl` publicly exposes `Border`, `Viewbox`, `TextBlock`, `ProgressBar`, and overlay `Canvas` instances.
- host code still mutates control-owned overlay layers.
- frame lists are borrowed through `IReadOnlyList<T>` rather than immutable snapshots.
- timer, mouse capture, and global cursor cleanup are not expressed as a reusable lifecycle contract.

A project extraction at this point would institutionalize these seams.

---

## 3. Proposed Next Slice: `ICW-316A`

## Title

**Harden reusable canvas contracts and lifecycle before assembly extraction**

## Objective

Make the existing in-app canvas boundary semantically reusable while keeping all files in their current projects. The slice should not move projects or namespaces yet.

## Scope

### 3.1 Establish one authoritative item-query contract

Choose one of these designs:

**Recommended:**

```csharp
public interface ICanvasSceneSource : ICanvasSpatialQuerySource
{
    SpatialBounds SceneBounds { get; }
    int TotalItemCount { get; }
    bool TryReadResidentPixel(
        double worldX,
        double worldY,
        int mipLevel,
        out CanvasPixelSample sample);

    event EventHandler<CanvasSceneChangedEventArgs>? SceneChanged;
}
```

Then remove `SpatialQuerySource` from `CanvasControl`.

Alternative: remove `QueryVisible` from `ICanvasSceneSource` and make a single `SpatialQuerySource` mandatory. Do not retain duplicate methods on sibling injected services.

### 3.2 Define frame snapshot semantics

Add:

- `CanvasFrameId` or `SceneRevision`,
- immutable item storage,
- count relationship validation,
- viewport validity checks,
- raster freeze/thread-affinity validation,
- raster pixel-size validation.

Suggested shape:

```csharp
public sealed record CanvasFrame
{
    public required CanvasFrameId Id { get; init; }
    public required BitmapSource Raster { get; init; }
    public required ImmutableArray<ICanvasItem> Items { get; init; }
    public required SpatialBounds Viewport { get; init; }
    public required CanvasPixelSize PixelSize { get; init; }
    public required int TotalItemCount { get; init; }
}
```

Use a validating factory if required members alone cannot enforce invariants.

### 3.3 Replace public visual-tree access with semantic APIs

Remove or internalize:

- `SurfaceHost`
- `FrameHost`
- `LoadingText`
- `WorldReadout`
- `TileReadout`
- `ValueReadout`
- `BusyBar`
- `TileGridLayer`
- `AnnotationLayer`

Replace them with state and publication APIs:

```csharp
CanvasStatus State
CanvasPixelReadout PixelReadout
CanvasOverlayFrame OverlayFrame
bool IsBusy
bool IsLoading
```

These may be dependency properties, immutable state records, or focused methods. The host must no longer manipulate named WPF elements.

### 3.4 Define source replacement lifecycle

When `SceneSource` changes:

- unsubscribe from the old source,
- subscribe to the new source,
- reset stale frame/item state,
- apply new scene bounds,
- define whether a render request is raised,
- reject use after unload if applicable.

Use a dependency-property callback if dependency-property injection remains.

### 3.5 Harden interaction lifecycle

Add one idempotent cleanup path that:

- stops the anchor-pan timer,
- releases mouse capture,
- clears `Mouse.OverrideCursor`,
- clears drag and anchor state,
- unsubscribes source events,
- tolerates repeated unload calls.

Exercise it during:

- `Unloaded`,
- host/window closure,
- source replacement where needed,
- exceptional input termination.

### 3.6 Make overlay publication frame-consistent

The control should accept semantic overlay data tied to a frame revision. It should own WPF element realization and pooling.

Minimum transitional API:

```csharp
void PublishOverlays(CanvasFrameId frameId, CanvasOverlayFrame overlays);
```

Reject or ignore overlays for a stale frame ID.

A stronger design includes overlays in the same frame transaction.

---

## 4. Explicit Non-Scope for `ICW-316A`

Do not include:

- project creation,
- source-file moves,
- namespace churn,
- solution-file edits,
- NuGet packaging,
- full input-handler extraction from `ICW-313`,
- selection and tooltip implementation from `ICW-314`,
- tile-source extraction from `ICW-076`.

This keeps the slice reviewable and separates semantic changes from mechanical movement.

---

## 5. Acceptance Criteria for `ICW-316A`

1. `CanvasControl` exposes exactly one item-query source.
2. A published frame cannot be changed by mutating a caller-owned list.
3. Every published frame has an explicit identity or revision.
4. `VisibleItemCount`, frame item count, and total count cannot contradict one another.
5. Non-frozen or thread-invalid raster input is rejected or normalized according to a documented rule.
6. Replacing `SceneSource` unsubscribes the old source and initializes the new source deterministically.
7. `CanvasControl` exposes no public named visual elements or overlay canvases.
8. Host overlay publication is revision-checked.
9. Unload during anchor pan stops the timer, releases capture, and resets the cursor.
10. Existing no-flash and zero-copy invariants remain intact.
11. Core and Windows test suites pass.
12. App Release build passes.
13. `Validate-TaskTracker.ps1` passes.
14. ADR-0007 and `ICW-316` are updated to reflect the hardened boundary.

---

## 6. Required Tests

## Contract tests

- `CanvasSceneSource_IsSingleVisibleQueryAuthority`
- `CanvasControl_DoesNotExposeSpatialQuerySource`
- `CanvasFrame_CopiesOrOwnsItemsImmutably`
- `CanvasFrame_RejectsVisibleCountMismatch`
- `CanvasFrame_RejectsVisibleCountAboveTotal`
- `CanvasFrame_RejectsInvalidPixelSize`
- `CanvasFrame_RequiresFrozenRaster`
- `CanvasFrame_HasStableRevision`

## Lifecycle tests

- `ReplacingSceneSource_UnsubscribesPreviousSource`
- `ReplacingSceneSource_AppliesNewBounds`
- `UnloadDuringAnchorPan_StopsTimer`
- `UnloadDuringAnchorPan_ReleasesCapture`
- `UnloadDuringAnchorPan_ClearsOverrideCursor`
- `RepeatedUnload_IsSafe`

## Boundary tests

- `CanvasControl_PublicApi_ContainsNoNamedVisualElements`
- `CanvasControl_PublicApi_ContainsNoOverlayCanvas`
- `StaleOverlayFrame_IsRejected`
- `PublishedFrame_RemainsStableAfterCallerMutation`

## Regression tests

- persistent frame shell remains attached exactly once,
- raster publication remains zero-copy,
- pixelometer reads resident payloads only,
- hover does not queue generation,
- scrollbar and pan behavior remain unchanged.

---

## 7. Follow-On Slice: `ICW-316B`

After `ICW-316A` passes, execute the physical extraction.

## Scope

- create `InfiniteCanvas.Controls.Wpf`,
- move `CanvasControl`, XAML, `CanvasFrame`, WPF-specific state, and overlay realization,
- keep `CanvasViewModel` in `InfiniteCanvas.ViewModels` unless a stronger reason emerges,
- reference `InfiniteCanvas.Core` and `InfiniteCanvas.ViewModels`,
- ensure no app, rendering, or spatial references,
- update sample app references,
- update solution,
- update test paths,
- add a second-host integration project.

## Additional acceptance criteria

- the library compiles without application resources,
- no implicit dependency on `App.Current`,
- no reliance on sample-app resource keys,
- no `MainWindow` type/name references,
- no sample-domain item type references,
- a minimal host can instantiate the control, inject a fake scene source, publish a frame, pan, zoom, and unload it,
- library API review shows no accidental WPF implementation leakage beyond intentional control-level types.

---

## 8. Task Reconciliation

## `ICW-316`

**Correction required.** Expand and split. Current priority `P3` is questionable because this work is the active gate for the reusable-library direction. If assembly reuse is the immediate roadmap, raise it to `P1` or `P2`.

## `ICW-312`

Keep Done for the delivered implementation, but add a follow-up note that duplicate query authority is corrected by `ICW-316A`.

## `ICW-315`

Keep Done for the delivered frame boundary, but add follow-up hardening criteria for immutable items, revision identity, and validated raster dimensions.

## `ICW-313`

Do not pull full handler extraction into this slice. Do pull lifecycle cleanup and typed timing seams where required to make the control safe to unload.

## `ICW-314`

Do not start selection/tooltip ownership until the frame/source authority decision is complete. Otherwise it will encode the current split-brain design.

## `ICW-031`

Continue to sequence typed metrics before tooltip payload migration. Reuse its typed-value discipline for `CanvasPixelSize`, frame identity, and interaction units.

---

## 9. Implementation Sequence

1. Add failing contract tests for duplicate source authority and public visual exposure.
2. Consolidate source interfaces and remove `SpatialQuerySource` from the control.
3. Add frame ID, immutable items, and constructor/factory validation.
4. Add source-change callback and event subscription lifecycle.
5. Add unified interaction cleanup.
6. Introduce semantic status/readout APIs.
7. Introduce revision-checked overlay publication.
8. Migrate `MainWindow` away from visual-element access.
9. Update ADR, tickets, invariants, and handoff.
10. Run full validation.
11. Only then begin `ICW-316B`.

This order deliberately makes illegal states unrepresentable before moving files.

---

## 10. Risks and Mitigations

### Risk: Breaking no-flash behavior

**Mitigation:** Do not replace the persistent frame shell. Keep raster source swapping unchanged and preserve the existing shell wiring tests.

### Risk: Accidental raster copy

**Mitigation:** Keep the frozen `ImageSource`/`BitmapSource` reference as the frame payload. Immutability hardening should copy only the item collection, not the raster pixels.

### Risk: Overlay flicker during API migration

**Mitigation:** Introduce revision-checked overlay publication before removing direct canvas access. Migrate host code in one atomic commit.

### Risk: Designer support regression

**Mitigation:** Keep the parameterless constructor. Provide null-object or design-time source behavior rather than passive nullable properties with undefined semantics.

### Risk: Scope expansion into `ICW-313/314`

**Mitigation:** Only take lifecycle seams needed for safe extraction. Defer behavior decomposition and selection ownership.

---

## 11. Definition of Done

`ICW-316A` is complete when the current application hosts a canvas that already behaves like a reusable component even though it has not moved assemblies yet.

A reviewer should be able to answer all of these with one clear authority:

- Where do visible items come from?
- Which scene revision does the frame represent?
- Can a caller mutate a published frame?
- Who owns overlay realization?
- What happens when the source changes?
- What happens when the control unloads mid-interaction?
- Can a host use the component without touching its visual tree?

Until those answers are explicit and enforced, physical extraction is premature.

---

## 12. Final Recommendation

**Next slice:** `ICW-316A — Harden reusable canvas contracts and lifecycle before assembly extraction`

**Do not start with:** moving `CanvasControl.xaml` into a new project.

The repository has made good progress toward a reusable canvas, but the next high-value action is to reduce ambiguity at the boundary. Once the semantic API is narrow, immutable, lifecycle-safe, and frame-consistent, `ICW-316B` becomes a low-risk mechanical extraction instead of an architectural gamble.

---

## 13. Audit Limitations

- No implementation newer than `84a0cdb5` existed at audit time.
- This is a plan/readiness audit, not a code-delta audit.
- Repository-provided build and test claims were not independently rerun.
- The prior exhaustive report `ICW-AUD-F2D7D66DDA5B` is the baseline; findings were not repeated except where they directly control the next slice.
