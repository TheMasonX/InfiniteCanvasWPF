# InfiniteCanvasWPF — Audit Pass 9 (Same HEAD): Background Noise Seamlessness Gap

**HEAD:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` — still unchanged; reconfirmed via commit feed before starting. This pass swept back over `SampleImageGenerator.cs`'s noise-generation code in full (not yet fully read in any prior pass), since it's math-heavy, recently rewritten (`ICW-129`, "Migrate background noise generation to FastNoise2," currently `In Progress`), and exactly the kind of code that hides subtle bugs behind passing unit tests.

---

## Executive Summary

| # | Finding | Severity | Confidence |
|---|---|---|---|
| 1 | **Each tile is generated with a different noise seed (`pixelSeed = options.Seed + 3 * tileIndex`), which defeats ICW-129's own stated goal of "seamless worldspace sampling."** Correct world-space coordinate offsetting is in place, but with a different seed per tile, adjacent tiles sample two *unrelated* noise fields rather than adjacent windows of the *same* field — worldspace offsetting alone cannot produce continuity across a seed change. | **Medium-High** | 85% |
| 2 | **Compounding issue in the same code path:** noise contrast is normalized independently per tile, using that tile's own locally-sampled min/max (`GenUniformGrid2D`'s output range), rather than a fixed or scene-wide range. Even if finding #1 were fixed, tiles would still show visible contrast-level seams at their shared boundaries, since each tile stretches its own local extrema to fill the same fixed jitter band. | **Medium** | 80% |

Both findings are inside `SampleImageGenerator.GenerateNoisePixelsCore`/its caller (`GenerateMonochromeMipPixels`), not covered by any existing ticket beyond ICW-129 itself (which describes the intended end state, not this gap), and not caught by the "24/24 tests passed" evidence already recorded against ICW-129 — I checked `SampleImageGeneratorTests.cs` and confirmed there is no test that samples two adjacent tiles and compares their noise values or contrast at the shared boundary; existing tests validate same-seed-same-output determinism and that worldspace offsets are threaded through, but not actual cross-tile continuity.

---

## 1. [MEDIUM-HIGH] Per-tile seed variation defeats "seamless worldspace sampling"
**File:** `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:187` (seed derivation), `:277-288` (mip-scaled step size + noise call), `:503-556` (`GenerateNoisePixelsCore`)
**Confidence: 85%**

```csharp
var pixelSeed = options.Seed + 3 * tileIndex;   // ← different for every tile
...
() => GenerateMonochromeMipPixelsSeeded(options.PixelWidth, options.PixelHeight, options.TargetValue,
    options.Noise, 0, pixelSeed, options.CircleCount, noiseSettings, (float)bounds.X, (float)bounds.Y, tileId),
