# ICW-030: Generation Input Bounds Policy

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Define and enforce safe upper bounds for scene generation controls, especially objects per tile, to prevent accidental OOM/freeze behavior.

## Scope

- src/InfiniteCanvas.App/MainWindow.xaml.cs
- src/InfiniteCanvas.Rendering/SampleImageGenerator.cs
- tests/InfiniteCanvas.Tests/SampleImageGeneratorTests.cs
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md

## Validation

- Pending:
  - `dotnet test .\tests\InfiniteCanvas.Tests\InfiniteCanvas.Tests.csproj --configuration Release`
  - `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- Current validation allows any non-negative objects-per-tile value.
- Generator allocates annotations directly by that count, creating a user-triggerable memory and latency hazard.
- Current defect raster sizing multiplies annotation dimensions by roughly `2.4x` to `4.5x`, increasing per-object allocation/work cost and amplifying the impact of unbounded object counts.

## Next Step

- Introduce policy limits with user-facing validation messages and tests for accepted/rejected boundary values.
