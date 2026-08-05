# InfiniteCanvasWPF — Exhaustive Deep-Dive Architecture and Code Audit

**Report Hash ID:** `ICW-AUD-F2D7D66DDA5B`  
**Audit timestamp:** 2026-08-04 17:17:42 America/Chicago  
**Repository:** `TheMasonX/InfiniteCanvasWPF`  
**Commit audited:** `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`  
**Commit subject:** `feat(canvas): inject canvas data sources and migrate to CanvasFrame boundary`  
**Audit type:** Whole-repository architectural audit with commit-focused verification  
**Primary task cross-reference:** `ICW-312`, `ICW-313`, `ICW-314`, `ICW-315`, `ICW-316`, `ICW-031`, `ICW-P0-PIXELOMETER-READOUT`  

---

## 1. Executive Summary

Commit `84a0cdb5` is directionally correct. It removes a concrete pixelometer side effect, replaces the invalid `PublishFrame(UIElement)` boundary with a value object, and advances the canvas toward extraction from the sample application. The changes also preserve the no-flash frame shell and zero-copy raster handoff.

However, the new boundary is not yet clean enough to treat `ICW-312` and `ICW-315` as architecturally complete without follow-up corrections. The largest problem is that the new contracts create **two injectable authorities for the same visible-item query**:

- `ICanvasSceneSource.QueryVisible(SpatialBounds)`
- `ICanvasSpatialQuerySource.QueryVisible(SpatialBounds)`

`CanvasControl` exposes both as nullable dependency properties, while the frame itself separately carries an item list. That leaves three plausible sources of visible items: scene source, spatial query source, and `CanvasFrame.Items`. The system currently relies on convention rather than a single enforceable ownership rule.

The extraction plan in `ICW-316` should therefore not be implemented as a mechanical project move. It should first consolidate the boundary, eliminate host-facing control internals, define lifecycle and thread-affinity contracts, and decide whether published frames are immutable snapshots or merely bags of borrowed references.

### Findings summary

| ID | Severity | Confidence | Finding | Existing task action |
|---|---:|---:|---|---|
| F-01 | High | 98% | Duplicate visible-query capability creates two authorities | Correct `ICW-312`; gate `ICW-316` |
| F-02 | High | 94% | Three competing item-delivery paths create an implicit consistency contract | Extend `ICW-315/314` |
| F-03 | High | 92% | `CanvasFrame` claims snapshot semantics but does not enforce immutability | Extend `ICW-315` |
| F-04 | Medium | 96% | `CanvasControl` exposes WPF internals, so the abstraction remains shallow | Expand `ICW-316` |
| F-05 | Medium | 90% | Data-source dependency properties have no change/lifecycle behavior | Correct `ICW-312` |
| F-06 | Medium | 88% | `SceneChanged` is specified but not owned by the control boundary | Clarify `ICW-312/314` |
| F-07 | Medium | 87% | Timer, cursor, and mouse-capture lifecycle is not encapsulated | Extend `ICW-313/316` |
| F-08 | Medium | 93% | Frame invariants are incomplete and permit contradictory counts/items | Extend `ICW-315` |
| F-09 | Medium | 85% | Raster dimensions duplicate `ImageSource` metadata without a declared authority | Extend `ICW-315` |
| F-10 | Medium | 91% | Primitive-heavy pan/scroll configuration obscures policy and units | Extend `ICW-313` |
| F-11 | Medium | 86% | Host-composed overlay mutation crosses the frame publication boundary | Expand `ICW-314/316` |
| F-12 | Low | 95% | Public aliases expose named template parts and freeze implementation details | Expand `ICW-316` |
| F-13 | Low | 82% | `CanvasFrame` is a manual DTO where a validated immutable value is preferable | Refactor under `ICW-315` |
| F-14 | Low | 89% | Completed-ticket wording overstates separation achieved by the current code | Correct tracker evidence |

---

## 2. Scope and Method

This audit examined the pinned commit, its changed contracts and handoff documentation, the surrounding control/view-model/rendering boundaries, existing repository audits, task tickets, functional invariants, and the current extraction sequence.

The review deliberately avoided opening duplicate work. Each finding is classified as one of:

- a correction to an existing task,
- an acceptance-criteria extension,
- a sequencing constraint,
- or a genuinely separate concern.

