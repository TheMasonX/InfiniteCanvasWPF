# InfiniteCanvasWPF — Audit Delta #4

**Scope of this pass:** `ZeroCopyBitmapFactory.Windows.cs` (344 lines, full read) — the unsafe native-interop rendering core that composites tiles and defect annotations into the frame buffer — cross-checked against `DefectTemplateFactory.cs`, `SampleAnnotation.TryGetDefectValue`, and the existing `DefectBitmap`-related ticket cluster (`ICW-097`, `ICW-102`, `ICW-103`, `ICW-306`, `ICW-004`). Also skimmed `TileGridIndexLookup.cs` and `CanvasUserSettings.cs` (Core) — both clean, nothing to report.

---

## New Finding — `DrawDefectPatch` locks and reads `annotation.DefectBitmap` via unsafe GDI+ interop every frame, for every visible annotation, and the value it reads is never used — **High confidence, unambiguous, verified line-by-line**

`ZeroCopyBitmapFactory.DrawDefectPatch` (lines 230–283):

```csharp
var bitmapData = bitmap.LockBits(bitmapBounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
try
{
    var source = (byte*)bitmapData.Scan0;
    for (var y = top; y < bottom; y++)
    {
        ...
        var sourceRow = source + (sourceY * bitmapData.Stride);
        for (var x = left; x < right; x++)
        {
            ...
            var value = sourceRow[sourceX * 3];   // <-- read from the locked GDI+ bitmap
            var offset = _layout.GetPixelOffset(x, y);
            var currentValue = destination[offset];
            var displayValue = DefectOverlaySampler.ResolveDisplayValue(currentValue, annotation, worldX, worldY);
            destination[offset] = displayValue;
            ...
        }
    }
}
finally { bitmap.UnlockBits(bitmapData); }
```

`value` is assigned from the locked bitmap's raw bytes and then **never referenced again** — not by `displayValue`, not by anything else in the loop or method. `DefectOverlaySampler.ResolveDisplayValue(byte currentValue, SampleAnnotation annotation, double worldX, double worldY)` (confirmed by reading its 26-line source, cited in Delta #2) computes the displayed byte entirely from `annotation.DefectPixels` — a separate, independent `byte[]` on the `SampleAnnotation` record — via `TryGetDefectValue`'s own coordinate math (using `DefectPixelWidth`/`DefectPixelHeight`, not `bitmap.Width`/`bitmap.Height`). The `bitmap`/`LockBits`/`sourceRow` machinery contributes nothing to what actually gets drawn.

Tracing where `DefectBitmap` comes from confirms this isn't an oversight where the two sources are expected to diverge and get reconciled later — `DefectTemplateFactory.Build` constructs `DefectBitmap` **by copying the exact same `pixels` array** that also becomes `DefectPixels` on the annotation (`CreateBitmapFromPixels(templateWidth, templateHeight, pixels)`, called with the identical `pixels` passed to the `DefectTemplate` record). So `DefectBitmap` is a second, GDI+-native, 3-bytes-per-pixel representation of data `DrawDefectPatch` already has cheaper, direct access to via `annotation.DefectPixels` (1 byte per pixel, no interop, no lock/unlock) — built once per template, carried on every placed annotation, and in this method's read path, touched only to be discarded.

**Cost profile:** this isn't a one-time cost. `DrawDefectPatch` runs once per visible `SampleAnnotation` per rendered frame (inside `GenerateFrozenBitmap`'s `foreach (var annotation in annotations)`). Each call does a `Bitmap.LockBits`/`UnlockBits` pair — real GDI+ interop with associated marshaling and (per `ICW-103`'s already-tracked concern) thread-safety exposure — purely to produce a value that's thrown away. For a scene with many annotations rendered every frame during continuous pan/zoom, this is a steady, avoidable per-frame cost sitting directly in the render hot path this whole ticket cluster (`ICW-004`, `ICW-097`) already treats as performance-sensitive.

This is distinct from every existing `DefectBitmap`-related ticket: `ICW-103` is about the lock racing with concurrent disposal (a *safety* concern about this same call), `ICW-306` is about the pixel-format assumption being undocumented/fragile (a *robustness* concern about this same call), `ICW-004` is a spike to benchmark `DrawTile`/`DrawDefectPatch`'s inner-loop cost in the abstract — none of them note that part of what's being measured/guarded/documented is computing a value nobody reads.

**Recommendation:** Delete the `LockBits`/`source`/`sourceRow`/`value` block entirely — `DrawDefectPatch` doesn't need to touch `bitmap`'s pixel data at all, only its `Width`/`Height` (already used above, before the lock, to size the destination rectangle). If `DefectBitmap`'s pixel payload truly has no other consumer in the codebase (confirmed: `grep` found exactly the four references listed above — the `init` property, this dead read, and the two construction sites — no other reader), this removes not just the wasted per-frame lock but calls into question whether `DefectBitmap` needs to exist as a `Bitmap` at all rather than being replaced by `DefectPixelWidth`/`DefectPixelHeight` (already present on the record) — which would also shrink `ICW-102`'s and `ICW-103`'s scope, since there'd be no GDI+ object left to dispose or race on. Worth flagging that possibility on those two tickets before more work is sunk into disposal/concurrency-guarding a resource that may not need to exist in this form.

---

*This delta report should be read alongside `icw-wave-e-audit.md`, `icw-wave-e-audit-delta-2.md`, and `icw-wave-e-audit-delta-3.md`; it does not repeat their content.*
