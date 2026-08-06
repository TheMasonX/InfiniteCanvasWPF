# 2026-07-24 Handoff: Canvas Layers, Settings, and Zoom Corrections

## Status

- Branch: `main`
- Remote state: `origin/main` is at `ebaf13a`.
- Previous-agent handoff commit: `52e1064` (`perf: make sample image generation nonblocking`).
- Correction commit: `ebaf13a` (`feat: correct canvas layers settings and zoom`).
- Completed tasks: ICW-040 through ICW-044.
- Remaining task from this request: ICW-045 custom zoom entry redesign.

## User Requirements Captured

- Draw a dedicated grid overlay between background image tiles.
- Reduce default annotation label size to about 70% of its previous value.
- Display either annotation class or ID, defaulting to class, rather than both.
- Use `System.Drawing.Bitmap` as the Windows sparse defect image input.
- Generate grayscale defect bitmaps with background value 150 and 5-10 GDI+ circles.
- Render sparse defect bitmaps unaltered and separately from annotation shapes.
- Persist settings between runs and save current settings on close.
- Clamp zoom independently per axis so the free axis continues non-linear zooming.
- Treat zoom presets as temporary commands and display calculated zoom percentage.
- Remove the rejected standalone custom zoom UI and track its integrated replacement.

The requirements and evidence are recorded in `docs/tasks/active-tasks.md`, `docs/tasks/task-tracker.md`, and tickets ICW-040 through ICW-045.

## Implemented Changes

### Tile grid and annotation overlay

- `MainWindow.BuildFrameVisual` now inserts a non-hit-testable tile-grid `Canvas` between the raster `Image` and annotation overlay.
- Unique world-space tile edges are projected with the same captured `CameraSnapshot` as the raster frame.
- Shared boundaries are drawn once rather than receiving doubled strokes.
- Annotation labels now default to size 8.5 instead of 12.
- A Class/ID selector controls label text and defaults to Class.

### Sparse grayscale bitmap layer

- Windows defect templates remain pooled `System.Drawing.Bitmap` instances after generation.
- Templates are initialized to grayscale 150 and receive 5-10 darker circles through `Graphics.FillEllipse`.
- `ZeroCopyBitmapFactory.DrawDefectPatch` locks and samples the bitmap directly.
- Source intensity is copied equally into B, G, and R channels without class tinting or blending.
- The sparse image extent is independent of the logical annotation bounds; raster iteration is no longer clipped to the bounding box.
- Bounding boxes, labels, fills, and selection animation remain in the WPF shape overlay.

### Settings persistence

- `CanvasUserSettings` and `CanvasUserSettingsStore` live in `InfiniteCanvas.Core`.
- Settings are stored at `%LOCALAPPDATA%\InfiniteCanvas\settings.json`.
- The versioned JSON model validates generation and display ranges.
- Missing, malformed, inaccessible, or invalid files fall back to defaults.
- Saving writes a temporary file and atomically replaces the destination.
- `MainWindow` loads settings before initial scene generation and saves current control values on close.

### Zoom behavior

- `ViewportZoomPolicy` contains pure, tested wheel-clamp and display-percentage calculations.
- Wheel zoom computes independent X/Y targets; an axis at its fit floor remains fixed while the other continues scaling.
- The zoom ComboBox is an editable/read-only display surface: selecting a preset issues a command, clears selection, and later shows calculated percentage text.
- Calculated percentage uses the axis with the larger minimum fit scale, matching the material-axis constraint.
- The rejected standalone custom textbox, Apply button, and Custom preset item were removed.

## Validation Evidence

Run from the repository root:

```powershell
dotnet test .\InfiniteCanvasWPF.slnx --configuration Release
dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release
dotnet run --project .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release --no-build
```

Last verified results:

- Full test suite: 32 passed, 0 failed.
- Release WPF app build: succeeded.
- Runtime smoke test: app initialized and reported `Responding: True`.
- Focused Windows regression test verifies grayscale value 150 is preserved in B/G/R at a pixel outside the logical annotation bounds.
- Focused settings tests verify JSON round-trip and malformed/invalid-file fallback.
- Focused zoom tests verify free-axis continuation and largest-fit-axis percentage calculation.

## Behavioral Contracts

- The grid and annotation overlays must use the exact camera snapshot used to generate their raster frame.
- Sparse defect pixels are image data, not annotation styling. Do not tint them with `SampleAnnotation.Color`.
- `SampleAnnotation.Bounds` remains the spatial query and shape-overlay rectangle; it does not crop the linked defect bitmap.
- Zoom preset selection is command input, not persistent camera state. `ZoomPresetComboBox.Text` reflects calculated state after rendering.
- Persisted settings must remain versioned and validated before being applied to controls or generation.

## Remaining Work

1. ICW-045: add a Custom dropdown value whose integrated content contains a textbox and Apply action; Enter must invoke the same command.
2. Visually inspect the tile grid, label density, grayscale defects, and one-axis-clamped wheel anchoring at both near and far zoom levels.
3. Resolve bitmap-pool disposal ownership with ICW-029 shutdown/regeneration hardening. The current pooled bitmaps remain alive for the generated scene and are replaced on regeneration, but explicit deterministic disposal is not yet modeled.
4. Consider whether settings save should move from the `Closed` event into coordinated shutdown as part of ICW-029.

## Working Tree Guidance

An unrelated pre-existing modification remains in `.github/agents/infinitecanvas.agent.md`. It was not part of either pushed implementation commit and must not be reverted or included accidentally without review.

All implementation, test, task, ADR, and README changes described above are already committed and pushed. This handoff document is the only new file from the handoff step.
