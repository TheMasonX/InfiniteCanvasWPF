# ICW-044: Axis Clamp and Derived Zoom Display

- Status: Done
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Allow one camera axis to continue non-linear zooming after the other reaches its zoom-out clamp, and make the zoom dropdown a command surface whose display reflects calculated zoom rather than rigid selection.

## Scope

- Clamp each axis independently during continued wheel zoom.
- Preserve non-linear zoom behavior on the unclamped axis.
- Treat preset items as commands that set a temporary camera state.
- Display the calculated ideal zoom percentage using the largest material axis constraint (for example, vertical height).

## Validation

- Add deterministic camera tests for one-axis-clamped continuation in both orientations.
- Add tests for calculated display percentage and preset override behavior after wheel zoom.

## Findings

- Current preset selection and uniform-first clamp behavior conflate command intent with actual camera state.
- Added pure zoom policy that clamps each wheel target independently.
- ComboBox clears preset selection after command execution and displays calculated percentage for the stricter fit axis.
- Focused policy tests and the full 32-test suite passed; Release app build succeeded.

## Next Step

- Visually verify cursor anchoring after one axis reaches its floor.