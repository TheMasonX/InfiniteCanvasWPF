# Handoff: Wave H — Canvas Assembly Extraction (ICW-316)

Date: 2026-08-05

## Status

The canvas component is now a separate WPF library. Another application can
reference it, implement the Core source interfaces, and publish a frame. The
app keeps overlay composition and the render pipeline.

## What Landed

### New library: `InfiniteCanvas.Controls`

- `CanvasControl.xaml` and `CanvasControl.xaml.cs` moved from
  `InfiniteCanvas.App/Controls` into `src/InfiniteCanvas.Controls`.
- `CanvasFrame.cs` moved with the control. Namespaces changed from
  `InfiniteCanvas.App.Controls` to `InfiniteCanvas.Controls`.
- `CanvasViewModel` stays in `InfiniteCanvas.ViewModels` (non-WPF net10.0) so
  the core test project is not retargeted.
- The library references only `InfiniteCanvas.Core` and
  `InfiniteCanvas.ViewModels`. It has no App, Rendering, or Spatial reference.
- `CanvasControl.xaml` now defines its own default brushes (BorderBrush,
  SecondaryTextBrush, AccentBrush). Values match the app baseline, so there is
  no visual change. A host app no longer needs to define these resources.
- `CanvasOverlayHost` stays internal. The app reaches it through
  `InternalsVisibleTo("InfiniteCanvas.App")`. Overlay composition moves into
  the library with ICW-314.

### App and solution wiring

- `MainWindow.xaml` points `controls:` at the library and uses
  `appcontrols:` for the app-local SliderTextBox and
  TileBackgroundNoiseSettingsView controls.
- `MainWindow.xaml.cs` imports `InfiniteCanvas.Controls`.
- `InfiniteCanvas.App.csproj` references the library. The solution file lists
  the new project.

### Tests

- `CanvasScrollbarWiringTests`, `FrameShellWiringTests`, and
  `CanvasBoundaryZeroReferenceTests` now assert the new library paths.
- `CanvasBoundaryZeroReferenceTests` adds `CanvasFrame.cs` to the boundary
  scan and a new gate: the Controls project references only Core and
  ViewModels.
- New consumer-host gate `CanvasControlConsumerHostTests` in the Windows test
  project. It constructs the control outside the app, sets `SceneSource`, and
  publishes a `CanvasFrame`. It proves the library loads with its own
  resources.

## Validation Evidence

- Full solution Release build: 0 errors (pre-existing `_frameClaimantId`
  unused-field warning and two benchmark warnings only).
- Core suite: 181/181 pass (was 179 at Wave G; +2 boundary-gate tests).
- Windows suite: 21/21 pass (was 18 at Wave G; +3 consumer-host tests).
- `pwsh -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`: clean.
- ADR-0007 marked Accepted.

## Decisions Taken

- Contracts stay in `InfiniteCanvas.Core`. No new contracts assembly.
- The canvas library defines its own default brushes. This keeps the control
  host-agnostic. If the app later changes a theme color, the canvas keeps the
  library default unless it is overridden.
- `CanvasOverlayHost` stays internal behind `InternalsVisibleTo`. This honors
  ICW-319 (no raw element surface on the public API) while the app keeps
  composing overlays until ICW-314 moves that work into the library.

## Open Items and Recommended Next Step

- ICW-313 (IInputHandler abstraction) and ICW-314 (selection and tooltip
  ownership) are the remaining ADR-0007 steps. Both are user-deferred.
- ICW-324 (seamless-noise decision) and ICW-325 (anisotropic mip selection)
  still need product decisions.
- ICW-144 needs fresh fast-scroll BenchmarkDotNet evidence on target hardware.
