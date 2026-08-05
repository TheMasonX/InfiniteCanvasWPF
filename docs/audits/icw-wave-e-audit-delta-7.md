# InfiniteCanvasWPF — Audit Delta #7 (post scroll-flash fixes / FrameBufferPool / MVVM extraction)

**Scope of this pass:** Read `FrameBufferPool.Windows.cs` (new triple-buffer rotation class, 146 lines, full read) and its integration in `MainWindow.xaml.cs` end to end — no issues found, it's a clean, correct implementation of exactly the two-pass composition-safe reuse pattern its own doc comments describe. Read `CanvasViewModel.cs`/`CameraTransform.cs` fully to check whether the axis-independent zoom-floor math could permanently distort the canvas's aspect ratio — traced it in full, then found this is a **known, deliberately-designed, already-audited feature** (`ICW-011`/`ICW-013`, confirmed correct by a prior audit's addendum), not a bug; recorded below so it isn't mistaken for one again. Then read the newest commit (`3b0d099`, "persistent frame shell stops remaining scroll flashes," today's date in-repo) in full, which is where this pass's actual finding is.

---

## New Finding — `UpdateTileGridLayer` rebuilds the tile-boundary overlay from the **entire scene's tile collection** every frame, not the viewport-filtered set already computed for that same frame — pre-existing, but carried forward unfixed by today's rewrite of this exact method — **High confidence, verified against both the old and new implementations**

`MainWindow.RenderFrameAsync` already computes a viewport-filtered tile list once per frame, right before publishing:
```csharp
var visibleTiles = _tiles.Where(tile => tile.Bounds.Intersects(viewport)).ToArray();
...
return (Bitmap: bitmap, VisibleItems: visibleItems, VisibleTiles: visibleTiles);
```
`frame.VisibleTiles` is a real field on the returned tuple and is used later in the same method (`frame.VisibleTiles.Count(tile => tile.IsImageGenerated)`). But `UpdateTileGridLayer` — the method today's commit rewrote from `BuildTileGridLayer` to update the new persistent shell in place — never receives it. Instead, every call does:
```csharp
foreach (var worldX in _tiles.SelectMany(tile => new[] { tile.Bounds.X, tile.Bounds.Right }).Distinct())
{ ... gridLayer.Children.Add(new Line { ... }); }
foreach (var worldY in _tiles.SelectMany(tile => new[] { tile.Bounds.Y, tile.Bounds.Bottom }).Distinct())
{ ... gridLayer.Children.Add(new Line { ... }); }
```
`_tiles` here is the **complete, unfiltered scene tile collection** — the same field `RenderFrameAsync` explicitly filters down to `visibleTiles` two lines earlier for every other rendering purpose. This method skips that filter and enumerates every tile in the scene, regardless of whether it's anywhere near the viewport, every single frame, unconditionally (there is no visibility toggle for the grid overlay — `grep` for a `_showTileGrid`-style flag found none; this teal boundary grid is always drawn).

**This means the per-frame cost of this method scales with total scene size, not viewport size or visible-tile count** — for a large canvas (which is the entire point of an "infinite canvas" app with background-tile/mip/LOD machinery), this is a `SelectMany` + `Distinct` over potentially thousands of tile-edge coordinates, followed by allocating and adding a WPF `Line` `FrameworkElement` per distinct edge, every frame, most of them for tiles nowhere near the screen (only clipped visually after full construction — `ClipToBounds = true` doesn't exempt off-screen children from being built, measured, and arranged).

**This is not new** — I diffed the pre-commit version (`git show 3b0d099^:...`, the old `BuildTileGridLayer`) and it has the exact same `_tiles.SelectMany(...)` — byte-for-byte identical logic, just returning a fresh `Canvas` instead of mutating a persistent one. So this bug predates today's session entirely. What's notable for this audit is that **today's commit rewrote this exact method for an unrelated reason (attaching it to a persistent shell instead of rebuilding the Canvas) and had every opportunity to also thread `visibleTiles` through, but didn't** — the bug was carried forward unchanged into new code. A prior audit (`docs/audits/ICW-Audit-7-25-26-audit-26-07-25-03-11-26.md`) already flagged the adjacent, but distinct, concern that *"`BuildTileGridLayer` also reconstructs every line every frame"* — that's about the per-frame rebuild cost in general (which the new persistent-shell design doesn't change — `UpdateTileGridLayer` still does `gridLayer.Children.Clear()` and fully rebuilds every frame). Neither that audit nor today's rewrite caught that the rebuilt set is drawn from the *wrong, much larger* source collection. These are two separable problems with two separable fixes:

**Recommendation:**
1. Immediate, minimal fix: change `UpdateTileGridLayer`'s two `_tiles.SelectMany(...)` calls to use the already-computed `visibleTiles`/`frame.VisibleTiles` instead of `_tiles`, and thread that collection through `PublishFrame`'s parameter list (it currently receives `annotations`, `camera`, `frameWidth`, `frameHeight` but not the visible-tiles list). This alone bounds the per-frame cost to viewport size, matching every other per-frame operation in this same method.
2. Larger fix (matches the prior audit's already-flagged concern, extend rather than duplicate): only rebuild the grid-line set when the camera or viewport-filtered tile set actually changes, rather than on every frame regardless of whether anything moved.

Given `ICW-317`/`ICW-318` (today's scroll-flash fixes) are specifically about frame-publish performance and visual stability, and this method sits directly in `PublishFrame`'s per-frame hot path, this is worth a follow-up ticket scoped under that same cluster rather than a fresh unrelated one — e.g. `ICW-317-FOLLOWUP` or folded into whatever ticket eventually acts on the prior audit's "reconstructs every line every frame" note.

---

## Investigated, confirmed not a bug (recorded so it isn't re-flagged)

`CanvasViewModel.ApplyZoomFloor`'s non-uniform fallback branch (independently pushing `ScaleX`/`ScaleY` to their own per-axis minimums when the viewport aspect ratio doesn't match the scene's) looks, on first read, like it could permanently distort the canvas's aspect ratio once triggered, since ordinary `Zoom(scaleDelta, origin)` calls afterward multiply both axes by the same delta and would preserve rather than correct any divergence. Tracing further: this is exactly `ICW-011`/`ICW-013`'s deliberately-designed *"axis-clamped non-uniform zoom"* / *"uniform-first zoom floor policy"* feature (stretch to fill rather than letterbox when the floor is hit on only one axis), and a prior audit (`docs/audits/infinitecanvaswpf-code-audit-addendum-26-07-24-22-24-24.md`) already traced this exact math by hand and confirmed it's correctly implemented. `active-tasks.md`'s `ICW-009` entry already carries the known, intentionally-deferred follow-up: *"Evaluate optional letterboxed presentation mode for aspect ratio preservation."* Nothing new to add here — recorded only so this pass's investigation isn't wasted if it comes up again.

---

*This delta report should be read alongside `icw-wave-e-audit.md` and `icw-wave-e-audit-delta-2.md` through `-6.md`. It does not repeat their content. Still unexplored from this session's stated goals: `CanvasControl.xaml.cs`'s wheel-zoom/pan interaction code, `CanvasViewportViewModel.cs`, and the `tests/` directory for coverage-gap analysis — worth a follow-up pass.*
