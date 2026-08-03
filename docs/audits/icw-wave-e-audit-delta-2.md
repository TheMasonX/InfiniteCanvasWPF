# InfiniteCanvasWPF — Audit Delta #2 (Sprint 1 Wave E area)

**Scope of this pass:** Expanded outward from `TileWorkCoordinator.cs`/`BackgroundTileContracts.cs` into their primary consumer, `SampleImageTile.cs` (890 lines, fetched in full), plus `MainWindow.xaml.cs`'s pixelometer path, `DefectOverlaySampler.cs`, `CoalescingAsyncAction.cs`, and `LiveSpatialIndexService.cs`.
**This document contains only what's new or corrected relative to the first report** (`icw-wave-e-audit.md`). Everything re-confirmed without change (e.g., `ICW-P0-ACTIVECOUNT` residuals, `AppendMatches`'s known O(n) scan under `ICW-060`, `CoalescingAsyncAction`'s serialization behavior) is omitted here — see the first report and the project's own bundled audits for those.

---

## New Finding 1 — Pixelometer hover path computes two different "defect at this point" answers from two different algorithms, using two independent spatial queries — **High confidence, directly sharpens the still-open `ICW-100` (overlay precedence) ticket**

`MainWindow.UpdatePixelometer` (called, unthrottled, on every `OnViewportMouseMove`) does this in sequence:

```csharp
// Inside TryReadPixelValue (called first):
var sampleArea = new SpatialBounds(worldX, worldY, 0.01, 0.01);
var hitAnnotations = _spatialIndex.Query(sampleArea);
for (var index = 0; index < hitAnnotations.Count; index++)
{
    if (hitAnnotations[index].TryGetDefectValue(worldX, worldY, out var value))
        defect = Math.Max(defect, value);   // <-- highest-value wins, order-independent
}
...
// Immediately after, back in UpdatePixelometer:
var finalValue = ResolveDisplayPixelValue(backgroundValue, worldX, worldY);
// which internally does:
var sampleArea = new SpatialBounds(worldX, worldY, 0.01, 0.01);   // same bounds, re-queried
var hitAnnotations = _spatialIndex.Query(sampleArea);              // second, identical query
return DefectOverlaySampler.ResolveDisplayValue(backgroundValue, hitAnnotations, worldX, worldY);
```

`DefectOverlaySampler.ResolveDisplayValue(byte, IEnumerable<SampleAnnotation>, double, double)` (verified, full 26-line file):

```csharp
var resolvedValue = currentValue;
foreach (var annotation in annotations)
{
    resolvedValue = ResolveDisplayValue(resolvedValue, annotation, worldX, worldY);
    // ^ unconditionally overwrites on every hit — last-wins, order-dependent
}
```

Two distinct problems, both real:

1. **Wasted duplicate work.** `_spatialIndex.Query(sampleArea)` — a full spatial-index query, not a cache read — runs twice per call with byte-for-byte identical arguments, once inside `TryReadPixelValue` and again inside `ResolveDisplayPixelValue`, on a path that fires on every unthrottled mouse-move event over the viewport. This is a second, independent instance of the "duplicate iteration per frame" pattern already flagged in the first report for `RenderFrameAsync`'s tile-bounds intersection — same root cause (two nearby call sites each independently re-deriving the same query result), different subsystem.

