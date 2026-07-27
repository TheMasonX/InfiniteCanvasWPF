---
id: ICW-128
key: ICW-128
title: Add FBM-style background noise (octaves + gain + amplitude)
status: Done
type: Improvement
priority: P2
tags:
  - rendering
  - noise
  - sampling
related: []
created: 2026-07-26
updated: 2026-07-26
---

# Add FBM-style background noise (octaves + gain + amplitude)

Add fractal-Brownian-motion-like sampling to the deterministic background generator so that the existing per-pixel value-noise can be layered across multiple lower-frequency octaves. This keeps the value-noise approach but adds coherence at larger scales.

Scope
- Expose new generation parameters on `SampleImageGenerator.GenerateSet` and `GenerateMonochromePixels`/`GenerateMonochromeMipPixels`:
  - `noiseOctaves` (default 3)
  - `noiseGain` (default 0.5)
  - `noiseAmplitude` (default 1.0)
- Implement an integer-deterministic FBM sampler that blends value-noise samples at coarser grid sizes (2x2, 4x4, ...).
- Keep `octaves = 1` behavior identical to previous per-pixel value noise.

Validation
- Run unit tests: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj`
- Visual check of generated tiles with default parameters should retain similar visual jitter but show more coherent patches at larger scales when `noiseOctaves > 1`.

Notes
- Approach uses a deterministic integer mix function for sample values to avoid allocations and to preserve cross-platform determinism.
