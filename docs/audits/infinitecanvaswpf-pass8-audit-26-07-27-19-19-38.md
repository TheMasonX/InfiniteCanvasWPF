# InfiniteCanvasWPF — Audit Pass 8 (Same HEAD, Geometry & Policy Primitives)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since passes 6–7; verified before writing).
**Scope this pass:** full reads of `LiveSpatialIndexService.cs`, `CameraTransform.cs`, `ViewportZoomPolicy.cs`, `TileGridIndexLookup.cs`, `SpatialBounds.cs` — the geometry/policy primitives underneath everything audited in passes 5–7, none of which had a dedicated full read yet in this series.

One new, concrete, unticketed correctness gap (§1); two low-severity hygiene notes (§2–3); one confirmed-clean result on a genuinely tricky piece of lock-free concurrency (§4).

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **`SpatialBounds` explicitly permits zero `Width`/`Height`** (its constructor rejects `width < 0` / `height < 0`, not `<= 0`), and `ZeroCopyBitmapFactory.Windows.cs`'s `DrawTile` divides by `tile.Bounds.Width`/`tile.Bounds.Height` with no zero-guard (lines 207, 215) to map screen pixels to source-image pixels. A tile constructed with a degenerate (zero-width or zero-height) `Bounds` would produce `NaN`/`Infinity` there, which then gets cast to `int` for a pixel-buffer index — an unspecified conversion in unchecked context, i.e. a potential out-of-bounds array access rather than a clean exception. Not covered by `ICW-308` (which only addresses `Intersects` inclusive/exclusive semantics) or `ICW-301` (camera scale invariants — a different type). | Medium | 80% |
| 2 | `TileGridIndexLookup.TryGetTileIndex`'s `(row * columns) + column` uses unchecked `int` arithmetic. For scene-bounds/tile-size combinations that push `row` very large, this can silently overflow and wrap into a small positive value that passes the `index < tileCount` guard — returning a **wrong-but-plausible** tile index instead of failing loudly. Low practical likelihood at this app's current scale (tens to low hundreds of tiles), but the failure mode (silent misindex) is worse than the failure mode of an exception, and the codebase already has precedent for guarding exactly this class of arithmetic (`checked` in `Bgra32BufferLayout` per `ICW-307`, and `BackgroundTilePayload`'s `checked(width * height)`). | Low | 70% |
| 3 | Two "do not touch" comments addressed to a specific person / defending against a specific past regression are committed in shared source (`CameraTransform.cs:5`, `ViewportZoomPolicy.cs:29`). Checked whether the logic they defend is actually pinned by a test: `ViewportZoomPolicy`'s is — `ComputeWheelDeltas_BothAxesClamped_ChoosesMaxUniformTargetOrFallsBack` would fail if the `||` were ever changed to `^`, so that comment is more belt-and-suspenders than a real gap. `CameraTransform`'s comment is pure rationale, not a regression warning, so it's fine as-is. Only genuine note: consider rephrasing the person-directed one impersonally so it reads the same to every future contributor, not just "Luke." | Trivial | 90% |
| 4 | **Confirmed clean:** `LiveSpatialIndexService<T>`'s lock-free, CAS-based state machine (hot/publishing/snapshot item partitioning, background index rebuild, failure-recovery merge) was traced end-to-end, including the failed-rebuild recovery path and concurrent `Add`/`AddRange` calls arriving during an in-flight `PublishSnapshotAsync`. No data loss, no duplication, no torn reads found — `Query` always observes one consistent `Volatile.Read` snapshot. This is genuinely careful concurrent code; recording it so it isn't re-suspected without new evidence in a future pass. | — (informational) | 85% |

---

## 1. [MEDIUM] Zero-size `SpatialBounds` is permitted by the type but not guarded at its one risky use site

**Confidence: 80%**

```csharp
// SpatialBounds.cs:17-25
if (!double.IsFinite(width) || width < 0 || !double.IsFinite(x + width))
{
    throw new ArgumentOutOfRangeException(nameof(width));
}
if (!double.IsFinite(height) || height < 0 || !double.IsFinite(y + height))
{
    throw new ArgumentOutOfRangeException(nameof(height));
}
```
Only strictly-negative width/height is rejected — `Width = 0` and `Height = 0` are valid constructions today, and nothing elsewhere in the type (`Right`, `Bottom`, `Intersects`) breaks on a zero-size bounds; a zero-size rectangle is a perfectly sensible mathematical object; this is not a bug in `SpatialBounds` itself.

The risk is at the one place a tile's `Bounds` gets used as a **divisor**:
```csharp
// ZeroCopyBitmapFactory.Windows.cs:205-216 (DrawTile)
var worldY = (y - camera.OffsetY) / camera.ScaleY;
...
(int)((worldY - tile.Bounds.Y) * sourceDimensions.Height / tile.Bounds.Height),
...
var worldX = (x - camera.OffsetX) / camera.ScaleX;
...
(int)((worldX - tile.Bounds.X) * sourceDimensions.Width / tile.Bounds.Width),
```
If `tile.Bounds.Height` or `.Width` is `0`, this produces `double.PositiveInfinity`/`NaN` (division by positive zero) or `NaN` (0/0 if the numerator is also 0), and the subsequent `(int)` cast of a non-finite double is an unspecified/implementation-defined conversion in an unchecked context in C# — it does not reliably throw, and does not reliably produce a value that a later bounds-check would catch. The value produced is then used as a pixel-array offset a few lines further down, so the realistic failure mode is an out-of-bounds read/write into the pixel buffer, not a clean, diagnosable exception.

I did not find a path in the current `SampleImageGenerator`/tile-layout code that actually constructs a zero-size tile under today's parameters — this is a **latent gap**, not a reproduced crash. It's worth closing regardless, because nothing about `SpatialBounds`'s own contract prevents a future caller (a new layout mode, a malformed config, a test fixture) from doing so, and the consequence if it happens is silent memory corruption rather than a clear failure.

**Recommendation:** pick one:
- Tighten `SpatialBounds`'s constructor to reject `width <= 0` / `height <= 0` if a zero-size spatial region is never actually meaningful anywhere in the domain (tiles, viewports, annotations all seem to assume positive extent) — simplest, and turns this into a loud constructor-time failure instead of a silent runtime one.
- Or, if zero-size bounds are legitimately meaningful somewhere (e.g., a degenerate/point annotation), leave the type as-is and add an explicit guard in `DrawTile` before the divisions (skip the tile / early-return if `tile.Bounds.Width <= 0 || tile.Bounds.Height <= 0`).
Either is a small change; the first is preferable unless there's a known caller that needs zero-size bounds.

---

## 2. [LOW] Unchecked tile-index arithmetic in `TileGridIndexLookup`

**Confidence: 70%**

```csharp
// TileGridIndexLookup.cs:47-51
var index = (row * columns) + column;
if (index < 0 || index >= tileCount)
{
    return false;
}
```
`row` and `columns` are plain `int`; `row = (int)((worldY - sceneBounds.Y) / tileHeight)` has no upper bound check before the multiply. For an extreme combination (very large scene height relative to `tileHeight`), `row * columns` can overflow `int32` and wrap to a small value — the `index < 0` check catches a wrap into negative, but not a wrap that lands back in `[0, tileCount)`, which would silently return a valid-looking but wrong `tileIndex`. Low likelihood at the scene/tile scales this app currently generates (a few hundred tiles at most), but the failure mode — wrong tile silently selected instead of a thrown exception — is exactly the kind of thing worth foreclosing cheaply.

**Recommendation:** wrap the multiply in `checked { }` (letting `OverflowException` surface a real problem loudly) or add an explicit `row > tileCount / Math.Max(columns, 1)` guard before multiplying. The codebase already uses `checked` for the same reason in `Bgra32BufferLayout` (`ICW-307`) and `BackgroundTilePayload`'s dimension math — this would just extend an existing convention, not introduce a new one.

---

## 3. [TRIVIAL] "Do not touch" comments — one already backed by a test, one just needs a tone pass

**Confidence: 90%**

```csharp
// CameraTransform.cs:5-7
// LUKE: I SET THESE EXTREMELY WIDE ON PURPOSE. DO NOT TOUCH.
// The minimum scale is effectively a zoom-out limit, and the maximum scale is effectively a zoom-in limit.
// The actual zoom-in/out limits are determined by the viewport size and the content bounds, which are enforced in `ClampToBounds`.
```
```csharp
// ViewportZoomPolicy.cs:29
if (xIsClamped || yIsClamped) // NOTE: STOP CHANGING THIS LOGIC. IT IS NOT XOR ^. DO NOT REMOVE THIS COMMENT.
```
Checked whether each is defending against a regression that could actually recur silently:
- `ViewportZoomPolicy`'s is: `ComputeWheelDeltas_BothAxesClamped_ChoosesMaxUniformTargetOrFallsBack` (in `ViewportZoomPolicyTests.cs`) explicitly exercises the both-clamped path that an accidental `||`→`^` change would break. The comment is redundant with a real regression test, which is a fine, low-cost belt-and-suspenders combination — no action needed.
- `CameraTransform`'s comment is rationale for a design decision, not a warning about a specific historical bug — also fine as-is.

Only cosmetic note: addressing a specific person by name in committed source ("LUKE:") reads oddly to every contributor who isn't Luke and won't have the context for why. Worth rephrasing as an impersonal rationale comment the next time this file is touched for an unrelated reason — not worth a dedicated change on its own.

---

## 4. [Confirmed clean] `LiveSpatialIndexService<T>`'s lock-free state machine

**Confidence: 85%**

Traced the two places this kind of hot/cold-partition CAS design usually breaks:
- **Concurrent `Add`/`AddRange` during an in-flight `PublishSnapshotAsync`:** `PublishSnapshotAsync` atomically swaps `HotItems` to `[]` and moves them to `PublishingItems` via `Interlocked.CompareExchange`; any `Add` that races this CAS either lands before the swap (included in the batch being published) or after (accumulates in the new, empty `HotItems`) — no window where an added item is neither in the new `HotItems` nor the `PublishingItems` being rebuilt. `Query` always reads `SnapshotIndex` + `PublishingItems` + `HotItems` from one `Volatile.Read`, so it never misses an item that's "in flight" between the two.
- **Failed rebuild recovery:** on an exception from `_indexBuilder.Build`, the `catch` block merges `PublishingItems` back into `HotItems` (`state.PublishingItems.AddRange(state.HotItems)`, correctly picking up anything added during the failed attempt) so nothing is lost and a subsequent `PublishSnapshotAsync` call will retry the same items.

No bug found. Flagging as confirmed-clean rather than silently passing over it, consistent with this series' practice of recording what's been specifically checked and found sound, not just what's broken.

---

## Suggested Priority

1. **§1** — cheap, and the failure mode (silent corruption vs. exception) is the kind of thing worth closing before it's ever hit rather than after.
2. **§2** — trivial, bundle with any other low-priority arithmetic-hardening pass (there's already a natural grouping with `ICW-307`'s `Bgra32BufferLayout` work).
3. **§3** — no action required; noted for completeness.

## Assumptions & Open Questions

- §1's severity assumes a zero-size tile `Bounds` is reachable from *some* future code path even though none was found in the current `SampleImageGenerator`; if the team is confident zero-size bounds can never occur given how tiles are laid out today and always will be, this could reasonably be downgraded to Low/deferred — flagging as Medium because the cost of guarding it is small relative to the cost of the failure mode if a future layout change (e.g., a variable-size-tile feature) makes it reachable.
- As with all prior passes, static source review only; no build or test execution was performed.
