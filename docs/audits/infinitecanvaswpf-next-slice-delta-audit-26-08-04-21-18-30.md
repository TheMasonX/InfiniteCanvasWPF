# InfiniteCanvasWPF — Next-Slice Delta Audit, Pass 2

**Report Hash ID:** `ICW-DELTA-5C7F428818E1`  
**Audit timestamp:** 2026-08-04 21:18:30 America/Chicago  
**Repository:** `TheMasonX/InfiniteCanvasWPF`  
**HEAD verified:** `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`  
**Baseline reports:** `ICW-AUD-F2D7D66DDA5B`, `ICW-NEXT-F6F854D63C32`  
**Scope:** Additional findings and corrections for the proposed `ICW-316A` contract-hardening slice  
**Delta policy:** Prior findings are not repeated unless materially extended.

---

## 1. Executive Summary

The repository has not advanced beyond `84a0cdb5`, so this pass continues the static next-slice audit rather than reviewing a new code delta.

The prior report correctly identified the duplicate scene-query authorities, weak frame snapshot semantics, exposed WPF internals, and missing lifecycle ownership. This continuation found a second layer of issues concentrated in `ICanvasItem`, `CanvasViewModel`, and foundational geometry semantics.

The most important extension is that hardening `CanvasFrame` alone is insufficient. `CanvasViewModel` currently republishes the same weak state as independently settable mutable properties:

- `Viewport`
- `VisibleItemCount`
- `TotalItemCount`
- `VisibleItems`
- `SceneBounds`

Even if `CanvasFrame` becomes validated and immutable, any caller can place the view model back into an impossible state by setting those properties separately. The next slice must therefore introduce an atomic frame-state value inside the view model or restrict setters.

### New findings

| ID | Severity | Confidence | Finding | Task action |
|---|---:|---:|---|---|
| D2-01 | High | 98% | `CanvasViewModel` exposes independently mutable frame state | Add to `ICW-316A` |
| D2-02 | High | 96% | `ApplyFrame` makes visible items optional, silently permitting count/list divergence | Correct `ICW-315/316A` |
| D2-03 | Medium | 95% | `ICanvasItem.Id` is primitive string identity with no domain or uniqueness contract | Extend `ICW-314/316A` |
| D2-04 | Medium | 92% | Item identity and item instance lifetime are unspecified across scene revisions | Extend `ICW-314` |
| D2-05 | Medium | 94% | Scene and frame state publish through multiple property notifications, exposing torn observations | Add atomic state publication |
| D2-06 | Medium | 88% | `ComputeMinimumZoom` returns an unnamed tuple and has an unsafe public precondition | Extend typed metrics |
| D2-07 | Medium | 91% | Zoom-floor logic relies on exact hidden assumptions about positive finite camera scales | Strengthen `CameraTransform` contract |
| D2-08 | Medium | 90% | `SpatialBounds.Intersects` treats edge contact and zero-area bounds as intersection | Clarify geometry semantics |
| D2-09 | Low | 93% | `HasScene` is computed but notification ownership remains manual and easy to bypass | Restrict scene mutation |
| D2-10 | Low | 87% | Public methods combine commands and derived-state synchronization without one transaction boundary | Consolidate viewport state transition |

---

## 2. D2-01 — `CanvasViewModel` Can Be Put Into Impossible States

**Severity:** High  
**Confidence:** 98%

The view model exposes public setters for all frame-derived properties:

```csharp
public partial SpatialBounds Viewport { get; set; }
public partial int VisibleItemCount { get; set; }
public partial int TotalItemCount { get; set; }
public partial IReadOnlyList<ICanvasItem> VisibleItems { get; set; } = [];
```

This allows states such as:

- `VisibleItemCount = -1`,
- `VisibleItemCount > TotalItemCount`,
- `VisibleItems.Count != VisibleItemCount`,
- `Viewport` from frame N with `VisibleItems` from frame N+1,
- `TotalItemCount = 0` with a non-empty visible item list.