Static verification was performed through the GitHub repository connector. The repository could not be cloned in the execution environment because outbound DNS access was unavailable. Therefore, no independent local build, test run, Roslyn analysis, or duplication tool was executed. The commit records `170/170` core tests, `18/18` Windows tests, and a zero-error Release build; this audit treats those as repository-provided evidence, not independently reproduced results.

---

## 3. Detailed Findings

## F-01 — Duplicate Query Capability Creates Two Injectable Authorities

**Severity:** High  
**Confidence:** 98%  
**Files:**

- `src/InfiniteCanvas.Core/ICanvasSceneSource.cs`
- `src/InfiniteCanvas.Core/ICanvasSpatialQuerySource.cs`
- `src/InfiniteCanvas.App/Controls/CanvasControl.xaml.cs`

`ICanvasSceneSource` includes:

```csharp
IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport);
```

`ICanvasSpatialQuerySource` independently defines the same method:

```csharp
IReadOnlyList<ICanvasItem> QueryVisible(SpatialBounds viewport);
```

`CanvasControl` then exposes both `SceneSource` and `SpatialQuerySource` dependency properties.

This is not useful interface segregation. It is duplicated authority. A host can provide:

1. one object for both properties,
2. two objects with different results,
3. only one property,
4. neither property.

The type system cannot express which one is authoritative or require agreement.

### Why this matters

The design creates a split-brain boundary exactly where the extraction is supposed to remove ambiguity. Any future selection, tooltip, keyboard navigation, accessibility, or hit-testing feature can accidentally query a different source from the one that produced the frame.

### Required correction

Amend `ICW-312` before physical extraction:

**Preferred design:**

```csharp
public interface ICanvasSceneSource : ICanvasSpatialQuerySource
{
    SpatialBounds SceneBounds { get; }
    int TotalItemCount { get; }
    bool TryReadResidentPixel(...);
    event EventHandler<CanvasSceneChangedEventArgs>? SceneChanged;
}
```

Then expose only `SceneSource` from `CanvasControl`.

If separate injection is genuinely required, remove `QueryVisible` from `ICanvasSceneSource` and document that `SpatialQuerySource` is mandatory. Do not keep the same operation on both contracts.

### Task disposition

- **Correct `ICW-312`**: its “Done” state should include contract consolidation.
- **Gate `ICW-316`**: do not fossilize the duplication in a reusable assembly.

---

## F-02 — Three Competing Item-Delivery Paths Form an Implicit Consistency Contract

**Severity:** High  
**Confidence:** 94%

Visible items can now arrive through:

1. `ICanvasSceneSource.QueryVisible`,
2. `ICanvasSpatialQuerySource.QueryVisible`,
3. `CanvasFrame.Items`.

`CanvasControl.PublishFrame` applies `CanvasFrame.Items` to `CanvasViewModel`, while the control also carries two query services for future behavior. Nothing states that the services and frame represent the same scene version, viewport, or epoch.

### Failure modes

- A scene regenerates after frame rendering but before tooltip querying.
- `Frame.Items` represents epoch N while `SceneSource` serves epoch N+1.
- A host injects a filtered spatial source but publishes unfiltered items.
- Counts in the frame come from one source while item instances come from another.

These are not theoretical once selection and tooltip ownership move under `ICW-314`.

### Required correction

Choose one explicit model:

**Snapshot model:** `CanvasFrame` is the complete interaction snapshot. Hit testing and tooltip behavior use only frame-owned data or a frame-owned query snapshot.

**Live-source model:** the frame carries raster/camera metadata only, and the control queries a versioned source using a scene/frame revision token.

The current hybrid model should not survive `ICW-314`.

### Task disposition

Extend `ICW-315` and `ICW-314` with a “single item authority per published frame” acceptance criterion.

---

## F-03 — `CanvasFrame` Claims Snapshot Semantics but Does Not Enforce Immutability

**Severity:** High  
**Confidence:** 92%

The documentation describes `CanvasFrame` as a value carrying one published frame. Yet:

```csharp
public IReadOnlyList<ICanvasItem> Items { get; }
public ImageSource Raster { get; }
```

