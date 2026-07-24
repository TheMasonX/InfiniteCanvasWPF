# ICW-015: GenerateSet Validation and Parameter Semantics

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Fix argument validation in `GenerateSet` so invalid parameters throw accurately attributed exceptions and clarify the `imageCount` versus `rows` semantics.

## Scope

- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`

## Findings

- Cross-validated audit finding: combined validation currently attributes multiple invalid inputs to `imageCount`.
- `imageCount` is dual-purpose when `rows` is provided and behavior needs explicit documentation/tests.

## Next Step

- Replace combined guards with per-parameter checks and add tests for all invalid-argument pathways.
