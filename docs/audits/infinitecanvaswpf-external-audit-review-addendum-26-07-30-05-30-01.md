# InfiniteCanvasWPF — External Audit Review, Addendum

**Scope:** Finished reviewing the two original source reports (the external bug audit request document and the independent bug audit report document) that fed into an AI assistant synthesis reviewed previously. This addendum covers only what's genuinely new since that report: two confidence corrections backed by evidence the external auditors didn't have access to, and a cross-validation note.

---

## 1. Confidence correction: `ICW-M-023` (anisotropic mip selection) should be raised, not left at 78%

**Claim:** `BackgroundTileMipPolicy.SelectMipLevel` picks a mip level from `Math.Min(camera.ScaleX, camera.ScaleY)`, which can select too coarse a mip for the more-zoomed-in axis under non-uniform (anisotropic) zoom.

I confirmed this directly against source (`BackgroundTileContracts.cs:171-172`):
```csharp
var minimumScale = Math.Min(camera.ScaleX, camera.ScaleY);
var level = (int)Math.Floor(Math.Log2(1.0 / minimumScale));
```
Using the *smaller* of the two axis scales means mip selection is always driven by whichever axis is currently more zoomed *out* — so whenever the axes diverge, the more zoomed-*in* axis gets under-resolved (visibly blurrier than it should be), because the resolution decision ignores it entirely in favor of the less-demanding axis.

**Why the external audit's 78% is too conservative:** their confidence reflects (reasonably, from a static source read) uncertainty about whether non-uniform zoom is a real, reachable state or just a theoretical possibility the type system allows. From my own audit history, I know it's neither theoretical nor rare — non-uniform (independent per-axis) zoom is a deliberately built, actively-maintained feature in this codebase (the "uniform zoom recovery" and "custom zoom entry" work I reviewed in an earlier session exists specifically to *handle* the case where `ScaleX != ScaleY`, including a dedicated recovery path for when the two axes cross a common target again). Given the feature exists specifically because this divergent-scale state is expected to occur in normal use, this isn't an edge case worth 78% confidence — it's a routine state this exact function will regularly be called with. I'd put this at 90%+.

**No new recommendation beyond theirs** ("select mips from the more demanding axis, or use anisotropic/LOD-aware sampling") — just confirming it deserves higher priority than a 78%-confidence P2 item would normally get.

---

## 2. Confidence correction: `ICW-M-024` (half-open vs. inclusive bounds) should be raised from 74% — and it's already tracked

**Claim:** `TileGridIndexLookup` treats right/bottom edges as *outside* a tile, while `SpatialBounds.Intersects` uses *inclusive* comparisons — an inconsistent edge convention.

I read both implementations directly:
```csharp
// SpatialBounds.Intersects — inclusive at touching edges
return X <= other.Right && Right >= other.X && Y <= other.Bottom && Bottom >= other.Y;
```
```csharp
// TileGridIndexLookup.TryGetTileIndex — exclusive at the right/bottom edge
if (worldX < sceneBounds.X || worldX >= sceneBounds.Right || worldY < sceneBounds.Y || worldY >= sceneBounds.Bottom)
{
    return false;
}
```
This is a literal, confirmed inconsistency, not an inference — `Intersects` says a point exactly on the right/bottom edge is *inside*; `TryGetTileIndex` says the same point is *outside*. That's about as directly verifiable as a claim gets, so I'd raise this to ~90%+ confidence rather than 74%.

**Also worth noting for reconciliation purposes:** this exact issue is already independently tracked in the live backlog as `ICW-308` ("Clarify SpatialBounds intersection semantics (inclusive vs exclusive)," status `Proposed`, P2) — though that ticket's current scope is narrower than what the external audit and my own check confirm: `ICW-308` only proposes documenting/testing `SpatialBounds.Intersects`' own inclusive behavior, it doesn't mention reconciling it against `TileGridIndexLookup`'s *opposite* convention. Worth widening `ICW-308`'s scope (or filing a linked ticket) to cover the cross-utility inconsistency, not just documenting one side of it in isolation.

---

## 3. Cross-validation note: the dead-`DefectBitmap` cluster is now confirmed by three independent sources

Both original source reports (not just the synthesis) independently rediscovered the two `DefectBitmap`-related issues I found in an earlier session:

- **`ICW-BUG-009`** (in the independent bug audit report document): `DrawDefectPatch` locks a `Bitmap`, reads a value from it, and never uses that value — matches my own finding exactly, including having identified the same discarded local.
- **`ICW-BUG-010`**: old defect-template bitmaps can be disposed while old render work may still be executing, since `RegenerateSceneAsync` cancels tile work and disposes the template pool without waiting for in-flight work to actually stop — matches the dispose-vs-render race I found independently in an earlier session, down to the same two method calls (`CancelAll()` immediately followed by `DisposeDefectTemplatePools`).

Three independent audit efforts (two external, one mine, using different methods — mine via live commit-history archaeology, theirs via static source review) converged on the same two findings without any of us referencing each other's work beforehand. That level of independent convergence is itself useful signal: this cluster (dead GDI+ sampling path + the disposal race it enables) is a safe bet for high real-world priority regardless of any single report's stated confidence number, and is a good candidate for a "fix once, closes three tickets across three reports" task if/when the backlog gets reconciled.