`IReadOnlyList<T>` prevents mutation through that interface, but it does not prevent the host from retaining and mutating the underlying list. `ICanvasItem` itself may also expose mutable state. `ImageSource` can be mutable unless frozen, and the constructor does not enforce `IsFrozen`.

### Consequence

A “published frame” can change after publication without a new `PublishFrame` call. That breaks deterministic rendering and makes event ordering, tests, and future background-thread publication difficult to reason about.

### Required correction

At minimum:

- require or defensively copy items into an immutable collection,
- document whether item instances are immutable snapshots,
- verify `Raster.IsFrozen` or freeze a clonable raster,
- add a frame/scene revision identifier,
- add tests proving host mutation after publication cannot alter the control’s frame state.

### Task disposition

Extend `ICW-315`; this is a correction to the frame boundary, not a new task.

---

## F-04 — `CanvasControl` Still Exposes WPF Internals, So the Module Is Shallow

**Severity:** Medium  
**Confidence:** 96%

The control publicly exposes:

```csharp
public Border SurfaceHost => ViewportHost;
public Viewbox FrameHost => FramePresenter;
public TextBlock LoadingText => LoadingOverlay;
public TextBlock WorldReadout => PixelometerWorldText;
public TextBlock TileReadout => PixelometerTileText;
public TextBlock ValueReadout => PixelometerValueText;
public ProgressBar BusyBar => RenderBusyBar;
public Canvas? TileGridLayer => _tileGridLayer;
public Canvas? AnnotationLayer => _annotationLayer;
```

This is a classic shallow module: callers must understand and manipulate its internal visual structure. Moving this class into a library would relocate the coupling, not remove it.

### Required correction

Replace visual-element exposure with semantic APIs:

- `SetLoadingState(CanvasLoadingState state)`
- `SetPixelReadout(CanvasPixelReadout readout)`
- `SetBusy(bool isBusy)` or bindable state
- `PublishOverlays(CanvasOverlayFrame overlays)` or an overlay renderer service
- `ViewportSize` rather than `SurfaceHost.ActualWidth/Height`
- template-part contracts only where custom styling truly requires them

### Task disposition

Expand `ICW-316` acceptance criteria. “Another app can reference the assembly” is insufficient; another app should not need access to template internals.

---

## F-05 — Source Dependency Properties Have No Change or Lifecycle Semantics

**Severity:** Medium  
**Confidence:** 90%

Both source dependency properties use:

```csharp
new PropertyMetadata(null)
```

There is no property-changed callback to:

- unsubscribe from the old source,
- subscribe to the new source,
- apply scene bounds,
- invalidate visible items,
- clear stale frame state,
- trigger re-rendering,
- validate related source compatibility.

The properties therefore act as nullable storage slots rather than real dependency boundaries.

### Required correction

Add callbacks and define replacement semantics. If source replacement is unsupported after load, enforce that explicitly with constructor injection in the extracted library plus a designer-safe factory or design-time source.

### Task disposition

Correct `ICW-312`.

---

## F-06 — `SceneChanged` Exists Without a Clear Owner

**Severity:** Medium  
**Confidence:** 88%

`ICanvasSceneSource` declares `SceneChanged`, but the control does not subscribe to it. The commit handoff says `MainWindow` raises it after regeneration, yet the host still controls rendering and frame publication.

This creates an event with no clear architectural consumer:

- If the host owns refresh, the event does not belong in the control-facing contract.
- If the control owns refresh, it must subscribe, unsubscribe, coalesce changes, and define threading.
- If external consumers own refresh, the event needs versioned event arguments.

### Required correction

Specify the event’s consumer and behavior. Prefer typed event data:

```csharp
event EventHandler<CanvasSceneChangedEventArgs>? SceneChanged;
```

Include revision, change kind, and optional dirty bounds. An untyped event forces full invalidation and hides ordering.

### Task disposition

Clarify `ICW-312`; coordinate with `ICW-314`.

---

## F-07 — Timer, Cursor, and Mouse Capture Lifecycle Is Not Encapsulated

**Severity:** Medium  
**Confidence:** 87%

`CanvasControl` owns a `DispatcherTimer`, global `Mouse.OverrideCursor`, and mouse capture. `DetachFrameShell` only detaches visual frame state. The inspected control code does not show a unified unload/dispose path that always:

