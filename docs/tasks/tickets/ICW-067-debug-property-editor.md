---
id: ICW-067-debug-property-editor
key: ICW-067
title: Build reusable SliderTextBox controls and organize generator settings
status: Proposed
type: Task
priority: P2
tags:
  - icw
  - ui
  - controls
  - settings
dependsOn: []
related:
  - ICW-151
  - ICW-P1-SETTINGS-VALIDATION
links:
  - src/InfiniteCanvas.App/MainWindow.xaml
  - src/InfiniteCanvas.App/Controls/TileBackgroundSettingsView.xaml
  - src/InfiniteCanvas.App/Controls/TileBackgroundSettingsView.xaml.cs
  - src/InfiniteCanvas.ViewModels/MainViewModel.cs
  - docs/tasks/tickets/ICW-151-preserve-noise-settings-viewmodel-on-regeneration.md
created: 2026-07-25
updated: 2026-08-03
---

# ICW-067-debug-property-editor

## Audit Synthesis Scope Note (2026-08-04)

- `BoundedNumeric` Integer branch throws `ArgumentException` for fractional bounds narrower than one integer step instead of returning false (finding F-020 / C2-013). Fix within this ticket's scope, which owns `SliderTextBox` and `BoundedNumeric`.
- Align the status divergence for this ticket (active-tasks In Progress vs ticket Proposed) when the scope is updated.

## Summary

The debug panel repeats label-and-slider markup for the background noise settings and uses raw text boxes for tile generation fields. Create one reusable `SliderTextBox` control with the label above the slider and a numeric text box on the right.

## Scope

- Add a `SliderTextBox` `UserControl` with dependency properties for label, minimum, maximum, tick frequency, value, and numeric type.
- Support integer values such as octaves and tile counts, plus double values such as amplitude and lacunarity.
- Define one parse, clamp, and update path so slider and text-box edits stay synchronized.
- Replace the repeated background controls in `TileBackgroundSettingsView` with the new control.
- Replace Tiles X, Tiles Y, Objects per tile, and Generation seed inputs with the control or a shared numeric editor.
- Place the scene material settings in a dedicated expander.
- Use a lighter text resource for the tile background noise expander header.
- Preserve the existing binding and persistence contracts. ICW-151 tracks the separate data-context reset defect.

## Acceptance Criteria

- The control displays the label above the slider and the numeric text box on the right at a stable width.
- Integer controls reject fractional input and double controls accept finite decimal input.
- Values stay within the configured range after text entry, slider movement, focus loss, and regeneration.
- Noise controls use the new control without changing their configured ranges.
- Tiles X and Tiles Y use the new settings surface, and scene material settings appear in an expander.
- The tile background noise header uses a text color rather than the accent color.
- XAML compilation and focused view-model tests pass.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`
- Command: `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`
- Result: Research complete. Implementation and focused UI or view-model tests are pending.

## Notes

- WPF bindings use `double` for `Slider.Value`, so the control must keep a numeric editing value separate from display formatting or use a typed adapter.
- Do not duplicate validation rules between XAML, JSON loading, and the control. Coordinate field bounds with ICW-P1-SETTINGS-VALIDATION.
- The current noise view model stores all fields as `double`; integer fields are clamped and rounded when snapshotted for generation.

## Related Tasks

- ICW-151
- ICW-P1-SETTINGS-VALIDATION