Hardening the constructor of `CanvasFrame` will not solve this because the view model immediately decomposes that value into separately mutable properties.

### Required correction

Introduce an atomic state object:

```csharp
public sealed record CanvasFrameState(
    CanvasFrameId FrameId,
    SpatialBounds Viewport,
    ImmutableArray<ICanvasItem> VisibleItems,
    int TotalItemCount);
```

Expose:

```csharp
public CanvasFrameState FrameState { get; private set; }
```

Derive counts:

```csharp
public int VisibleItemCount => FrameState.VisibleItems.Length;
public int TotalItemCount => FrameState.TotalItemCount;
public SpatialBounds Viewport => FrameState.Viewport;
```

At minimum, make generated setters private and validate all state through one method.

### Acceptance criterion

No public API may independently assign viewport, visible items, or item counts.

---

## 3. D2-02 — Optional Visible Items Hide Contract Violations

**Severity:** High  
**Confidence:** 96%

`ApplyFrame` declares:

```csharp
public void ApplyFrame(
    SpatialBounds frameViewport,
    int frameVisibleItemCount,
    int frameTotalItemCount,
    IReadOnlyList<ICanvasItem>? frameVisibleItems = null)
```

If the caller omits `frameVisibleItems`, the view model records the provided count but assigns an empty list:

```csharp
VisibleItemCount = frameVisibleItemCount;
VisibleItems = frameVisibleItems ?? [];
```

The API explicitly creates contradictory state.

The comment says the list is optional so hosts can drive the view model from any source. That flexibility is harmful because it erases whether the count is authoritative, whether items are unavailable, or whether the caller forgot to pass them.

### Required correction

Choose one:

1. Visible items are part of the frame and therefore required.
2. Visible items are not part of the frame; remove them from `ApplyFrame` and query through one versioned source.
3. Represent absence explicitly with a discriminated state such as `ItemsNotMaterialized`, not an empty list.

Do not use null/default omission to mean both “no visible items” and “items were not supplied.”

---

## 4. D2-03 — String Item Identity Is Primitive Obsession

**Severity:** Medium  
**Confidence:** 95%

`ICanvasItem` defines:

```csharp
string Id { get; }
```

The contract does not define:

- whether IDs are unique globally or only within a scene,
- whether comparison is ordinal or case-insensitive,
- whether empty or whitespace IDs are valid,
- whether IDs are stable across regeneration,
- whether two item types may share an ID,
- whether IDs are user-facing or machine-only.

This becomes load-bearing when `ICW-314` moves selection and tooltip ownership into the control.

### Required correction

Use a typed identity:

```csharp
public readonly record struct CanvasItemId
{
    public CanvasItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
```

Document uniqueness scope as `(SceneRevision, CanvasItemId)` unless IDs are globally stable.

For heterogeneous item kinds, consider a namespaced identity or typed key rather than concatenated strings.

---

## 5. D2-04 — Selection Identity Across Revisions Is Undefined

**Severity:** Medium  
**Confidence:** 92%

The planned selection migration assumes item identity is stable, but the current contracts do not say what happens when:

- a scene regenerates and creates new item instances with the same ID,
- an item keeps its ID but changes bounds,
- an item disappears and later reappears,
- two source implementations reuse the same ID,
- the selected item belongs to an older frame revision.

### Required correction

`ICW-314` must specify:

- selection is stored by typed ID, not object reference,
- selection is scoped to a scene revision or reconciled on scene change,
- a missing selected ID clears selection deterministically,
- hit testing returns an item and revision pair,
- tooltip content is not reused after its frame becomes stale.

This must be designed before selection ownership moves.

---

## 6. D2-05 — Multi-Property Notification Produces Torn Frame Observations

**Severity:** Medium  
**Confidence:** 94%

`ApplyFrame` assigns four observable properties sequentially. Each assignment may raise `PropertyChanged`.

Subscribers can therefore observe intermediate combinations:

1. new viewport with old counts/items,
2. new visible count with old total/items,
3. new total with old items,
4. finally the complete state.

This is a transactional consistency problem even on one UI thread. Derived controls, accessibility consumers, status text, and tests may respond to partial state.

### Required correction

Publish one observable `FrameState` property, or suppress/batch notifications and raise one explicit `FrameChanged` event after all fields are committed.

The preferred design is a single immutable state value.

---

## 7. D2-06 — Minimum Zoom Is an Unnamed Tuple with Unsafe Preconditions

**Severity:** Medium  
**Confidence:** 88%

`ComputeMinimumZoom` returns:

```csharp
(double ScaleX, double ScaleY)
```

This is shallow typing for a domain concept. It also divides by scene width and height without guarding `HasScene`, even though the method is public.

`ApplyZoomFloor` checks `HasScene`, but external callers can call `ComputeMinimumZoom` directly with an empty scene.

### Required correction

Use a value type:

```csharp
public readonly record struct CanvasScale(double X, double Y);
```

Either:

- make `ComputeMinimumZoom` private,
- return `bool TryComputeMinimumZoom(...)`,
- or throw a clear exception for empty scene bounds.

Also validate viewport dimensions as finite and positive rather than relying on callers.

Tie this correction to `ICW-031`’s typed-metrics direction.

---

## 8. D2-07 — Zoom-Floor Logic Depends on Hidden Camera Invariants

**Severity:** Medium  
**Confidence:** 91%

`ApplyZoomFloor` performs divisions such as:

```csharp
minimumUniform / currentScaleX
minimumScaleX / currentScaleX
minimumScaleY / currentScaleY
```

This assumes current scales are finite and strictly positive. The code may currently preserve that invariant, but the view-model method does not state or enforce it.

A reusable boundary should not rely on a distant implementation detail remaining true forever.

### Required correction

- expose scale through a validated `CanvasScale`,
- assert or guard positive finite scale before division,
- keep all camera mutation behind methods that preserve the invariant,
- add property-based tests over extreme finite scene/viewport sizes.

---

## 9. D2-08 — `SpatialBounds.Intersects` Semantics Are Ambiguous

**Severity:** Medium  
**Confidence:** 90%

The method uses inclusive comparisons:

```csharp
X <= other.Right
&& Right >= other.X
&& Y <= other.Bottom
&& Bottom >= other.Y
```

Therefore:

- rectangles touching at one edge intersect,
- rectangles touching at one point intersect,
- zero-width or zero-height bounds can intersect,
- a zero-area item on a viewport boundary can be returned as visible.

That may be correct, but the method name does not communicate whether intersection includes boundary contact.

This matters for spatial queries, hit testing, viewport item counts, and selection behavior.

### Required correction

Define explicit methods if both meanings are needed:

```csharp
IntersectsInclusive
OverlapsArea
ContainsPoint
```

Add tests for:

- edge contact,
- corner contact,
- zero-area bounds,
- identical bounds,
- extremely large finite bounds.

Do not silently change current behavior without checking spatial-index parity.

---

## 10. D2-09 — `HasScene` Notification Is Manually Coupled to One Setter Method

**Severity:** Low  
**Confidence:** 93%

`HasScene` is derived from `SceneBounds`, but notification is manually raised only inside `SetSceneBounds`.

Because `SceneBounds` has a public setter, callers can assign it directly and bypass `HasScene` notification.

### Required correction

Make the setter private and use the generated partial property change hook, or make `HasScene` part of an immutable scene state.

This is a concrete example of why public generated setters weaken the module.

---

## 11. D2-10 — Viewport Commands and Synchronization Are Entangled

**Severity:** Low  
**Confidence:** 87%

Methods such as `Pan`, `Zoom`, `ResetCamera`, `ApplyViewportSize`, and `ApplyZoomFloor` mutate the camera and then separately synchronize `Viewport`.

The view model has no single transition primitive that guarantees camera and viewport remain paired.