- stops `_anchorPanTimer`,
- releases mouse capture,
- clears `Mouse.OverrideCursor`,
- resets drag fields,
- unsubscribes source events,
- prevents ticks after unload.

A reusable control must survive unload/reload, window closure during anchor pan, visual-tree moves, and source replacement.

### Required correction

Add deterministic lifecycle handling on `Loaded`/`Unloaded` or an internal disposable interaction session. Never rely solely on matching mouse-up events.

### Task disposition

Extend `ICW-313` and make it a gate for `ICW-316`.

---

## F-08 — Frame Invariants Permit Contradictory Counts and Items

**Severity:** Medium  
**Confidence:** 93%

`CanvasFrame` validates only non-negative counts and positive dimensions. It permits:

- `VisibleItemCount > TotalItemCount`,
- `VisibleItemCount != Items.Count`,
- `Items.Count > TotalItemCount`,
- viewport values that are invalid or non-finite,
- dimensions inconsistent with the raster.

This weakens the value object into a bag of primitives.

### Required correction

Enforce:

```text
0 <= Items.Count == VisibleItemCount <= TotalItemCount
```

unless there is a documented reason counts differ. If counts are intentionally independent, rename them to reflect what they count and document the difference.

Validate viewport finiteness and positive extent through `SpatialBounds`.

### Task disposition

Extend `ICW-315`.

---

## F-09 — Raster Dimensions Duplicate `ImageSource` Metadata Without Declaring Authority

**Severity:** Medium  
**Confidence:** 85%

`CanvasFrame` carries `Raster`, `Width`, and `Height`. The constructor does not confirm that dimensions match the raster’s pixel dimensions.

For `BitmapSource`, `PixelWidth` and `PixelHeight` exist. For other `ImageSource` implementations, width semantics differ. The control applies frame width/height to the shell and stretches the raster to fit, so a mismatch can silently scale or distort the frame.

### Required correction

Use a typed raster abstraction, likely `BitmapSource`, if the pipeline requires pixels. Otherwise introduce:

```csharp
public readonly record struct CanvasPixelSize(int Width, int Height);
```

and validate the raster/size relationship according to one documented rule.

### Task disposition

Extend `ICW-315`; no separate ticket needed.

---

## F-10 — Primitive-Heavy Interaction Configuration Hides Policy and Units

**Severity:** Medium  
**Confidence:** 91%

The control embeds:

```csharp
_mouseWheelZoomDelta = 1.2
_panExponent = 1.8
_panDeadZone = 1
_panScale = 0.1
_panGain = 0.075
16 ms timer interval
10, 20, 24 pixel scrollbar layout constants
```

These values are not grouped, named by units, configurable, or linked to input-device behavior. The interaction logic combines policy, state, timing, and visual geometry in one class.

### Required correction

Create typed policies:

```csharp
CanvasZoomPolicy
AnchorPanOptions
CanvasScrollbarLayoutMetrics
CanvasInteractionClock
```

Use unit-bearing names such as `DeadZoneDevicePixels`, `TickInterval`, and `ZoomFactorPerWheelNotch`. Inject a clock/tick source for deterministic tests.

### Task disposition

Extend `ICW-313`. This is exactly the abstraction work that task should perform, not a new parallel ticket.

---

## F-11 — Overlay Mutation Crosses the Frame Publication Boundary

**Severity:** Medium  
**Confidence:** 86%

The host retains overlay composition responsibility and mutates `TileGridLayer` and `AnnotationLayer` exposed by the control. `PublishFrame` publishes raster and frame state, raises `FramePublished`, and then the host composes overlays against the published camera snapshot.

This creates a multi-step publication protocol:

1. publish raster,
2. update view model,
3. raise event,
4. host mutates overlay canvases.

The raster and overlays are not atomic as one visual frame. A failure or reentrant render between steps can produce mixed epochs.

### Required correction

Publish one composed semantic frame transaction:

```csharp
CanvasFrame
  Raster
  Viewport
  Items
  OverlayPrimitives / OverlaySnapshot
  Revision
```

The control should own realization/pooling of overlay visuals. This also supports `ICW-007` pooling without exposing canvases.

### Task disposition

Expand `ICW-314` and `ICW-316`; preserve `ICW-315`’s zero-copy raster requirement.

