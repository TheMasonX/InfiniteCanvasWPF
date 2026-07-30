# InfiniteCanvasWPF — Audit Pass 10 (Same HEAD, Remaining View/Model Layer)

**HEAD audited:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` (unchanged since pass 6; verified before writing).
**Scope this pass:** `ViewportScrollbarPolicy.cs`, `CanvasViewportViewModel.cs`, `Bgra32Color.cs`, `AnnotationFeaturePresenter.cs`, `ViewportRenderRequest.cs`, `IRenderer.cs`, and a full inventory of every `public interface` in the codebase (5 total) with implementer counts, plus a check of the remaining unread `InfiniteCanvas.Spatial` files.

One combined architectural finding this pass (§1, elevates and connects a pass-7 observation into a confirmed pattern); everything else checked out clean.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **2 of the 5 interfaces in the entire codebase have zero implementers and zero consumers, and both are in `InfiniteCanvas.Rendering`.** Pass 7 flagged `IBackgroundTileSource` as unused scaffolding; this pass found `IRenderer<TScene, TOutput>` in the same state — its only reference anywhere is `ViewportRenderRequest`, a record whose *only* purpose is to be `IRenderer`'s parameter type. A full inventory of every interface in the solution (`ISpatialEntity`, `ISpatialIndexBuilder<T>`, `ISpatialIndexService<T>`, `IBackgroundTileSource`, `IRenderer<TScene,TOutput>`) confirms the other three are genuinely live (implemented and consumed in production). This is no longer "one unused interface" — it's 40% of the codebase's abstraction seams, concentrated entirely in one layer, sitting unimplemented. | Medium | 90% |
| 2 | Confirmed clean: `ViewportScrollbarPolicy.ComputeMetrics`/`ComputePanDelta` — both guard `sceneLength <= 0` *before* dividing by it, exactly the zero-guard pass 8 found missing in `ZeroCopyBitmapFactory.Windows.cs`'s `DrawTile`. Worth citing as the in-codebase example of "how to do this safely" when fixing pass 8's §1. | — (informational) | 90% |
| 3 | Confirmed non-issue: `LinearSpatialIndexBuilder` (a second, simpler `ISpatialIndexBuilder<T>` implementation alongside `StrTreeSpatialIndexBuilder`) is used only by tests (`CanvasViewportViewModelTests.cs`, `LiveSpatialIndexServiceTests.cs`), never by production code (`MainWindow.xaml.cs` always constructs `StrTreeSpatialIndexBuilder<SampleAnnotation>`). This is a legitimate, ordinary test-double pattern (avoiding the NetTopologySuite-backed tree in unit tests), not another instance of §1's dead-scaffolding pattern — checked specifically to rule that out given the coincidental "two implementations, one place used" shape. | — (informational) | 85% |

---

## 1. [MEDIUM] Unused-interface pattern is now confirmed at 2-of-5, both in `Rendering`

**Confidence: 90%**

Full interface inventory, with implementer/consumer counts verified by reference search:

| Interface | Implementers | Consumers (beyond own file) | Status |
|---|---|---|---|
| `ISpatialEntity` (`Core`) | `SampleAnnotation` | `ISpatialIndexBuilder<T>`/`ISpatialIndexService<T>` generic constraints, spatial index services | **Live** |
| `ISpatialIndexBuilder<T>` (`Spatial`) | `StrTreeSpatialIndexBuilder`, `LinearSpatialIndexBuilder` | `LiveSpatialIndexService<T>` constructor (production); tests (see §3) | **Live** |
| `ISpatialIndexService<T>` (`Spatial`) | `LiveSpatialIndexService<T>`, `StrTreeSpatialIndexService<T>`, `ImmutableSpatialIndexService<T>` | `CanvasViewportViewModel<T>`, `MainWindow.xaml.cs` | **Live** |
| `IBackgroundTileSource` (`Rendering`) | **none** | two isolated unit tests exercising an unrelated record's constructor only (per pass 7) | **Unused** |
| `IRenderer<TScene, TOutput>` (`Rendering`) | **none** | none — `ViewportRenderRequest.cs`'s only reference outside its own file is `IRenderer.cs`, and `IRenderer.cs`'s only reference outside its own file is nothing | **Unused** |

```csharp
// IRenderer.cs — entire file
public interface IRenderer<in TScene, out TOutput>
{
    TOutput Render(TScene scene, ViewportRenderRequest request);
}
```
```csharp
// ViewportRenderRequest.cs — entire file
public readonly record struct ViewportRenderRequest(SpatialBounds Viewport, double ZoomLevel);
```
Neither type appears anywhere else in `src/` or `tests/`. There is no class implementing `IRenderer`, no method accepting it as a parameter, no DI registration, nothing. `ZeroCopyBitmapFactory` — the class that actually renders frames — implements no interface and isn't referenced through `IRenderer` anywhere.

Combined with pass 7's `IBackgroundTileSource` finding, this reframes what looked like one isolated unused type into a pattern: **both of this codebase's zero-implementation interfaces live in the same namespace**, both look like early sketches toward a pluggable-implementation architecture for the rendering layer (a swappable renderer, a swappable tile source) that the actual implementation (`ZeroCopyBitmapFactory`, `SampleImageGenerator`) never grew into. That's not inherently wrong — sketching an interface before committing to it is a reasonable way to think through a design — but two of them sitting live in the compiled assembly, indistinguishable from load-bearing types, is worth a deliberate decision rather than letting a third one quietly join them next time someone extends rendering.

**Recommendation:** same options as pass 7's §2, now with more weight given the pattern: either commit to the abstraction (have `ZeroCopyBitmapFactory` actually implement `IRenderer`, which would also give the render pipeline a seam for the kind of testing/mocking that's currently impossible since rendering is only reachable through the concrete GDI+-backed class) or remove both interfaces and their sole-purpose supporting types (`ViewportRenderRequest`, and `IBackgroundTileSource`'s four supporting records) until there's an actual second implementation that justifies them. Given `ADR`s exist in this repo for smaller decisions than this, this is a reasonable candidate for one if the team wants to keep the sketch rather than delete it — an ADR would at least record *why* it's there unwired, which today only this kind of audit surfaces.

---

## Suggested Priority

1. **§1** — no urgency, nothing is broken by unused interfaces — but worth a deliberate decision (implement, relabel as experimental, or delete) rather than letting it grow to 3-of-6 the next time someone sketches a rendering abstraction.

## Assumptions & Open Questions

- As with pass 7's §2, the "sketch toward a future pluggable architecture" framing is inferred from shape and naming, not confirmed by any ticket or ADR found in the repo.
- As with all prior passes, static source review only; no build or test execution was performed.