2. **The two computed values can disagree, and both are shown to the user in the same status line.** `defectValue` (displayed via `$"... + defect {defectValue}"`) is computed with **max-wins** semantics (`Math.Max` across all hits, order-independent). `finalValue` (displayed via `$"PIXEL {finalValue}"`, and internally built on top of `DefectOverlaySampler.ResolveDisplayValue`) is computed with **last-wins** semantics (each hit unconditionally overwrites the previous). For two overlapping annotations at the same world point with different defect values — e.g. one with value 200 and a later one (in `_spatialIndex.Query`'s result order) with value 50 — the status bar would simultaneously show `defect 200` and a `PIXEL` value derived from `50`. These aren't two views of the same computation; they're two different, undocumented precedence policies applied to the same input and surfaced together as if they agreed.

This is the exact ambiguity the existing `ICW-100` "overlay precedence" ticket already names in the abstract (*"`DefectOverlaySampler.ResolveDisplayValue` uses last-wins order... Define explicit precedence"*) — the delta here is a concrete, traced reproduction (overlapping annotations, specific values, specific call sites) rather than a general concern, plus the previously-unnoted duplicate-query cost that a fix for the precedence issue would naturally eliminate as a side effect (compute `hitAnnotations` once, derive both the raw defect number and the blended pixel value from that single list with one agreed-upon precedence rule).

**Recommendation:** Fix as one change: query `_spatialIndex` once per `UpdatePixelometer` call, pick a single precedence policy (max-value is probably the more defensible default for a diagnostic readout, but that's a product call, not an engineering one), and derive both displayed numbers from it so they can't disagree. This also resolves the duplicate-query cost for free. Attach directly to the still-open `ICW-100` (overlay-precedence) ticket rather than filing a new one — this *is* that ticket's concern, just now with a concrete repro.

---

## New Finding 2 — `SampleImageTile.TryGetPixelsNonBlocking(mipLevel, ...)`'s fallback-candidate selection allocates and sorts on the same unthrottled mouse-move hot path — **Medium-high confidence, new perf finding distinct from the already-closed `ICW-020`/`ICW-055`**

`ICW-020`/`ICW-055` (both Done) fixed the O(1) **tile** lookup for the pixelometer (which of the N tiles contains this world point). That fix is real and doesn't need revisiting. This is a different, still-open cost one level down: once the correct tile is found, `TryReadPixelValue` calls `tile.TryGetPixelsNonBlocking(mipLevel, out pixels, out residentMipLevel, ...)`, whose fallback path (taken whenever the exact requested mip isn't resident, which is routine during any zoom transition) does this **under a lock**, every call:

```csharp
var fallbackCandidates = new List<(int MipLevel, byte[] Pixels)>();
if (_pixels is not null) fallbackCandidates.Add((0, _pixels));
fallbackCandidates.AddRange(_mipPixels.Select(pair => (pair.Key, pair.Value)));
var fallback = fallbackCandidates
    .OrderBy(candidate => Math.Abs(candidate.MipLevel - mipLevel))
    .ThenBy(candidate => candidate.MipLevel)
    .FirstOrDefault(candidate => candidate.Pixels is not null);
```

This allocates a `List<(int,byte[])>`, a LINQ `Select` iterator, and a full `OrderBy`/`ThenBy` sort (up to 8 mip levels, so small in absolute terms, but non-zero allocation + comparison overhead) **inside `_cacheGate`'s lock**, on every mouse-move over the viewport whenever the resident set doesn't already contain the exact requested mip level — which, per `BackgroundTileMipPolicy.SelectMipLevel`, is a normal, frequent state during any pan/zoom sequence, not an edge case. The render path's own per-frame tile draw already goes through the same fallback logic once per visible tile per frame (`DrawTile`/`GenerateFrozenBitmap`, not audited line-by-line here), so this method's cost is already being paid at least once per frame; the pixelometer path adds a second, independent invocation per mouse-move event, uncorrelated with frame cadence and with no debounce.

This is lower severity than a correctness bug — it's a handful of small allocations under a lock, not a queue-depth-scaling algorithmic risk like the `DrainQueueWithLivenessCheck` finding in the first report — but it sits on a genuinely hot, unthrottled input path, and the fix is simple (track the nearest-resident mip incrementally as `_mipPixels`/`_pixels` change, rather than re-deriving it by sorting the full candidate set on every read).

**Recommendation:** Either (a) debounce `UpdatePixelometer` to a frame-aligned or time-based cadence (it currently has no throttle at all, unlike the render request path which already goes through `CoalescingAsyncAction`), or (b) maintain the "nearest resident mip" as an incrementally-updated field instead of recomputing it by full sort on every read, or both. (a) is the cheaper fix and likely sufficient on its own — no diagnostic overlay needs sub-frame update latency.

---

## Minor Note — Queued work items with zero live claimants are only reaped lazily, and only via events elsewhere — **Low confidence this is ever observable, recorded for completeness**

Traced (not newly discovered as a defect, just newly confirmed as intentional): when a claimant's token fires while its work item is still `Queued` (not yet `Running`), the auto-removal callback registered in `TileWorkItem.AddClaimant` only calls the *item-level* `TileWorkItem.RemoveClaimant`, which cancels `_workCts` and updates the claimant list, but does **not** touch the coordinator's `_items`/`_queue` collections or counters — that cleanup is deferred to the next `PublishInterestSet` or `DrainQueueWithLivenessCheck` pass, both of which explicitly check `item.ClaimantCount == 0` before promoting/keeping an item. This matches `DrainQueueWithLivenessCheck`'s own doc comment precisely and is confirmed-intentional design, not a bug. The only residual edge case: if neither of those two entry points runs for a while (i.e., no new work completes and no new interest set is published), a zero-claimant queued item could sit un-reaped for an arbitrary period. Given the render loop calls `PublishInterestSet` every frame and `DrainQueue` fires on every completion, this is very unlikely to be observable in the app's current usage pattern — noted here only so it isn't independently "rediscovered" as a bug in a future audit pass without this context attached.

---

*This delta report should be read alongside the first report (`icw-wave-e-audit.md`); it does not repeat that report's summary table or already-covered findings.*
