---
id: ICW-192
key: ICW-192
status: To Do
title: Centralize byte conversion and remove redundant casts in generator pipeline
type: Task
priority: P3
tags: [rendering, micro-optimizations, cleanup]
---

Summary
- Reduce repeated `(byte)` casts and numeric noise in helper APIs by centralizing final `byte` conversion at the pixel-sink boundary. Keep intermediate computations in `float`/`double`/`int` as appropriate.

Scope
- Audit `SampleImageGenerator` helpers (`GenerateNoisePixelsCore`, `ApplyMipDetails`, `GenerateCenteredDefectPixels`, `CreateBitmapFromPixels`) and update signatures to use consistent numeric types. Convert to `byte` only when writing into output buffers.

Validation
- Unit tests confirming identical pixel outputs for representative seeds; verify improved readability and fewer casts in the codebase.

Next step
- Implement changes in a small PR limited to internal helpers, run `dotnet test`, and observe no output deltas for tests that assert pixel values.
