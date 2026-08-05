# InfiniteCanvasWPF — Delta Report: `CanvasViewModel.Zoom` Is Dead on Arrival — Inconsistent Encapsulation in the New Canvas Layer

**Previous reports:** sixteen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. This session reads `CanvasViewModel.cs` in full (not read in the prior session) and traces every zoom-related call path across the whole solution to verify it, per this session's instruction to check rather than assume.

---

## 1. Finding: `CanvasViewModel.Zoom` has zero callers anywhere — production, `CanvasControl`, and its own dedicated test file all bypass it in favor of reaching directly into `Camera`

`CanvasViewModel` defines:
```csharp
public bool Zoom(double scaleDelta, ScreenPoint origin, double width, double height)
{
    if (!Camera.Zoom(scaleDelta, origin))
        return false;
    ApplyViewportSize(width, height);
    return true;
}
```
This reads as the intended public API for "zoom, then reconcile the viewport" — a single call that wraps `CameraTransform.Zoom` and the required `ApplyViewportSize` follow-up, the same encapsulation pattern `Pan` already correctly provides (`CanvasViewModel.Pan` wraps `Camera.Pan` + `ApplyViewportSize`, and `CanvasControl.OnViewportMouseMove` correctly calls `ViewModel.Pan(...)`, not `ViewModel.Camera.Pan(...)`).

**A repo-wide grep confirms `CanvasViewModel.Zoom` is never called.** The actual wheel-zoom path in `CanvasControl.OnViewportMouseWheel` reaches directly into `ViewModel.Camera.Zoom(zoomDeltas.ScaleX, zoomDeltas.ScaleY, ...)` — the raw `CameraTransform`'s own three-argument overload (separate X/Y scale deltas), not the ViewModel's two-argument wrapper — and then manually calls `ViewModel.ApplyZoomFloor(...)` and `ViewModel.ApplyViewportSize(...)` itself, replicating exactly what `Zoom` was already built to do in one call. `CanvasViewModel`'s own dedicated test file, `CanvasViewModelTests.cs`, does the same thing: `viewModel.Camera.Zoom(0.1, new ScreenPoint(0, 0));` — even the test suite written for this class reaches through `Camera` rather than exercising the method meant to wrap it. I checked every `.Zoom(` call site across `src/` and `tests/` (nine total) to confirm this — none of the nine calls `CanvasViewModel.Zoom`.

**Why this matters for the extraction effort specifically:** this is the same "expose the internal collaborator instead of a method" pattern my last report found in `CanvasControl` (raw `TextBlock`/`Border`/`Viewbox` properties) — just one layer down, in the ViewModel rather than the control. `Camera` is a `public CameraTransform Camera { get; } = new();` — a fully mutable object exposed wholesale, and `CanvasControl` reaches through it for zoom while correctly going through the wrapper for pan. That inconsistency (`Pan` used correctly, `Zoom` bypassed) is good evidence the wrapper-method design was the actual intent, not an afterthought that was never really planned — `Zoom` exists, is well-formed, and simply isn't used. As `ICW-313` (input-handler abstraction) and `ICW-316` (assembly extraction) proceed, whichever one touches wheel-zoom should either delete `CanvasViewModel.Zoom` (since the three-argument, separate-X/Y-delta version `CameraTransform.Zoom` provides is what's actually needed — the two-argument wrapper's single `scaleDelta` can't represent non-uniform zoom at all, which may be *why* it was abandoned) or extend it to accept separate X/Y deltas and route the real call sites through it.

