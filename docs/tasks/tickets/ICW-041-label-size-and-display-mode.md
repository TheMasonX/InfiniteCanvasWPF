# ICW-041: Label Size and Display Mode

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Reduce the default annotation label size to approximately 70% of its current value and let users display either class or ID, never both. Default to class.

## Scope

- Replace the combined class-and-ID label behavior with a Class/ID dropdown.
- Default the dropdown and generated labels to Class.
- Preserve global label visibility and positioning behavior.

## Validation

- Add focused label formatting/default tests where the logic is extracted or already testable.
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release`

## Findings

- Runtime screenshots show oversized labels and severe overlap at zoomed-out densities.
- Default label size changed from 12 to 8.5.
- Added a Class/ID dropdown defaulting to Class; combined labels were removed.
- Full test suite passed 32/32 and the Release app build succeeded.

## Next Step

- Visually review extreme zoom-out density and tune only if 8.5 remains too large.