```

`bounds.X`/`bounds.Y` (this tile's absolute world position) are correctly threaded through as `worldOriginX`/`worldOriginY` into `GenerateNoisePixelsCore`'s call to FastNoise2's `GenUniformGrid2D(..., worldOriginX, worldOriginY, width, height, stepSize, stepSize, seed)`. This is the *coordinate* half of "seamless worldspace sampling," and it's implemented correctly — a fixed noise field sampled at two adjacent world-space windows, with the same seed, would tile seamlessly. But `seed` here is `pixelSeed`, and `pixelSeed` is deliberately varied per tile (`+ 3 * tileIndex`). Simplex/FBm noise implementations (including FastNoise2's) treat the seed as a hash salt that effectively re-randomizes the entire gradient/permutation table — two calls with different seeds, even sampling the exact same world-space coordinates, produce **uncorrelated** output. Varying `worldOriginX`/`worldOriginY` per tile only achieves continuity if the underlying field being sampled is the same field for every tile; varying the seed per tile means each tile samples its own independent field, offset-correctness notwithstanding.

**Why this matters:** the whole point of worldspace-offset sampling (as opposed to the "custom fractal-Brownian-motion" approach ICW-129 is replacing, which presumably worked in tile-local pixel coordinates) is to make the noise pattern continue smoothly across a tile boundary the way a single large noise field would. With a per-tile seed, the pattern will visibly "jump" to an unrelated texture at every tile edge — the exact seam artifact worldspace sampling was meant to eliminate.

**Is this intentional?** I don't think so, based on the ticket's own text: ICW-129 explicitly lists *"Preserve deterministic tile generation across tiles and across repeated runs for a fixed seed"* and *"seamless worldspace sampling"* as scope items. "Deterministic... for a fixed seed" is satisfied (re-running with the same `options.Seed` reproduces the same per-tile seeds and thus the same output) — but that's a different property than *spatial* seamlessness across tiles, which needs the *same* seed reused across all tiles, not a fixed *global* seed that's then perturbed per tile.

**Recommendation:** Pass `options.Seed` directly (unmodified) as the noise seed for every tile's `GenerateNoisePixelsCore` call, relying entirely on `worldOriginX`/`worldOriginY` for per-tile variation (which is already correctly wired). If per-tile seed variation for the *defect placement*/*classification* randomness (a separate, legitimately-per-tile-independent concern, already handled via `DeterministicRandom` elsewhere) is what `pixelSeed` was originally meant for and got reused here by mistake, that would explain how this happened — worth checking whether `pixelSeed` is used anywhere else in this method for a purpose where per-tile variation *is* correct, to make sure separating the two doesn't lose something.

---

## 2. [MEDIUM] Per-tile-local min/max noise normalization will still seam even if #1 is fixed
**File:** `src/InfiniteCanvas.Rendering/SampleImageGenerator.cs:522-549` (`GenerateNoisePixelsCore`)
**Confidence: 80%**

```csharp
var outputMinMax = fastNoise.GenUniformGrid2D(noiseBuffer.AsSpan(0, pixelCount), worldOriginX, worldOriginY, width, height, stepSize, stepSize, seed);
var noiseMin = outputMinMax.min;
var noiseMax = outputMinMax.max;
var range = noiseMax - noiseMin;
...
var jitterScale = noiseSpread * (float)Math.Max(0.0, noiseSettings.Amplitude);
var noiseToJitterScale = (2.0f * jitterScale) / range;
var noiseToJitterOffset = (-noiseMin * noiseToJitterScale) - jitterScale;
```

`GenUniformGrid2D` returns the actual min/max of the noise values it generated *for this one call* (this tile, this mip level), and the code stretches that local range to fill the full `[-jitterScale, +jitterScale]` band every time. Even with a shared seed and correct world-space continuity (fixing finding #1), FBM noise doesn't have a globally uniform local min/max — different windows of the same field can have different local extrema, especially at the octave/gain/lacunarity settings exposed here (higher octave counts and gain values increase the variance of local extrema across different sample windows). That means two adjacent tiles sampling continuous, correlated noise values would still get **independently rescaled contrast**: a tile that happens to sample a locally "flat" region gets its subtle variation stretched to look just as high-contrast as a neighboring tile that sampled a locally "busy" region. The underlying pattern would connect correctly at the seam (once #1 is fixed), but the visible brightness/contrast would still jump.

**Recommendation:** Normalize against a fixed, scene-wide (or theoretical, derived from the FBM configuration's known output bounds) min/max rather than each call's local sample range — e.g., sample/derive the min/max once per scene generation (or use FastNoise2's documented output bounds for a given octave/gain/lacunarity combination, if it exposes one) and pass that fixed range into every tile's `GenerateNoisePixelsCore` call instead of recomputing it per call.

---

## Note on scope

Both findings sit inside a ticket (`ICW-129`) that's already `In Progress` and whose own text names the exact property (seamless worldspace sampling) these findings show isn't yet achieved — so this isn't a new, separately-motivated request, just evidence that the ticket's acceptance criteria aren't fully met yet by the current implementation, worth checking before marking it `Done`.
