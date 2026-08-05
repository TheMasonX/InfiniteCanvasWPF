# InfiniteCanvasWPF Boundary Audit

Report ID: 84a0cdb5f817
Commit: `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`
Scope: exhaustive deep dive of the `ICW-312` / `ICW-315` boundary shift and adjacent task-tracker changes.

## Executive summary

This commit is directionally strong: it removes the live pixelometer side effect, introduces a real scene-source seam, and moves frame publication from a `UIElement` tree to an immutable `CanvasFrame` payload. The validation story is also good: the commit message records Core 170/170, Windows 18/18, Release build 0 errors, and tracker cleanliness. The remaining issues are mostly architectural sharp edges rather than outright regressions.

The biggest gap is that the new abstraction is not yet deep enough to support the reuse goal that motivated `ICW-316`. `CanvasFrame` lives in `InfiniteCanvas.App`, `UpdateAnnotationLayer` still downcasts back to `SampleAnnotation`, and `MainWindow` still owns several shape-specific assumptions. The seam is real, but it is not yet a reusable component boundary.

## Findings

### 1) `CanvasFrame` is still anchored in the App assembly, which blocks the next extraction slice
`CanvasControl.PublishFrame(CanvasFrame)` is the right shape for `ICW-315`, but `CanvasFrame` itself lives under `src/InfiniteCanvas.App/Controls`. That means the public frame boundary still depends on an application assembly type, so `ICW-316` cannot be a pure “move `CanvasControl` and `CanvasViewModel`” extraction. The frame contract should move with the control or into a tiny shared boundary assembly; otherwise the next slice inherits an avoidable coupling point.

Impact: medium-high architectural debt, because it turns the extraction task into a broader type relocation than the tracker currently implies.

### 2) The new item contract is still shallow and leaks `SampleAnnotation` back into the host
`CanvasControl` now receives `IReadOnlyList<ICanvasItem>`, but `MainWindow.UpdateAnnotationLayer` immediately downcasts each item back to `SampleAnnotation` and silently skips anything else. That means the abstraction is only strong enough for counts and bounds; it is not yet strong enough for rendering, selection, or tooltip behavior.

This is exactly the kind of half-abstraction that tends to become a brittle “generic shell, app-specific escape hatch” over time. A renderer/interaction interface, or richer item metadata on `ICanvasItem`, would make the boundary honest.

Impact: medium, because it is not broken today, but it is a shallow module and a future maintenance trap.

### 3) The resident-pixel read path still relies on a magic probe and hidden tile-layout assumptions
`MainWindow.TryReadResidentPixel` uses a hardcoded `0.01 x 0.01` `SpatialBounds` probe to query annotations, and it still assumes uniform tile sizing by reaching through `_tiles[0]`, `_tileColumns`, and `TileGridIndexLookup`. That works under the current layout, but it is fragile and hard to reason about.

If the goal is to “lift us out of legacy systems cleanly,” this is one of the clearest places to do it: add a point-query contract or a named epsilon helper, and push the tile lookup responsibility behind the new scene-source interface instead of keeping it in the host.

Impact: medium, because the code is intentionally constrained today but still arbitrary and brittle.

### 4) `SampleImageTile.TryGetPixelValue` remains a side-effectful read method with a misleading name
The old generating pixel read path was removed from the pixelometer, which is good. But the tile API still contains `TryGetPixelValue(...)`, and that method still calls `TryGetPixelsNonBlocking(...)`, which can start generation. That is easy for a future caller to misuse because the name sounds pure.

This is a classic “unclear contract” smell. The safe resident-only path now exists, so the generating path should be named or classified so no one mistakes it for a harmless read.

Impact: medium, especially because this is exactly the kind of method that gets copied into future call sites.

### 5) `CanvasControl.PublishFrame` raises `FramePublished` synchronously
The control now owns the frame shell, but it also immediately calls back into the host during `PublishFrame`. That means frame display, view-model application, and overlay composition all run in one synchronous stack frame.

This is acceptable for now, but it makes the new boundary easier to stall or re-enter than it needs to be. If overlay composition grows or ever becomes async, this synchronous event becomes a coupling point.

Impact: low-medium. Not a bug today, but a future bottleneck.

### 6) The frame snapshot and visible-item list are still reference-based, not snapshot-based
`CanvasFrame.Items` and `CanvasViewModel.VisibleItems` both hold `IReadOnlyList<T>` references without defensive copying. That is fine if the lists are always fresh and immutable in practice, but the contract does not enforce that. A reused list from a query implementation could mutate underneath the frame snapshot.

Because this commit is explicitly about making the frame boundary stronger, this is worth tightening now. Immutable arrays or explicit snapshot copies would reduce the temporal coupling.

Impact: low-medium, but important for boundary integrity.

## Task alignment and duplication check

`ICW-312` and `ICW-315` are correctly split, and I did not find obvious duplication with `ICW-317` / `ICW-318`. The separation is actually useful: `ICW-317` is shell persistence, `ICW-318` is compositor fencing, `ICW-312` is data-source injection, and `ICW-315` is the frame payload boundary.

The main correction is scope, not duplication:

`ICW-316` should be widened to include `CanvasFrame` and any other frame-boundary contract types, not just `CanvasControl` and `CanvasViewModel`. Right now the next extraction slice is already partially baked into the app assembly.

`ICW-312` also has a small internal duplication smell: `ICanvasSceneSource.QueryVisible(...)` and `ICanvasSpatialQuerySource.QueryVisible(...)` are the same surface today. That may be fine as a staging move, but long term it probably wants consolidation or clearer specialization.

`ICW-314` remains the right follow-up for selection and tooltip ownership, but it should be treated as the place to formalize the item/render contract, not just move handlers.

## Recommendations

1. Move `CanvasFrame` into the eventual reusable boundary, or into a tiny shared contract assembly, before starting `ICW-316`.
2. Replace the `SampleAnnotation` downcast with a real item-rendering/interaction contract.
3. Replace the `0.01` probe and direct tile-layout reach-through with a named point-query or resident-read abstraction.
4. Rename or reclassify the generating tile read path so it cannot be mistaken for a pure query.
5. Consider snapshotting `CanvasFrame.Items` to an immutable representation at publication time.

## Validation reviewed

The commit message reports: Core 170/170, Windows 18/18, App Release 0 errors, tracker clean. The diff also adds dedicated gates for zero-reference boundaries, source-contract driving, and frame-shell wiring, which is the right kind of evidence for this boundary work.
