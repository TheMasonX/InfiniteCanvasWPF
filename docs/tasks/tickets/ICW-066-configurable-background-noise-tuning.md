# ICW-066: Configurable background noise and defect-circle tuning

## Status
In Progress

## Summary
Expose the background tile noise intensity and defect-circle density as runtime-editable settings that are persisted and covered by tests.

## Scope
- Extend the generator and settings model so the background tile noise amplitude and defect circle count can be configured.
- Surface these settings in the existing display/generation UI with clear validation and persistence.
- Preserve deterministic generation and add focused regression tests around the new settings.

## Validation
- dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Debug
- dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release

## Findings
- The current generator already uses deterministic noise and defect circles, but the parameters are hardcoded inside the renderer/generator. The request should turn those into editable runtime properties rather than hidden constants.

## Next Step
Introduce a small settings model in the core layer, wire it into the generator call path, and expose it in the UI.
