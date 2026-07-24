# ICW-015: GenerateSet Validation and Parameter Semantics

- Status: In Progress
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Fix argument validation in `GenerateSet` so invalid parameters throw accurately attributed exceptions and clarify the `imageCount` versus `rows` semantics.

## Scope

- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs

## Validation

- `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - Passed: 23/23.

## Findings

- Completed: replaced grouped guards with per-parameter argument validation and added a focused test that verifies accurate `ParamName` attribution for invalid arguments.
- Remaining: `imageCount` dual-purpose behavior when `rows` is supplied still needs explicit documentation/tests.

## Next Step

- Finish the `rows` + `imageCount` behavior documentation/tests and then close the task.