---

## F-12 — Public Template-Part Aliases Freeze Implementation Details

**Severity:** Low  
**Confidence:** 95%

Properties such as `FrameHost`, `SurfaceHost`, and readout text blocks make XAML element names and control types part of the public API. Any future template refactor becomes a breaking change.

### Required correction

Make template parts private and expose semantic state through dependency properties, commands, events, and immutable snapshots.

### Task disposition

Expand `ICW-316`.

---

## F-13 — `CanvasFrame` Should Be a Strong Value, Not a Manual DTO

**Severity:** Low  
**Confidence:** 82%

The class is declared `sealed`, but it has no value equality, revision, deconstruction, or immutable-collection enforcement. The code is verbose while still under-validating relationships.

A `sealed record` or readonly record-like design would better communicate frame identity, provided equality does not accidentally compare expensive raster content. A custom `FrameId`/`Revision` is likely more meaningful than structural raster equality.

### Required correction

Introduce explicit identity:

```csharp
public readonly record struct CanvasFrameId(long Value);
```

Use a validated factory or constructor and immutable item collection.

### Task disposition

Refactor within `ICW-315`.

---

## F-14 — Tracker Completion Language Overstates the Boundary Achieved

**Severity:** Low  
**Confidence:** 89%

The tracker marks `ICW-312` and `ICW-315` Done and describes a reusable data-source/frame boundary. Functionally, the commit does land the intended slice. Architecturally, the duplicate query contracts, nullable passive injection, exposed internals, and host-owned overlays mean the boundary remains transitional.

This is not an argument to reopen all implementation work. It is an argument to prevent “Done” from being interpreted as “safe to extract unchanged.”

### Required correction

Add explicit follow-up notes to the completed tasks and place contract-consolidation gates in `ICW-316`.

---

## 4. Existing Task Reconciliation

## `ICW-312` — Canvas Data-Source Abstraction

**Status recommendation:** Keep implementation complete, add a correction amendment.

Add acceptance criteria:

- exactly one authoritative visible-item query contract,
- source replacement semantics are defined,
- `SceneChanged` has typed data and a named owner,
- source events are subscribed/unsubscribed safely,
- frame/source revision consistency is defined,
- null injection behavior is explicit.

## `ICW-315` — CanvasFrame Boundary

**Status recommendation:** Keep delivered slice complete, add hardening follow-up criteria.

Add:

- immutable frame semantics,
- raster frozen/thread-affinity validation,
- item/count consistency,
- raster dimension consistency,
- frame revision,
- atomic overlay/frame publication or a documented temporary exception.

## `ICW-316` — Assembly Extraction

**Status recommendation:** Expand before implementation.

Do not perform a directory/project move only. Required extraction gates:

1. no public access to internal WPF elements,
2. no duplicate source authority,
3. no application-specific namespace dependency,
4. deterministic unload/reload lifecycle,
5. semantic overlay API,
6. sample app becomes a consumer rather than a privileged friend,
7. one minimal second-host integration test.

## `ICW-313` — Input Handler Abstraction

Extend it to own:

- typed interaction options,
- timer/clock abstraction,
- capture/cursor cleanup,
- device-independent units,
- reentrancy policy,
- unload cancellation.

## `ICW-314` — Selection and Tooltip Ownership

Before implementation, choose snapshot vs. live-source semantics. Selection and tooltip must not query a different scene epoch from the displayed frame.

## `ICW-031` — Typed Metrics

Continue sequencing this before tooltip migration. Also use its type discipline as a model for frame dimensions, interaction units, and pixel samples.

## `ICW-P0-PIXELOMETER-READOUT`

The commit appears to close the specific side effect: hover reads resident payloads only. Preserve this invariant with a test proving repeated reads never invoke generation, queue work, or change cache reservations.

---

## 5. Consolidation Opportunities

### 5.1 Consolidate scene/query contracts

Replace the duplicated pair with one coherent source hierarchy.

### 5.2 Consolidate frame publication

Raster, item snapshot, viewport, counts, and overlay state should share a revision and publication transaction.

### 5.3 Consolidate UI status surface

Loading, busy state, pixel readout, and errors should be control state, not external mutation of text blocks and progress bars.