**One nuance worth being precise about, since it may explain the bypass rather than just describing it:** `CanvasViewModel.Zoom`'s signature takes a single `scaleDelta` (uniform zoom only). The actual wheel-zoom path needs `ViewportZoomPolicy.ComputeWheelDeltas`'s separate `ScaleX`/`ScaleY` results, because zoom can hit the X and Y floor independently (this is exactly what `ApplyZoomFloor`'s own non-uniform branch, lines 106-109, handles). **`CanvasViewModel.Zoom` may be dead precisely because its signature can't express what callers actually need** — a signature mismatch discovered organically during `CanvasControl`'s implementation, rather than a wrapper nobody bothered to use. If that's the history, the right fix is deleting the unusable uniform-only overload and, if a wrapper is still wanted, adding one with the correct two-scale signature — not reviving the existing dead one as-is.

**Confidence:** 95% (every `.Zoom(` call site in the solution enumerated and read; the "why it might be dead" explanation in the last paragraph is a plausible inference from the signature mismatch, not confirmed against any commit history or ticket text).

---

## 2. Smaller finding: `CanvasViewModel.ComputeMinimumZoom` and `ApplyZoomFloor` still divide by `SceneBounds.Width`/`Height` with no local guard — same class of bug as the already-open `ICW-301`/`ICW-308`, now present in newly-written code

```csharp
public (double ScaleX, double ScaleY) ComputeMinimumZoom(double viewportWidth, double viewportHeight)
{
    return (viewportWidth / SceneBounds.Width, viewportHeight / SceneBounds.Height);
}
```
No guard against `SceneBounds.Width == 0` or `SceneBounds.Height == 0` inside this method itself. In the two current call sites (`CanvasViewModel.ApplyZoomFloor` and `CanvasControl.OnViewportMouseWheel`), both correctly gate the call behind `HasScene` first, so this is not currently reachable with a degenerate scene — but `ComputeMinimumZoom` is `public`, and nothing about its signature or a doc comment indicates the caller must check `HasScene` first. This is the same "unguarded division in freshly-written code, safe today only because every current caller happens to check first" pattern `ICW-301` (for `CameraSnapshot`) and `ICW-308` (for `SpatialBounds`) already describe for other types — worth noting that the pattern is still being introduced in code written after those tickets existed, and a one-line guard (or an XML doc comment stating the precondition) here would be cheap insurance against a third caller someday skipping the check.

**Confidence:** 90% (both call sites and the method body read directly; no new call site was found to violate the guard today).

---

## 3. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `CanvasViewModel.Zoom` | Public method, zero callers anywhere including its own test file | **New finding**: dead on arrival, likely because its uniform-only signature can't express what the real wheel-zoom path needs. Recommend deleting or fixing the signature during `ICW-313`/`ICW-316`, not leaving it as a second, unused zoom entry point. | §1 |
| `CanvasViewModel.ComputeMinimumZoom` | Public, no internal guard | **Extend `ICW-301`/`ICW-308`'s pattern**: same unguarded-division risk, now present in code written after those tickets were filed. Currently safe only because both callers check `HasScene` first. | §2 |

---

## 4. Assumptions & Open Questions

- I did not read `CanvasControl.xaml` (the markup, 75 lines) or the full `canvas-data-source-abstraction-council-review-26-08-04.md` this session either — still outstanding from the prior report's noted gaps.
- §1's inference about *why* `Zoom` is dead (signature mismatch discovered during implementation) is plausible but unconfirmed — no git history is available through this session's tooling to check whether `Zoom` predates or postdates `OnViewportMouseWheel`'s direct-`Camera` approach.
- Given this is the second consecutive session finding an "exposed internal collaborator, bypassed wrapper method" pattern in the brand-new canvas layer (report 16: `CanvasControl`'s raw UI elements; this report: `CanvasViewModel.Camera`), it may be worth a single, explicit pass across both `CanvasControl.xaml.cs` and `CanvasViewModel.cs` checking every public member for "is this actually the call path production code uses, or is there a bypass" before `ICW-316` extracts either into a separate assembly — catching this per-member now is cheaper than discovering an assembly boundary doesn't actually encapsulate what it claims to.

---

*Methodology note: this session read `CanvasViewModel.cs` in full, then, per this session's explicit instruction not to assume file contents, grepped every `.Zoom(` call site across both `src/` and `tests/` (nine total occurrences) and read each one to confirm which overload it invokes, rather than assuming the ViewModel's own wrapper method was in use because it existed and looked reasonable.*
