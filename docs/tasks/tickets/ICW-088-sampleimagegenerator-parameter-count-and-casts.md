---
id: ICW-088
status: To Do
title: Reduce parameter count and unnecessary casts in `SampleImageGenerator` methods
type: Task
priority: P3
tags: [rendering, api-design, clumsy]
---

Summary
- Sonar flagged `SampleImageGenerator` methods with high parameter counts (up to 11) and unnecessary casts to `byte`.

Scope
- Consolidate related parameters into small configuration objects (e.g., `GeneratorOptions`), remove redundant casts, and add unit tests.

Validation
- Public generator API is preserved or updated with a clear migration path; `dotnet build` and unit tests pass.

Next step
- Propose a small API wrapper type `GeneratorOptions` and migration GUID; implement in a single PR to minimize churn.

Findings (quick)
- Many public and private methods accept long parameter lists that repeatedly carry the same small set of values: `nativeWidth/nativeHeight`, `targetValue`, `noise`, `seed`, `circleCount`, `NoiseSettings`, and world origin coordinates. Examples: `GenerateSet(...)`, `GenerateMonochromeMipPixels(...)`, `GenerateNoisePixelsCore(...)`, and `ApplyMipDetails(...)`.
- Several overloads exist solely to default the `mipLevel` or `seed` values; this increases surface area and test burden.
- Primitive obsession / Data clumps: world origin floats, width/height pairs, and generation tuning scalars travel together and read like a type that wants to be born.
- Speculative generality: some parameters (per-tile `tileLabel`, `circleCount`) are passed through many layers though they're only used in one pass (label/circle rasterization).

Recommendations
- Introduce small focused option/value types to reduce argument counts (examples below). Make them immutable records with clear defaults so callers can easily override only needed values.
	- `GeneratorOptions` — image-level settings (imageCount, pixelWidth, pixelHeight, targetValue, noise, objectsPerTile, columns, rows, seed, defectPoolSize, circleCount).
	- `MipOptions` — mip-generation inputs (mipLevel, seed, NoiseSettings, worldOrigin: Point/Vector2, tileLabel).
	- `CircleOptions` (optional) — circleCount and any circle-specific tunables if you expect more controls later.
- Replace multi-overload surface with one main method accepting `GeneratorOptions` and helper `GenerateMipPixels(nativeSize, mipOptions)` for per-mip generation.
- Extract responsibilities: move annotation generation into an `AnnotationGenerator` class, and defect-template pool building into a `DefectTemplateFactory` to keep `SampleImageGenerator` focused on wiring and composition.
- Reduce redundant casts by keeping numeric types consistent (use ints/doubles where appropriate and only cast at the final buffer write). Prefer `byte` conversions at a single sink.
- Keep `NoiseSettings` but pass it inside `MipOptions` so noise-related params don't travel separately.

Proposed PR work (small, incremental)
1. Add `GeneratorOptions` and `MipOptions` records with default static instances and XML docs.
2. Implement a thin adapter overload that accepts `GeneratorOptions` while keeping the old overloads forwarding to it (deprecation path). Add tests that call both to prove parity.
3. Extract `GenerateAnnotations(...)` into `AnnotationGenerator.Generate(tileId, bounds, options, seed)` and update callers.
4. Extract defect-pool creation into `DefectTemplateFactory.Build(count, seed)` and wire into `GenerateSet`.
5. Replace the long-parameter private helpers with option structs (e.g., `GenerateNoisePixelsCore(pixels, width, height, mipOptions)`), remove redundant casts, and run unit tests.
6. When green, remove deprecated forwarding overloads in a follow-up PR.

Validation
- `dotnet build` and `dotnet test` before and after the change should be equivalent; add focused unit tests asserting that the new `GeneratorOptions`-based API produces identical pixel outputs for representative seeds.

Risk and mitigations
- This is an API-level mechanical refactor; keep public behavior stable by retaining forwarding overloads for one release and adding migration notes in `ICW-015`/`ICW-101` as needed.