### 5.4 Consolidate interaction configuration

Move wheel, pan, anchor, timer, and scrollbar constants into named option/value types.

### 5.5 Consolidate lifecycle cleanup

One interaction-session cleanup method should stop timers, release capture, clear cursors, and reset transient state.

---

## 6. Legacy Exit Strategy

The cleanest route out of the current application-bound design is incremental strangulation, not a big-bang rewrite:

1. **Harden contracts in place.** Resolve duplicate authorities and snapshot semantics while behavior remains covered by current tests.
2. **Create the library boundary.** Move only code that has a semantic API; leave host-specific overlay/render orchestration behind temporarily.
3. **Build a second minimal host.** A tiny test host should prove the control is not relying on `MainWindow`, named internal elements, or sample-domain types.
4. **Move overlay realization into the control.** Keep overlay data abstract and immutable.
5. **Move selection/tooltip against frame-consistent item snapshots.**
6. **Extract input handlers only after lifecycle and policy types are defined.**
7. **Delete compatibility aliases immediately after migration.** Do not preserve `SurfaceHost`, `FrameHost`, or public layer canvases as “temporary” legacy APIs.

This avoids creating a new reusable assembly that merely packages the old coupling.

---

## 7. Recommended Priority Order

### P0 — Before `ICW-316`

1. Resolve F-01 duplicate query authority.
2. Decide F-02 snapshot vs. live-source model.
3. Add F-03 immutable/versioned frame semantics.
4. Define F-05/F-06 source lifecycle.
5. Add F-07 unload/capture/timer cleanup.

### P1 — During `ICW-316`

6. Replace exposed visual internals (F-04/F-12).
7. Move overlay realization behind a semantic API (F-11).
8. Enforce frame invariants and pixel-size types (F-08/F-09).

### P2 — During `ICW-313/314`

9. Extract typed interaction policies (F-10).
10. Implement frame-consistent selection and tooltip behavior.
11. Add a second-host integration fixture.

---

## 8. Suggested Tests

1. Inject different `SceneSource` and `SpatialQuerySource` results; the final design should make this impossible.
2. Mutate the original item list after `PublishFrame`; published frame contents must not change.
3. Publish a non-frozen raster from a worker thread; enforce or reject according to contract.
4. Replace a scene source; old event handlers must not fire.
5. Unload during anchor pan; timer stops, capture releases, cursor resets.
6. Publish `VisibleItemCount != Items.Count`; constructor/factory rejects it.
7. Publish raster dimensions inconsistent with frame size; constructor/factory rejects or explicitly normalizes.
8. Trigger scene change between render and tooltip query; tooltip must remain on the displayed frame revision.
9. Verify the sample host never accesses named template elements or overlay canvases.
10. Verify resident pixel reads do not queue or generate tile work.

---

## 9. Positive Findings

- The commit removes a real hover-triggered generation side effect.
- `CanvasFrame` is materially better than passing a `UIElement`.
- The persistent shell remains owned by the control, preserving no-flash behavior.
- The view-model project sheds an unnecessary spatial project reference.
- The handoff correctly identifies physical extraction as a separate slice.
- The task tracker was updated atomically with the implementation.
- Existing tests explicitly guard the frame shell wiring and boundary references.

These are strong steps. The report’s concern is not that the direction is wrong, but that the transitional seams should be tightened before they become a public reusable API.

---

## 10. Assumptions and Limitations

1. Static review only; build and test results were not independently reproduced.
2. GitHub connector output was used because direct repository cloning was unavailable in the execution environment.
3. Existing audit documents were treated as prior-art baselines and not repeated unless the new commit changed the status.
4. Findings about runtime event ordering and WPF thread affinity are based on the visible contracts and inspected source, not runtime instrumentation.
5. Confidence values reflect source clarity, not production incidence probability.

---

## 11. Final Assessment

**Architecture trajectory:** Good  
**Current reusable-boundary maturity:** Transitional  
**Risk of mechanically implementing `ICW-316` now:** High  
**Recommended decision:** Proceed with extraction only after the P0 contract hardening above.

The commit solves useful concrete problems, but the new abstractions currently expose more choices than the system can safely support. The next improvement should reduce authority, reduce public surface area, and make frame consistency enforceable rather than conventional.