### Required correction

Create a transition method that captures the camera snapshot and viewport atomically:

```csharp
private void CommitViewportTransition(
    Action<CameraTransform> mutation,
    CanvasViewportSize viewportSize)
```

Or return an immutable `CanvasViewportState` from a camera policy object.

This reduces duplicated “mutate, clamp, derive viewport, notify” pathways and supports deterministic tests.

---

## 12. Revised `ICW-316A` Scope

The prior report’s proposed slice should be extended with these requirements:

### Frame/view-model state

- replace independently mutable frame properties with one immutable state,
- remove optional visible-item materialization,
- publish one notification per frame,
- derive visible count from the item collection,
- add a typed frame ID.

### Item identity

- replace string IDs with `CanvasItemId`,
- define uniqueness and comparison,
- define selection reconciliation across scene revisions.

### Geometry and scale

- add typed viewport size and scale values,
- define inclusive versus positive-area intersection,
- enforce camera scale invariants at the API boundary.

### Lifecycle and API surface

Retain all requirements from `ICW-NEXT-F6F854D63C32`:

- one query authority,
- no public visual internals,
- deterministic source replacement,
- deterministic unload cleanup,
- revision-checked overlays.

---

## 13. Additional Acceptance Criteria

1. `CanvasViewModel` has no public setters for frame-derived state.
2. One state assignment produces one coherent frame notification.
3. Visible item count is derived from the materialized item collection.
4. “Items unavailable” cannot be represented as an empty list with a nonzero count.
5. `CanvasItemId` rejects null, empty, and whitespace values.
6. Item identity uniqueness scope is documented and tested.
7. Selection cannot survive onto an unrelated scene revision accidentally.
8. Public minimum-zoom APIs reject empty scenes and invalid viewport dimensions.
9. Camera scales are guaranteed finite and positive.
10. Geometry tests define edge and zero-area intersection behavior.
11. Assigning scene bounds always notifies `HasScene`.
12. Pan, zoom, reset, and resize converge through one viewport-state commit path.

---

## 14. Recommended Ticket Corrections

### `ICW-316`

Split as previously recommended. Add view-model atomicity and typed identity to `ICW-316A`.

### `ICW-315`

Add a follow-up note: frame hardening must continue through the view model. A validated frame that is decomposed into mutable independent properties does not preserve its invariants.

### `ICW-314`

Expand acceptance criteria around typed identity, scene revisions, stale selection, and stale tooltip payloads.

### `ICW-031`

Extend its typed-metrics approach to:

- `CanvasPixelSize`,
- `CanvasViewportSize`,
- `CanvasScale`,
- `CanvasFrameId`,
- `CanvasItemId`.

This is not duplicate work; it applies the same design discipline to newly introduced boundary primitives.

---

## 15. Priority

### P0 within the next slice

1. Atomic view-model frame state.
2. Remove optional visible items.
3. Typed frame and item identity.
4. Single query authority.
5. Source and interaction lifecycle cleanup.

### P1 before assembly extraction

6. Remove public WPF internals.
7. Revision-check overlays.
8. Define geometry intersection semantics.
9. Introduce typed viewport/scale values.

### P2 before selection migration

10. Selection reconciliation rules.
11. Typed tooltip payload and stale-frame handling.
12. Heterogeneous item identity policy.

---

## 16. Final Assessment

The next slice needs one additional design principle:

> **Do not validate state at one boundary and immediately decompose it into independently mutable primitives.**

The current `CanvasFrame` and `CanvasViewModel` combination does exactly that. The reusable component should carry a coherent frame state from publication through observation, selection, tooltip behavior, and overlay realization.

With the corrections in this delta, `ICW-316A` becomes a proper semantic stabilization slice rather than a narrow interface cleanup.

---

## 17. Limitations

- HEAD remains unchanged; this is a same-commit delta audit.
- No local build or tests were executed.
- The report focuses on newly identified issues and deliberately avoids repeating the full prior findings.
